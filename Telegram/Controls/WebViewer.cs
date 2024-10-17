using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Services;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class WebViewerEventReceivedEventArgs : EventArgs
    {
        public string EventName { get; }

        public JsonObject EventData { get; }

        public WebViewerEventReceivedEventArgs(string eventName, JsonObject eventData)
        {
            EventName = eventName;
            EventData = eventData;
        }
    }

    public partial class WebViewerNavigatingEventArgs : CancelEventArgs
    {
        public string Url { get; }

        public WebViewerNavigatingEventArgs(string url)
        {
            Url = url;
        }
    }

    public partial class WebViewerNavigatedEventArgs : EventArgs
    {
        public string Url { get; }

        public WebViewerNavigatedEventArgs(string url)
        {
            Url = url;
        }
    }

    public partial class WebViewerNewWindowRequestedEventArgs : CancelEventArgs
    {
        public string Url { get; }

        public WebViewerNewWindowRequestedEventArgs(string url)
        {
            Url = url;
        }
    }

    public partial class WebViewer : ContentControl
    {
        private WebPresenter _presenter;
        private bool _initialized;
        private bool _closed;

        public WebViewer()
        {
            DefaultStyleKey = typeof(WebViewer);

            if (ChromiumWebPresenter.IsSupported())
            {
                _presenter = new ChromiumWebPresenter();
            }
            else
            {
                _presenter = new EdgeWebPresenter();
            }

            _presenter.Navigating += OnNavigating;
            _presenter.Navigated += OnNavigated;
            _presenter.EventReceived += OnEventReceived;
            _presenter.NewWindowRequested += OnNewWindowRequested;

            Content = _presenter;
        }

        private async Task<bool> EnsureInitializedAsync()
        {
            if (_initialized || _closed)
            {
                goto Cleanup;
            }

            var succeeded = await _presenter.EnsureInitializedAsync();
            if (succeeded || _closed || _presenter is not ChromiumWebPresenter)
            {
                _initialized = true;
                goto Cleanup;
            }

            _presenter.Navigating -= OnNavigating;
            _presenter.Navigated -= OnNavigated;
            _presenter.EventReceived -= OnEventReceived;
            _presenter.NewWindowRequested -= OnNewWindowRequested;

            _presenter = new EdgeWebPresenter();
            _presenter.Navigating += OnNavigating;
            _presenter.Navigated += OnNavigated;
            _presenter.EventReceived += OnEventReceived;
            _presenter.NewWindowRequested += OnNewWindowRequested;

            Content = _presenter;

            _initialized = await _presenter.EnsureInitializedAsync();

            if (_closed)
            {
                _presenter.Close();
            }

        Cleanup:
            return !_closed && _presenter is not null;
        }

        private void OnNavigating(object sender, WebViewerNavigatingEventArgs e)
        {
            Navigating?.Invoke(this, e);
        }

        private void OnNavigated(object sender, WebViewerNavigatedEventArgs e)
        {
            Navigated?.Invoke(this, e);
        }

        private void OnEventReceived(object sender, WebViewerEventReceivedEventArgs e)
        {
            EventReceived?.Invoke(this, e);
        }

        private void OnNewWindowRequested(object sender, WebViewerNewWindowRequestedEventArgs e)
        {
            NewWindowRequested?.Invoke(this, e);
        }

        public event EventHandler<WebViewerNavigatingEventArgs> Navigating;

        public event EventHandler<WebViewerNavigatedEventArgs> Navigated;

        public event EventHandler<WebViewerEventReceivedEventArgs> EventReceived;

        public event EventHandler<WebViewerNewWindowRequestedEventArgs> NewWindowRequested;

        public async void Navigate(string uri)
        {
            if (await EnsureInitializedAsync())
            {
                _presenter.Navigate(uri);
            }
        }

        public async void NavigateToString(string htmlContent)
        {
            if (await EnsureInitializedAsync())
            {
                _presenter.NavigateToString(htmlContent);
            }
        }

        public async void InvokeScript(string javaScript)
        {
            if (await EnsureInitializedAsync())
            {
                _ = _presenter.InvokeScriptAsync(javaScript);
            }
        }

        public async Task InvokeScriptAsync(string javaScript)
        {
            if (await EnsureInitializedAsync())
            {
                await _presenter.InvokeScriptAsync(javaScript);
            }
        }

        public async void Reload()
        {
            if (await EnsureInitializedAsync())
            {
                _presenter.Reload();
            }
        }

        public void Close()
        {
            _closed = true;

            _presenter.Close();
            _presenter = null;

            Content = null;
        }
    }

    public abstract class WebPresenter : Control
    {
        private readonly TaskCompletionSource<bool> _templatedApplied = new();

        public Task<bool> EnsureInitializedAsync()
        {
            return _templatedApplied.Task;
        }

        protected void Initialize(bool succeeded)
        {
            _templatedApplied.TrySetResult(succeeded);
        }

        public event EventHandler<WebViewerEventReceivedEventArgs> EventReceived;

        public event EventHandler<WebViewerNavigatingEventArgs> Navigating;

        public event EventHandler<WebViewerNavigatedEventArgs> Navigated;

        public event EventHandler<WebViewerNewWindowRequestedEventArgs> NewWindowRequested;

        public abstract void Navigate(string uri);

        public abstract void NavigateToString(string htmlContent);

        public abstract Task InvokeScriptAsync(string javaScript);

        public abstract void Reload();

        public abstract void Close();

        protected void OnEventReceived(string eventName, JsonObject eventData)
        {
            EventReceived?.Invoke(this, new WebViewerEventReceivedEventArgs(eventName, eventData));
        }

        protected bool OnNavigating(string url)
        {
            var args = new WebViewerNavigatingEventArgs(url);
            Navigating?.Invoke(this, args);
            return args.Cancel;
        }

        protected void OnNavigated(string url)
        {
            Navigated?.Invoke(this, new WebViewerNavigatedEventArgs(url));
        }

        protected bool OnNewWindowRequested(string url)
        {
            var args = new WebViewerNewWindowRequestedEventArgs(url);
            NewWindowRequested?.Invoke(this, args);
            return args.Cancel;
        }
    }

    public partial class EdgeWebPresenter : WebPresenter
    {
        private WebView View;

        public EdgeWebPresenter()
        {
            DefaultStyleKey = typeof(EdgeWebPresenter);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            View = GetTemplateChild(nameof(View)) as WebView;
            View.NavigationStarting += OnNavigationStarting;
            View.NavigationCompleted += OnNavigationCompleted;
            View.Unloaded += OnUnloaded;

            Initialize(true);
        }

        private void OnNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            sender.AddWebAllowedObject("TelegramWebviewProxy", new TelegramWebviewProxy(ReceiveEvent));
            args.Cancel = OnNavigating(args.Uri?.ToString() ?? string.Empty);
        }

        private void OnNavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            OnNavigating(args.Uri?.ToString() ?? string.Empty);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ReceiveEvent(string eventName, string eventData)
        {
            if (JsonObject.TryParse(eventData, out JsonObject data))
            {
                OnEventReceived(eventName, data);
            }
            else
            {
                OnEventReceived(eventName, null);
            }
        }

        public override void Navigate(string uri)
        {
            View?.Navigate(new Uri(uri));
        }

        public override void NavigateToString(string htmlContent)
        {
            View?.NavigateToString(htmlContent);
        }

        public override async Task InvokeScriptAsync(string javaScript)
        {
            try
            {
                await View.InvokeScriptAsync("eval", new[] { javaScript });
            }
            catch
            {
                // This method can throw exceptions if fails to evaluate
            }
        }

        public override void Reload()
        {
            View?.Refresh();
        }

        public override void Close()
        {
            View?.NavigateToString(string.Empty);
        }
    }

    public partial class ChromiumWebPresenter : WebPresenter
    {
        private WebView2 View;

        public ChromiumWebPresenter()
        {
            DefaultStyleKey = typeof(ChromiumWebPresenter);
        }

        public static bool IsSupported()
        {
            try
            {
                if (SettingsService.Current.Diagnostics.ForceEdgeHtml || !ApiInfo.IsDesktop)
                {
                    return false;
                }

                Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00FFFFFF");
                return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
            }
            catch
            {
                return false;
            }
        }

        protected override async void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            View = GetTemplateChild(nameof(View)) as WebView2;
            View.CoreWebView2Initialized += OnCoreWebView2Initialized;
            View.NavigationStarting += OnNavigationStarting;
            View.NavigationCompleted += OnNavigationCompleted;
            View.WebMessageReceived += OnWebMessageReceived;

            await View.EnsureCoreWebView2Async();

            if (View.CoreWebView2 != null)
            {
                View.CoreWebView2.NewWindowRequested += OnNewWindowRequested;

                await View.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"window.external={invoke:s=>window.chrome.webview.postMessage(s)}");
                await View.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
window.TelegramWebviewProxy = {
postEvent: function(eventType, eventData) {
	if (window.external && window.external.invoke) {
		window.external.invoke(JSON.stringify([eventType, eventData]));
	}
}
}");

                View.CoreWebView2.Settings.IsStatusBarEnabled = false;
                View.CoreWebView2.Settings.AreDefaultContextMenusEnabled = SettingsService.Current.Diagnostics.EnableWebViewDevTools;
                View.CoreWebView2.Settings.AreDevToolsEnabled = SettingsService.Current.Diagnostics.EnableWebViewDevTools;

                Initialize(true);
            }
            else
            {
                Initialize(false);
            }
        }

        private void OnNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            args.Cancel = OnNavigating(args.Uri);
        }

        private void OnNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            OnNavigated(sender.CoreWebView2.Source);
        }

        private void OnCoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            if (args.Exception != null)
            {
                Logger.Error(args.Exception.ToString());
            }
        }

        private void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            // TODO: some clear nonsense from WebView2 here:
            // The JSON string received from mini apps (WebViewer.cs)
            // fails to be parsed by JsonArray.TryParse if retrieved using WebMessageAsJson.
            // The JSON string received from HLS player (WebVideoPlayer.xaml.cs)
            // throws a random exception if retrieved using TryGetWebMessageAsString.

            static string TryGetWebMessageAsString(CoreWebView2WebMessageReceivedEventArgs args)
            {
                try
                {
                    return args.TryGetWebMessageAsString();
                }
                catch
                {
                    return string.Empty;
                }
            }

            if (JsonArray.TryParse(TryGetWebMessageAsString(args), out JsonArray message) && message.Count == 2)
            {
                var eventName = message.GetStringAt(0);
                var eventData = message.GetStringAt(1);

                if (JsonObject.TryParse(eventData, out JsonObject data))
                {
                    OnEventReceived(eventName, data);
                }
                else
                {
                    OnEventReceived(eventName, null);
                }
            }
        }

        private void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = OnNewWindowRequested(args.Uri);
        }

        public override void Navigate(string uri)
        {
            View?.CoreWebView2?.Navigate(uri);
        }

        public override void NavigateToString(string htmlContent)
        {
            View?.CoreWebView2?.NavigateToString(htmlContent);
        }

        public override async Task InvokeScriptAsync(string javaScript)
        {
            try
            {
                await View.CoreWebView2.ExecuteScriptAsync(javaScript);
            }
            catch
            {
                // This method can throw exceptions if fails to evaluate
            }
        }

        public override void Reload()
        {
            View?.Reload();
        }

        public override void Close()
        {
            View?.Close();
        }
    }
}
