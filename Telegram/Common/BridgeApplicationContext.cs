//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;

namespace Telegram.Common
{
    public partial class BridgeApplicationContext
    {
        private static AppServiceConnection _connection;
        private static BackgroundTaskDeferral _deferral;

        private static TaskCompletionSource<bool> _connected = new();

        private static readonly DisposableMutex _lock = new();

        public static async Task AddLoopbackExemptionAsync()
        {
            if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            {
                try
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("LoopbackExemptGroup");
                }
                catch
                {
                    // The app has been compiled without desktop bridge
                }
            }
        }

        public static async Task LaunchAsync()
        {
            if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            {
                try
                {
                    _connected = new();
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("SystemTrayGroup");
                }
                catch
                {
                    // The app has been compiled without desktop bridge
                }
            }
        }

        public static async Task ConnectAsync()
        {
            using (await _lock.WaitAsync())
            {
                if (_connection == null)
                {
                    await LaunchAsync();
                }
            }

            await _connected?.Task;
        }

        public static async void Connect(AppServiceConnection connection, IBackgroundTaskInstance task)
        {
            Logger.Info();

            task.Canceled += OnCanceled;

            using (_lock.Wait())
            {
                _connection = connection;
                _connection.RequestReceived += OnRequestReceived;
                _connection.ServiceClosed += OnServiceClosed;

                _deferral = task.GetDeferral();
            }

            var values = new ValueSet
            {
                { "ProcessId", Process.GetCurrentProcess().Id },
                { "OpenText", Strings.NotifyIconOpen },
                { "ExitText", Strings.NotifyIconExit }
            };

            await SendMessageAsync(values);

            _connected?.TrySetResult(true);
        }

        public static Task ExitAsync()
        {
            return SendMessageAsync("Exit");
        }

        public static async void Debug(string value)
        {
            var response = await SendMessageAsync("Debug", value);
            if (response?.Status == AppServiceResponseStatus.Success)
            {
                if (response.Message.TryGet<string>("Debug", out var message))
                {
                    // TODO: XamlRoot here is not needed, WinUI 3 will not have this code
                    await MessagePopup.ShowAsync(null as XamlRoot, message);
                }
            }
        }

        private static readonly Lazy<WebAuthNGetApiVersionNumber> _webAuthNGetApiVersionNumber = new(() => NativeMethodInvoker.GetNativeMethod<WebAuthNGetApiVersionNumber>("webauthn.dll", "WebAuthNGetApiVersionNumber"));

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int WebAuthNGetApiVersionNumber();

        public static bool IsPasskeySupported()
        {
            var method = _webAuthNGetApiVersionNumber.Value;
            if (method != null)
            {
                return method() >= 3;
            }

            return false;
        }

        public static async Task<Object> AddLoginPasskeyAsync(IClientService clientService)
        {
            Logger.Info();
            await ConnectAsync();

            var response = await clientService.SendAsync(new GetPasskeyParameters());
            if (response is not Text parameters)
            {
                return response;
            }

            var message = new ValueSet
            {
                { "MakeCredential", parameters.TextValue },
                { "WindowId", WindowContext.Current.Handle }
            };

            var payload = await SendMessageAsync(message, timeout: 0);
            if (payload?.Status == AppServiceResponseStatus.Success)
            {
                if (payload.Message.TryGet("Result", out int result))
                {
                    if (result >= 0
                        && payload.Message.TryGet("ClientData", out string clientData)
                        && payload.Message.TryGet("AttestationObject", out byte[] attestationObject))
                    {
                        return await clientService.SendAsync(new AddLoginPasskey(clientData, attestationObject));
                    }
                    else
                    {
                        payload.Message.TryGet("Message", out string text);
                        return new Error(result, text ?? string.Empty);
                    }
                }
            }

            return new Error(400, "Unknown error");
        }

