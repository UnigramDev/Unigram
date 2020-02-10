using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Services.NavigationService;
using Template10.Services.ViewService;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Settings;
#if INCLUDE_WALLET
using Unigram.Views.Wallet;
#endif
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.Security.Credentials;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Common
{
    public class TLNavigationService : NavigationService
    {
        private readonly IProtoService _protoService;
        private readonly IPasscodeService _passcodeService;

        private ViewLifetimeControl _walletLifetime;

        private Dictionary<string, AppWindow> _instantWindows = new Dictionary<string, AppWindow>();
        private AppWindow _walletWindow;

        public TLNavigationService(IProtoService protoService, Frame frame, int session, string id)
            : base(frame, session, id)
        {
            _protoService = protoService;
            _passcodeService = TLContainer.Current.Passcode;
        }

        public int SessionId => _protoService.SessionId;
        public IProtoService ProtoService => _protoService;

        public async void NavigateToInstant(string url)
        {
            //if (ApiInformation.IsTypePresent("Windows.UI.WindowManagement.AppWindow"))
            //{
            //    _instantWindows.TryGetValue(url, out AppWindow window);
            //    if (window == null)
            //    {
            //        var nav = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, BootStrapper.ExistingContent.Exclude, 0, "0", false);
            //        var frame = BootStrapper.Current.CreateRootElement(nav);
            //        nav.Navigate(typeof(InstantPage), url);

            //        window = await AppWindow.TryCreateAsync();
            //        window.PersistedStateId = "InstantView";
            //        window.TitleBar.ExtendsContentIntoTitleBar = true;
            //        window.Closed += (s, args) =>
            //        {
            //            _instantWindows.Remove(url);
            //            frame = null;
            //            window = null;
            //        };

            //        _instantWindows[url] = window;
            //        ElementCompositionPreview.SetAppWindowContent(window, frame);
            //    }

            //    await window.TryShowAsync();
            //    window.RequestMoveAdjacentToCurrentView();
            //}
            //else
            {
                Navigate(typeof(InstantPage), url);
            }
        }

        public async void NavigateToWallet(string address = null)
        {
#if INCLUDE_WALLET
            Type page;
            if (_protoService.Options.WalletPublicKey != null)
            {
                try
                {
                    var vault = new PasswordVault();
                    var credential = vault.Retrieve($"{_protoService.SessionId}", _protoService.Options.WalletPublicKey);
                }
                catch
                {
                    // Credentials for this account wallet don't exist anymore.
                    _protoService.Options.WalletPublicKey = null;
                }
            }

            if (_protoService.Options.WalletPublicKey != null)
            {
                if (address == null)
                {
                    page = typeof(WalletPage);
                }
                else
                {
                    page = typeof(WalletSendPage);
                }
            }
            else
            {
                page = typeof(WalletCreatePage);
            }

            //page = typeof(WalletCreatePage);

            //if (ApiInformation.IsTypePresent("Windows.UI.WindowManagement.AppWindow"))
            //{
            //    var window = _walletWindow;
            //    if (window == null)
            //    {
            //        var nav = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, BootStrapper.ExistingContent.Exclude, 0, "0", false);
            //        var frame = BootStrapper.Current.CreateRootElement(nav);
            //        nav.Navigate(page, address);

            //        window = await AppWindow.TryCreateAsync();
            //        window.PersistedStateId = "Wallet";
            //        window.TitleBar.ExtendsContentIntoTitleBar = true;
            //        window.Closed += (s, args) =>
            //        {
            //            _walletWindow = null;
            //            frame = null;
            //            window = null;
            //        };

            //        _walletWindow = window;
            //        ElementCompositionPreview.SetAppWindowContent(window, frame);
            //    }

            //    window.RequestSize(new Windows.Foundation.Size(360, 640));
            //    await window.TryShowAsync();
            //    window.RequestSize(new Windows.Foundation.Size(360, 640));
            //    window.RequestMoveAdjacentToCurrentView();
            //}

            //return;

            if (_walletLifetime == null)
            {
                _walletLifetime = await OpenAsync(page, address);
                _walletLifetime.Released += (s, args) =>
                {
                    _walletLifetime = null;
                };

                if (_walletLifetime.NavigationService is NavigationService service)
                {
                    service.SerializationService = TLSerializationService.Current;
                }
            }
            else
            {
                await _walletLifetime.CoreDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    _walletLifetime.NavigationService.Navigate(page, address);
                });

                await ApplicationViewSwitcher.TryShowAsStandaloneAsync(_walletLifetime.Id, ViewSizePreference.Default, ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow), ViewSizePreference.UseHalf);
            }
