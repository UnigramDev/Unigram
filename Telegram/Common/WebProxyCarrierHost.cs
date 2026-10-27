//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Storage;
using Windows.System;

namespace Telegram.Common
{
    // Hosts the hidden web views behind TDLib web proxy carriers, from every session. TDLib owns the
    // relay protocol; each carrier only loads its page in a locked-down WebView2 and moves messages
    // between it and TDLib verbatim.
    // Updates arrive on TDLib's thread and are queued; everything else runs on the carrier thread,
    // which owns the controllers: a never-shown view of its own on UWP, the main window's on Win32.
    // Every carrier has a controller of its own, because the relay serves its bridge page with
    // frame-ancestors 'none', but they share one environment and so one browser process.
    public sealed partial class WebProxyCarrierHost
    {
        public static readonly WebProxyCarrierHost Current = new();

        // Page messages are prefixed: 'c' text, 'b' base64 binary, 'f' bridge failure.
        // WebView2 only carries strings, so binary data crosses the boundary as base64.
        private const string BridgeScript = @"(()=>{
if(window!==window.top||Object.prototype.hasOwnProperty.call(window,'TelegramWebProxy'))return;
const webview=window.chrome&&window.chrome.webview;
if(!webview)return;
const listeners=[];
let receiver=null;
const encode=value=>{
 const bytes=new Uint8Array(value),parts=[];
 for(let i=0;i<bytes.length;i+=32768)parts.push(String.fromCharCode.apply(null,bytes.subarray(i,i+32768)));
 return btoa(parts.join(''));
};
const decode=value=>{
 const binary=atob(value),bytes=new Uint8Array(binary.length);
 for(let i=0;i<binary.length;i++)bytes[i]=binary.charCodeAt(i);
 return bytes.buffer;
};
const dispatch=data=>{
 const event={data};
 if(typeof receiver==='function')receiver(event);
 for(const listener of listeners.slice())listener(event);
};
webview.addEventListener('message',e=>{
 const value=e.data;
 if(typeof value!=='string'||value.length===0)return;
 try{dispatch(value[0]==='b'?decode(value.substring(1)):value.substring(1))}catch(error){webview.postMessage('f')}
});
const bridge={
 postMessage(value){
  if(value instanceof ArrayBuffer)webview.postMessage('b'+encode(value));
  else if(ArrayBuffer.isView(value))webview.postMessage('b'+encode(value.buffer.slice(value.byteOffset,value.byteOffset+value.byteLength)));
  else if(typeof value==='string')webview.postMessage('c'+value);
  else webview.postMessage('f');
 },
 addEventListener(type,listener){if(type==='message'&&typeof listener==='function')listeners.push(listener)},
 removeEventListener(type,listener){const i=listeners.indexOf(listener);if(i>=0)listeners.splice(i,1)},
 get onmessage(){return receiver},
 set onmessage(value){receiver=typeof value==='function'?value:null}
};
Object.defineProperty(window,'TelegramWebProxy',{value:Object.freeze(bridge),configurable:false,writable:false});
})();";

        // The operator's page only needs fetch and WebSocket to its own origin. This runs in every
        // frame, so a fresh about:blank realm cannot be used to get the removed APIs back.
        private const string LockScript = @"(()=>{
const remove=(target,name)=>{try{Object.defineProperty(target,name,{value:undefined,configurable:false,writable:false})}catch(error){}};
for(const name of ['RTCPeerConnection','webkitRTCPeerConnection','RTCDataChannel','RTCSessionDescription','RTCIceCandidate','WebTransport','WebAssembly','Worker','SharedWorker','Notification','PaymentRequest','PresentationRequest','SpeechSynthesisUtterance','webkitSpeechRecognition','showOpenFilePicker','showSaveFilePicker','showDirectoryPicker','localStorage','sessionStorage','indexedDB','caches'])remove(window,name);
const navigator=window.navigator;
for(const name of ['serviceWorker','mediaDevices','getUserMedia','geolocation','credentials','storage','locks','wakeLock','share','xr','bluetooth','usb','hid','serial','sendBeacon','permissions','getGamepads'])remove(navigator,name);
})();";

        private const string LockPolicyScript = @"(()=>{