        public static async Task<Object> CheckAuthenticationPasskeyAsync(IClientService clientService)
        {
            Logger.Info();
            await ConnectAsync();

            var response = await clientService.SendAsync(new GetAuthenticationPasskeyParameters());
            if (response is not Text parameters)
            {
                return response;
            }

            var message = new ValueSet
            {
                { "GetAssertion", parameters.TextValue },
                { "WindowId", WindowContext.Current.Handle }
            };

            var payload = await SendMessageAsync(message, timeout: 0);
            if (payload?.Status == AppServiceResponseStatus.Success)
            {
                if (payload.Message.TryGet("Result", out int result))
                {
                    if (result >= 0
                        && payload.Message.TryGet("CredentialId", out string credentialId)
                        && payload.Message.TryGet("ClientData", out string clientData)
                        && payload.Message.TryGet("AuthenticatorData", out byte[] authenticatorData)
                        && payload.Message.TryGet("Signature", out byte[] signature)
                        && payload.Message.TryGet("UserHandle", out byte[] userHandle))
                    {
                        return await clientService.SendAsync(new CheckAuthenticationPasskey(credentialId, clientData, authenticatorData, signature, userHandle));
                    }
                    else
                    {
                        payload.Message.TryGet("Message", out string text);
                        return new Error(result, text ?? string.Empty);
                    }
                }
            }

            return new Error(400, "Unknown error");
        }

        public static void LoopbackExempt(bool enabled)
        {
            _ = SendMessageAsync("LoopbackExempt", enabled);
        }

        public static void SendUnreadCount(int unreadCount, int unreadMutedCount)
        {
            _ = SendMessageAsync(new ValueSet { { "UnreadCount", unreadCount }, { "UnreadUnmutedCount", unreadMutedCount } });
        }

        private static Task<AppServiceResponse> SendMessageAsync(string message, object parameter = null, bool reconnect = true, int timeout = 500)
        {
            return SendMessageAsync(new ValueSet { { message, parameter ?? true } }, reconnect, timeout);
        }

        private static async Task<AppServiceResponse> SendMessageAsync(ValueSet message, bool reconnect = true, int timeout = 500)
        {
            try
            {
                AppServiceResponse response = null;
                using (_lock.Wait())
                {
                    var connection = _connection;
                    if (connection == null)
                    {
                        return null;
                    }

                    if (SettingsService.Current.Diagnostics.BridgeDebug)
                    {
                        foreach (var item in message)
                        {
                            Logger.Info(item.Key);
                            break;
                        }
                    }

                    if (timeout != 0)
                    {
                        var task = connection.SendMessageAsync(message).AsTask();
                        var completed = await Task.WhenAny(task, Task.Delay(timeout));

                        if (task == completed)
                        {
                            response = task.Result;
                        }
                    }
                    else
                    {
                        response = await connection.SendMessageAsync(message);
                    }
                }

                if (response?.Status != AppServiceResponseStatus.Success)
                {
                    Logger.Error(response == null ? "Timeout" : response.Status);

                    if (reconnect)
                    {
                        Cancel();
                    }
                }
                else if (SettingsService.Current.Diagnostics.BridgeDebug)
                {
                    Logger.Info("Succeeded");
                }

                return response;
            }
            catch (Exception ex)
            {
                // All the remote procedure calls must be wrapped in a try-catch block

                // ToString not to send to AppCenter
                Logger.Error(ex.ToString());

                if (reconnect)
                {
                    Cancel();
                }
            }

            return null;
        }

        private static async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            if (args.Request.Message.ContainsKey("Exit"))
            {
                await BootStrapper.ConsolidateAsync();
            }
        }

        private static void OnServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            Logger.Debug(args.Status);
            Cancel();
        }

        private static void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Logger.Debug(reason);
            Cancel();
        }

        private static void Cancel()
        {
            if (SettingsService.Current.Diagnostics.BridgeDebug)
            {
                Logger.Info();
            }

            using (_lock.Wait())
            {
                if (_connection != null)
                {
                    _connection.RequestReceived -= OnRequestReceived;
                    _connection.ServiceClosed -= OnServiceClosed;
                    _connection = null;
                }

                _deferral?.Complete();
                _deferral = null;
            }
        }
    }
}