#endif
        }

        public async void NavigateToChat(Chat chat, long? message = null, string accessToken = null, IDictionary<string, object> state = null, bool scheduled = false)
        {
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                var user = _protoService.GetUser(privata.UserId);
                if (user == null)
                {
                    return;
                }

                var reason = user.GetRestrictionReason();
                if (reason != null && reason.Length > 0)
                {
                    await TLMessageDialog.ShowAsync(reason, Strings.Resources.AppName, Strings.Resources.OK);
                    return;
                }
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = _protoService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }

                if (supergroup.Status is ChatMemberStatusLeft && string.IsNullOrEmpty(supergroup.Username) && !supergroup.HasLocation)
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.ChannelCantOpenPrivate, Strings.Resources.AppName, Strings.Resources.OK);
                    return;
                }

                var reason = supergroup.GetRestrictionReason();
                if (reason != null && reason.Length > 0)
                {
                    await TLMessageDialog.ShowAsync(reason, Strings.Resources.AppName, Strings.Resources.OK);
                    return;
                }
            }

            if (Frame.Content is ChatPage page && chat.Id.Equals((long)CurrentPageParam) && !scheduled)
            {
                if (message != null)
                {
                    await page.ViewModel.LoadMessageSliceAsync(null, message.Value);
                }
                else
                {
                    await page.ViewModel.LoadMessageSliceAsync(null, chat.LastMessage?.Id ?? long.MaxValue, VerticalAlignment.Bottom, 8);
                }

                page.ViewModel.TextField?.Focus(FocusState.Programmatic);

                if (App.DataPackages.TryRemove(chat.Id, out DataPackageView package))
                {
                    await page.ViewModel.HandlePackageAsync(package);
                }
            }
            else
            {
                //NavigatedEventHandler handler = null;
                //handler = async (s, args) =>
                //{
                //    Frame.Navigated -= handler;

                //    if (args.Content is DialogPage page1 /*&& chat.Id.Equals((long)args.Parameter)*/)
                //    {
                //        if (message.HasValue)
                //        {
                //            await page1.ViewModel.LoadMessageSliceAsync(null, message.Value);
                //        }
                //    }
                //};

                //Frame.Navigated += handler;

                if (message != null || accessToken != null)
                {
                    state = state ?? new Dictionary<string, object>();

                    if (message != null)
                    {
                        state["message_id"] = message.Value;
                    }

                    if (accessToken != null)
                    {
                        state["access_token"] = accessToken;
                    }
                }

                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
                if (shift && !ctrl)
                {
                    await OpenAsync(scheduled ? typeof(ChatScheduledPage) : typeof(ChatPage), chat.Id);
                }
                else
                {
                    await NavigateAsync(scheduled ? typeof(ChatScheduledPage) : typeof(ChatPage), chat.Id, state);
                }
            }
        }

        public async void NavigateToChat(long chatId, long? message = null, string accessToken = null, IDictionary<string, object> state = null, bool scheduled = false)
        {
            var chat = _protoService.GetChat(chatId);
            if (chat == null)
            {
                var response = await _protoService.SendAsync(new GetChat(chatId));
                if (response is Chat result)
                {
                    chat = result;
                }
            }

            if (chat == null)
            {
                return;
            }

            NavigateToChat(chat, message, accessToken, state, scheduled);
        }

        public async void NavigateToPasscode()
        {
            if (_passcodeService.IsEnabled)
            {
                var dialog = new SettingsPasscodeConfirmView(passcode => Task.FromResult(!_passcodeService.Check(passcode)), _passcodeService.IsSimple);
                dialog.IsSimple = _passcodeService.IsSimple;

                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    Navigate(typeof(SettingsPasscodePage));
                }
            }
            else
            {
                Navigate(typeof(SettingsPasscodePage));
            }
        }
    }
}