if(window!==window.top)return;
const apply=()=>{
 const meta=document.createElement('meta');
 meta.httpEquiv='Content-Security-Policy';
 meta.content=""default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' wss://""+location.host+""; object-src 'none'; frame-src 'none'; worker-src 'none'; base-uri 'none'; form-action 'none'"";
 (document.head||document.documentElement).prepend(meta);
};
if(document.documentElement)apply();else document.addEventListener('readystatechange',apply,{once:true});
})();";

        // TDLib's {"t":"close"}, delivered as the bridge's text message
        private const string CloseMessage = "c{\"t\":\"close\"}";

        private const int MaxPageMessageLength = 4 * 1024 * 1024;
        private const int MaxItemsPerDrain = 32;

        private static readonly TimeSpan CloseGracePeriod = TimeSpan.FromMilliseconds(200);

        private static Task<CoreWebView2Environment> _environmentTask;

        private enum ItemKind
        {
            Open,
            Data,
            Close
        }

        private readonly struct Item
        {
            public Item(ItemKind kind, IClientService clientService, int carrierId, string key, WebProxyCarrier carrier, string payload)
            {
                Kind = kind;
                ClientService = clientService;
                CarrierId = carrierId;
                Key = key;
                Carrier = carrier;
                Payload = payload;
            }

            public readonly ItemKind Kind;
            public readonly IClientService ClientService;
            public readonly int CarrierId;
            public readonly string Key;
            public readonly WebProxyCarrier Carrier;
            public readonly string Payload;
        }

        // Guarded by _lock, touched from any thread
        private readonly object _lock = new();
        private readonly Queue<Item> _items = new();
        private DispatcherQueue _queue;
        private bool _starting;
        private bool _drainScheduled;
        private readonly DispatcherQueueHandler _drain;

        // Carrier thread only
        private readonly Dictionary<string, Carrier> _carriers = new();
        private CoreWebView2Environment _environment;
        private DispatcherQueueTimer _keepActiveTimer;

        private WebProxyCarrierHost()
        {
            _drain = Drain;
        }

        public static bool IsSupported()
        {
            try
            {
                return ApiInfo.IsDesktop && !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
            }
            catch
            {
                return false;
            }
        }

        public void Open(IClientService clientService, WebProxyCarrier carrier)
        {
            Enqueue(new Item(ItemKind.Open, clientService, carrier.Id, GetKey(clientService, carrier.Id), carrier, null));
        }

        // Encoded here, on TDLib's thread, so the carrier thread only hands the string to the page
        public void Post(IClientService clientService, int carrierId, byte[] data)
        {
            Enqueue(new Item(ItemKind.Data, clientService, carrierId, GetKey(clientService, carrierId), null, "b" + Convert.ToBase64String(data)));
        }

        public void Close(IClientService clientService, int carrierId)
        {
            Enqueue(new Item(ItemKind.Close, clientService, carrierId, GetKey(clientService, carrierId), null, null));
        }

        // Carrier ids are only unique within one TDLib instance
        private static string GetKey(IClientService clientService, int carrierId)
        {
            return clientService.SessionId + "-" + carrierId;
        }

        #region Queue

        // TDLib requires the messages to reach the page in update order, so every update goes through
        // one FIFO, drained on the carrier thread.
        private void Enqueue(Item item)
        {
            DispatcherQueue queue;

            lock (_lock)
            {
                _items.Enqueue(item);

                if (_queue == null)
                {
                    if (_starting)
                    {
                        return;
                    }

                    _starting = true;
                    queue = null;
                }
                else if (_drainScheduled)
                {
                    return;
                }
                else
                {
                    _drainScheduled = true;
                    queue = _queue;
                }
            }

            if (queue == null)
            {
                Logger.Info("[WebProxy] Starting the carrier thread");
                StartThread();
            }
            else if (!queue.TryEnqueue(_drain))
            {
                Logger.Error("[WebProxy] The carrier thread is shutting down");
            }
        }

        private void OnThreadStarted(DispatcherQueue queue)
        {
            bool schedule;

            lock (_lock)
            {
                _queue = queue;
                _starting = false;

                schedule = _items.Count > 0 && !_drainScheduled;
                _drainScheduled |= schedule;
            }

            Logger.Info("[WebProxy] Carrier thread started");

            if (schedule)
            {
                queue.TryEnqueue(_drain);
            }
        }

        // Nothing can host a carrier: every carrier waiting to open is failed, and TDLib retries later
        private void OnThreadFailed(WebProxyCarrierError error)
        {
            Item[] items;

            lock (_lock)
            {
                _starting = false;
                items = _items.ToArray();
                _items.Clear();
            }

            foreach (var item in items)
            {
                if (item.Kind == ItemKind.Open)
                {
                    Logger.Info($"[WebProxy {item.Key}] Can't start the carrier thread, reporting {error.GetType().Name}");
                    item.ClientService.Send(new ReportWebProxyCarrierError(item.CarrierId, error));
                }
            }
        }

        private void Drain()
        {
            for (int i = 0; i < MaxItemsPerDrain; i++)
            {
                Item item;

                lock (_lock)
                {
                    if (_items.Count == 0)
                    {
                        _drainScheduled = false;
                        return;
                    }

                    item = _items.Dequeue();
                }

                switch (item.Kind)
                {
                    case ItemKind.Open:
                        OpenCarrier(item);
                        break;
                    case ItemKind.Data:
                        if (_carriers.TryGetValue(item.Key, out Carrier carrier))
                        {
                            carrier.Post(item.Payload);
                        }
                        break;
                    case ItemKind.Close:
                        CloseCarrier(item.Key);
                        break;
                }
            }

            // Yield to the web views' own events between batches
            DispatcherQueue.GetForCurrentThread().TryEnqueue(_drain);
        }

        #endregion

        #region Carriers

        private void OpenCarrier(Item item)
        {
            if (!IsSupported())
            {
                Logger.Info($"[WebProxy {item.Key}] WebView2 runtime is not available");
                item.ClientService.Send(new ReportWebProxyCarrierError(item.CarrierId, new WebProxyCarrierErrorUnsupported()));
                return;
            }

            var carrier = new Carrier(this, item.ClientService, item.Key, item.Carrier);
            _carriers[item.Key] = carrier;

            if (_keepActiveTimer == null)
            {
                _keepActiveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
                _keepActiveTimer.Interval = TimeSpan.FromSeconds(1);
                _keepActiveTimer.Tick += OnKeepActiveTick;
                _keepActiveTimer.Start();
            }

            carrier.Open();
        }

        private void CloseCarrier(string key)
        {
            if (!_carriers.TryGetValue(key, out Carrier carrier))
            {
                Logger.Info($"[WebProxy {key}] Closed, but no web view was hosting it");
                return;
            }

            _carriers.Remove(key);
            carrier.Close();

            if (_carriers.Count == 0 && _keepActiveTimer != null)
            {
                _keepActiveTimer.Stop();
                _keepActiveTimer.Tick -= OnKeepActiveTick;
                _keepActiveTimer = null;
            }
        }

        private static Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            return _environmentTask ??= CreateEnvironmentAsync();
        }

        private static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
        {
            // A separate user data folder, because these arguments must not reach the mini app web views.
            // SmartScreen would report the capability URL, and WebRTC could leak the local address.
            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = "--disable-features=msSmartScreenProtection --force-webrtc-ip-handling-policy=disable_non_proxied_udp"
            };

            var folder = Path.Combine(AppPaths.LocalCachePath, "WebProxy");
            return await CoreWebView2Environment.CreateWithOptionsAsync(string.Empty, folder, options);
        }

        #endregion

        #region Carrier

        // One carrier's web view. Its event handlers live here, so each controller's events reach its
        // own carrier without looking the sender up.
        private sealed class Carrier
        {
            private readonly WebProxyCarrierHost _host;
            private readonly IClientService _clientService;
            private readonly string _key;
            private readonly int _id;
            private readonly string _url;
            private readonly string _urlWithoutFragment;
            private readonly string _allowedPrefix;
            private readonly string _allowedPrefixWithPort;

            private CoreWebView2Controller _controller;
            private CoreWebView2 _core;
            private DispatcherQueueTimer _closeTimer;
            private ulong _navigationId;

            private bool _closed;
            private bool _failed;
            private bool _initialized;

            private int _receivedCount;
            private long _receivedLength;
            private int _postedCount;
            private long _postedLength;

            public Carrier(WebProxyCarrierHost host, IClientService clientService, string key, WebProxyCarrier carrier)
            {
                _host = host;
                _clientService = clientService;
                _key = key;
                _id = carrier.Id;
                _url = carrier.Url;
                _urlWithoutFragment = WithoutFragment(carrier.Url);
                _allowedPrefix = carrier.Origin + carrier.PathPrefix;
                _allowedPrefixWithPort = carrier.Origin + ":443" + carrier.PathPrefix;

                Log($"Created for proxy {carrier.ProxyId}, temporary: {carrier.IsTemporary}, prefix: {_allowedPrefix}");
            }

            public async void Open()
            {
                try
                {
                    _host._environment = await GetEnvironmentAsync();
                }
                catch (Exception ex)
                {
                    Log("Failed to create the environment: " + Describe(ex));

                    _environmentTask = null;
                    Fail(new WebProxyCarrierErrorUnsupported());
                    return;
                }

                try
                {
                    // InPrivate keeps storage, cookies and cache in memory, so every carrier starts clean
                    var options = _host._environment.CreateCoreWebView2ControllerOptions();
                    options.IsInPrivateModeEnabled = true;
                    options.ProfileName = "WebProxy";

                    // Composition, because a windowed controller can't be parented to a CoreWindow. It is
                    // never given a RootVisualTarget, so nothing is ever rendered.
                    CoreWebView2Controller controller = await _host._environment.CreateCoreWebView2CompositionControllerAsync(CreateParentWindow(), options);
                    if (_closed)
                    {
                        Log("Closed while the controller was being created");
                        controller.Close();
                        return;
                    }

                    Log("Controller created, browser process " + controller.CoreWebView2.BrowserProcessId);

                    controller.IsVisible = false;

                    _controller = controller;
                    _core = controller.CoreWebView2;

                    ConfigureSettings(_core.Settings);

                    _core.NavigationStarting += OnNavigationStarting;
                    _core.NavigationCompleted += OnNavigationCompleted;
                    _core.FrameNavigationStarting += OnFrameNavigationStarting;
                    _core.NewWindowRequested += OnNewWindowRequested;
                    _core.PermissionRequested += OnPermissionRequested;
                    _core.DownloadStarting += OnDownloadStarting;
                    _core.WebResourceRequested += OnWebResourceRequested;
                    _core.WebMessageReceived += OnWebMessageReceived;
                    _core.ProcessFailed += OnProcessFailed;

                    _core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);

                    await _core.AddScriptToExecuteOnDocumentCreatedAsync(LockScript);
                    await _core.AddScriptToExecuteOnDocumentCreatedAsync(LockPolicyScript);
                    await _core.AddScriptToExecuteOnDocumentCreatedAsync(BridgeScript);

                    if (_closed)
                    {
                        Log("Closed while the scripts were being injected");
                        return;
                    }

                    _core.Navigate(_url);
                }
                catch (Exception ex)
                {
                    Log("Failed to create the controller: " + Describe(ex));
                    Fail(new WebProxyCarrierErrorTerminated());
                }
            }

            public void Post(string payload)
            {
                if (_core == null || _closed || _failed)
                {
                    return;
                }

                try
                {
                    if (_postedCount == 0)
                    {
                        Log("First binary message to the page");
                    }

                    _postedCount++;
                    _postedLength += payload.Length;
                    _core.PostWebMessageAsString(payload);
                }
                catch (Exception ex)
                {
                    Log("Failed to post to the page: " + Describe(ex));
                    Fail(new WebProxyCarrierErrorTerminated());
                }
            }

            // A page that has spoken to the bridge has a relay session, which it ends on {"t":"close"};
            // it needs a moment to send that before the web view goes away.
            public void Close()
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;

                Log($"Closing, received {_receivedCount} messages ({_receivedLength} chars), posted {_postedCount} binary messages ({_postedLength} chars)");

                if (_core != null && _initialized && !_failed)
                {
                    try
                    {
                        _core.PostWebMessageAsString(CloseMessage);

                        _closeTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
                        _closeTimer.Interval = CloseGracePeriod;
                        _closeTimer.IsRepeating = false;
                        _closeTimer.Tick += OnCloseTick;
                        _closeTimer.Start();
                        return;
                    }
                    catch
                    {
                        // The page is gone already
                    }
                }

                Destroy();
            }

            private void OnCloseTick(DispatcherQueueTimer sender, object args)
            {
                Destroy();
            }

            private void Destroy()
            {
                if (_closeTimer != null)
                {
                    _closeTimer.Stop();
                    _closeTimer.Tick -= OnCloseTick;
                    _closeTimer = null;
                }

                if (_core != null)
                {
                    _core.NavigationStarting -= OnNavigationStarting;
                    _core.NavigationCompleted -= OnNavigationCompleted;
                    _core.FrameNavigationStarting -= OnFrameNavigationStarting;
                    _core.NewWindowRequested -= OnNewWindowRequested;
                    _core.PermissionRequested -= OnPermissionRequested;
                    _core.DownloadStarting -= OnDownloadStarting;
                    _core.WebResourceRequested -= OnWebResourceRequested;
                    _core.WebMessageReceived -= OnWebMessageReceived;
                    _core.ProcessFailed -= OnProcessFailed;
                    _core = null;
                }

                if (_controller != null)
                {
                    try
                    {
                        _controller.Close();
                    }
                    catch
                    {
                        // The browser process may already be gone
                    }

                    _controller = null;
                }
            }

            // TDLib answers with updateWebProxyCarrierClosed, which is what destroys the web view
            private void Fail(WebProxyCarrierError error)
            {
                if (_failed || _closed)
                {
                    return;
                }

                Log("Reporting " + error.GetType().Name);
                _failed = true;
                _clientService.Send(new ReportWebProxyCarrierError(_id, error));
            }

            private bool IsAllowedUri(string uri)
            {
                return uri.StartsWith(_allowedPrefix, StringComparison.Ordinal)
                    || uri.StartsWith(_allowedPrefixWithPort, StringComparison.Ordinal);
            }

            private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
            {
                if (_navigationId == 0 && !args.IsRedirected && WithoutFragment(args.Uri) == _urlWithoutFragment)
                {
                    _navigationId = args.NavigationId;
                    return;
                }

                // Only the bridge URL may ever be loaded: no reloads, redirects or links away
                Log($"Cancelled navigation to {Describe(args.Uri)}, redirect: {args.IsRedirected}");
                args.Cancel = true;
            }

            private void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
            {
                if (args.NavigationId != _navigationId)
                {
                    return;
                }

                int statusCode = 0;
                try
                {
                    statusCode = args.HttpStatusCode;
                }
                catch
                {
                    // Old runtime
                }

                Log($"Navigation completed, success: {args.IsSuccess}, status: {args.WebErrorStatus}, HTTP {statusCode}");

                if (!args.IsSuccess)
                {
                    Fail(new WebProxyCarrierErrorLoadFailed(args.WebErrorStatus.ToString()));
                }
                else if (statusCode >= 400)
                {
                    Fail(new WebProxyCarrierErrorLoadFailed("HTTP " + statusCode));
                }
            }

            private void OnFrameNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
            {
                Log("Cancelled frame navigation to " + Describe(args.Uri));
                args.Cancel = true;
            }

            private void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
            {
                // Handled without a NewWindow means nothing is opened, not even in the system browser
                Log("Refused a new window for " + Describe(args.Uri));
                args.Handled = true;
            }

            private void OnPermissionRequested(CoreWebView2 sender, CoreWebView2PermissionRequestedEventArgs args)
            {
                Log("Denied permission " + args.PermissionKind);
                args.State = CoreWebView2PermissionState.Deny;
            }

            private void OnDownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
            {
                Log("Cancelled a download");
                args.Cancel = true;
            }

            private void OnWebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
            {
                var uri = args.Request.Uri;
                if (IsAllowedUri(uri) || !uri.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Log($"Blocked {args.Request.Method} {Describe(uri)}");
                args.Response = _host._environment.CreateWebResourceResponse(null, 403, "Forbidden", string.Empty);
            }

            private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
            {
                if (_closed || _failed)
                {
                    return;
                }

                if (!IsAllowedUri(args.Source))
                {
                    Log("Ignored a message from " + Describe(args.Source));
                    return;
                }

                string message;
                try
                {
                    message = args.TryGetWebMessageAsString();
                }
                catch (Exception ex)
                {
                    Log("Ignored a message that isn't a string: " + Describe(ex));
                    return;
                }

                if (string.IsNullOrEmpty(message) || message.Length > MaxPageMessageLength)
                {
                    Log("Ignored a message of length " + (message?.Length ?? 0));
                    return;
                }

                _initialized = true;
                _receivedCount++;
                _receivedLength += message.Length;

                switch (message[0])
                {
                    case 'c':
                        _clientService.Send(new ProcessWebProxyCarrierMessage(_id, new WebProxyCarrierMessageText(message.Substring(1))));
                        break;
                    case 'b':
                        byte[] data;
                        try
                        {
                            data = Convert.FromBase64String(message.Substring(1));
                        }
                        catch (Exception ex)
                        {
                            Log("Invalid binary message from the page: " + Describe(ex));
                            Fail(new WebProxyCarrierErrorTerminated());
                            return;
                        }

                        _clientService.Send(new ProcessWebProxyCarrierMessage(_id, new WebProxyCarrierMessageBinary(data)));
                        break;
                    case 'f':
                        Log("The bridge script reported a failure");
                        Fail(new WebProxyCarrierErrorTerminated());
                        break;
                    default:
                        Log("Ignored a message with prefix " + (int)message[0]);
                        break;
                }
            }

            private void OnProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
            {
                Log($"Process failed: {args.ProcessFailedKind}, reason: {args.Reason}, exit code: {args.ExitCode}");
                Fail(new WebProxyCarrierErrorTerminated());
            }

            private void Log(string message)
            {
                Logger.Info($"[WebProxy {_key}] {message}");
            }
        }

        private static void ConfigureSettings(CoreWebView2Settings settings)
        {
            settings.IsScriptEnabled = true;
            settings.IsWebMessageEnabled = true;
            settings.AreHostObjectsAllowed = false;
            settings.AreDefaultScriptDialogsEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;
            settings.IsBuiltInErrorPageEnabled = false;

            // Each of these needs a newer runtime than the one before, so they fail independently
            try
            {
                settings.AreBrowserAcceleratorKeysEnabled = false;
                settings.IsPasswordAutosaveEnabled = false;
                settings.IsGeneralAutofillEnabled = false;
            }
            catch
            {
                // Old runtime
            }

            try
            {
                settings.IsReputationCheckingRequired = false;
            }
            catch
            {
                // Old runtime
            }
        }

        #endregion

        #region Keep active

        private const uint PROCESS_SET_INFORMATION = 0x0200;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const uint NORMAL_PRIORITY_CLASS = 0x0020;
        private const uint IDLE_PRIORITY_CLASS = 0x0040;
        private const uint BELOW_NORMAL_PRIORITY_CLASS = 0x4000;

        private static bool _keepActiveFailureLogged;

        // Windows demotes the processes of a web view nobody can see, which throttles the relay
        // page's timers and network loop. TDesktop restores them every second for the same reason.
        private void OnKeepActiveTick(DispatcherQueueTimer sender, object args)
        {
            if (_environment == null)
            {
                return;
            }

            try
            {
                // The environment is used by nothing else, so each of these is a relay web view process
                foreach (var info in _environment.GetProcessInfos())
                {
                    if (info.ProcessId <= 0)
                    {
                        continue;
                    }

                    var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_INFORMATION, 0, (uint)info.ProcessId);
                    if (process == IntPtr.Zero)
                    {
                        if (!_keepActiveFailureLogged)
                        {
                            _keepActiveFailureLogged = true;
                            Logger.Info($"[WebProxy] Can't open web view process {info.ProcessId}, error {Marshal.GetLastWin32Error()}");
                        }

                        continue;
                    }

                    var priority = GetPriorityClass(process);
                    if (priority == IDLE_PRIORITY_CLASS || priority == BELOW_NORMAL_PRIORITY_CLASS)
                    {
                        SetPriorityClass(process, NORMAL_PRIORITY_CLASS);
                    }

                    CloseHandle(process);
                }
            }
            catch
            {
                // Old runtime, or the browser process is gone
            }
        }

#if NET9_0_OR_GREATER
        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial IntPtr OpenProcess(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);

        [LibraryImport("kernel32.dll")]
        private static partial uint GetPriorityClass(IntPtr hProcess);

        [LibraryImport("kernel32.dll")]
        private static partial int SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [LibraryImport("kernel32.dll")]
        private static partial int CloseHandle(IntPtr hObject);
#else
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, int bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetPriorityClass(IntPtr hProcess);

        [DllImport("kernel32.dll")]
        private static extern int SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("kernel32.dll")]
        private static extern int CloseHandle(IntPtr hObject);
#endif

        #endregion

        private static string WithoutFragment(string uri)
        {
            var index = uri.IndexOf('#');
            return index < 0 ? uri : uri.Substring(0, index);
        }

        // The query holds the bridge capability and the fragment the nonce, so neither is ever logged
        private static string Describe(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return "(empty)";
            }

            var index = uri.IndexOfAny(new[] { '?', '#' });
            return index < 0 ? uri : uri.Substring(0, index) + " (query omitted)";
        }

        private static string Describe(Exception ex)
        {
            return $"{ex.GetType().Name} 0x{ex.HResult:X8} {ex.Message}";
        }
    }
}
