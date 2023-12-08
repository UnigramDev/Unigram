//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.ViewService;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Settings;
using Telegram.Views;
using Telegram.Views.Payments;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Settings.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Telegram.Common
{
    public class TLNavigationService : NavigationService
    {
        private readonly IClientService _clientService;
        private readonly IPasscodeService _passcodeService;
        private readonly IViewService _viewService;

        private readonly Dictionary<string, AppWindow> _instantWindows = new Dictionary<string, AppWindow>();

        public TLNavigationService(IClientService clientService, IViewService viewService, Frame frame, int session, string id)
            : base(frame, session, id)
        {
            _clientService = clientService;
            _passcodeService = TypeResolver.Current.Passcode;
            _viewService = viewService;
        }

        public IClientService ClientService => _clientService;

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

        public async void ShowLimitReached(PremiumLimitType type)
        {
            await new LimitReachedPopup(this, _clientService, type).ShowQueuedAsync();
        }

        public async void ShowPromo(PremiumSource source = null)
        {
            await ShowPopupAsync(typeof(PromoPopup), source);
        }

        public Task ShowPromoAsync(PremiumSource source = null, ElementTheme requestedTheme = ElementTheme.Default)
        {
            return ShowPopupAsync(typeof(PromoPopup), source, requestedTheme: requestedTheme);
        }

        public async void NavigateToInvoice(MessageViewModel message)
        {
            var parameters = new ViewServiceParams
            {
                Title = message.Content is MessageInvoice invoice && invoice.ReceiptMessageId == 0 ? Strings.PaymentCheckout : Strings.PaymentReceipt,
                Width = 380,
                Height = 580,
                PersistentId = "Payments",
                Content = control =>
                {
                    var nav = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, SessionId, "Payments" + Guid.NewGuid(), false);
                    nav.Navigate(typeof(PaymentFormPage), new InputInvoiceMessage(message.ChatId, message.Id));

                    return BootStrapper.Current.CreateRootElement(nav);

                }
            };

            await _viewService.OpenAsync(parameters);
        }

        public async void NavigateToInvoice(InputInvoice inputInvoice)
        {
            var parameters = new ViewServiceParams
            {
                Title = Strings.PaymentCheckout,
                Width = 380,
                Height = 580,
                PersistentId = "Payments",
                Content = control =>
                {
                    var nav = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, SessionId, "Payments" + Guid.NewGuid(), false);
                    nav.Navigate(typeof(PaymentFormPage), inputInvoice);

                    return BootStrapper.Current.CreateRootElement(nav);

                }
            };

            await _viewService.OpenAsync(parameters);
        }

        public void NavigateToSender(MessageSender sender)
        {
            if (sender is MessageSenderUser user)
            {
                NavigateToUser(user.UserId);
            }
            else if (sender is MessageSenderChat chat)
            {
                Navigate(typeof(ProfilePage), chat.ChatId);
            }
        }

        public async void NavigateToChat(Chat chat, long? message = null, long? thread = null, string accessToken = null, NavigationState state = null, bool scheduled = false, bool force = true, bool createNewWindow = false)
        {
            if (Dispatcher.HasThreadAccess is false)
            {
                Logger.Info(Environment.StackTrace);

                // Throwing here should get the exception to AppCenter early enough to actually have a stack trace.
                throw new InvalidOperationException();
            }

            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                var user = _clientService.GetUser(privata.UserId);
                if (user == null)
                {
                    return;
                }

                if (user.RestrictionReason.Length > 0)
                {
                    await MessagePopup.ShowAsync(user.RestrictionReason, Strings.AppName, Strings.OK);
                    return;
                }
                else if (user.Id == _clientService.Options.AntiSpamBotUserId)
                {
                    var groupInfo = Strings.EventLogFilterGroupInfo;
                    var administrators = Strings.ChannelAdministrators;
                    var path = $"{groupInfo} > {administrators}";

                    var text = string.Format(Strings.ChannelAntiSpamInfo2, path);
                    var index = Strings.ChannelAntiSpamInfo2.IndexOf("{0}");

                    var formatted = new FormattedText(text, new[] { new TextEntity(index, path.Length, new TextEntityTypeTextUrl("tg://")) });

                    await MessagePopup.ShowAsync(formatted, Strings.AppName, Strings.OK);
                    return;
                }
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = _clientService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }

                if (supergroup.Status is ChatMemberStatusLeft && !supergroup.IsPublic() && !_clientService.IsChatAccessible(chat))
                {
                    await MessagePopup.ShowAsync(Strings.ChannelCantOpenPrivate, Strings.AppName, Strings.OK);
                    return;
                }

                if (supergroup.RestrictionReason.Length > 0)
                {
                    await MessagePopup.ShowAsync(supergroup.RestrictionReason, Strings.AppName, Strings.OK);
                    return;
                }
            }

            if (Frame.Content is ChatPage page && page.ViewModel != null && chat.Id.Equals((long)CurrentPageParam) && thread == null && !scheduled && !createNewWindow)
            {
                var viewModel = page.ViewModel;
                if (message != null)
                {
                    await viewModel.LoadMessageSliceAsync(null, message.Value);
                }
                else
                {
                    await viewModel.LoadLastSliceAsync();
                }

                if (viewModel != page.ViewModel)
                {
                    return;
                }

                if (accessToken != null && ClientService.TryGetUser(chat, out User user) && ClientService.TryGetUserFull(chat, out UserFullInfo userFull))
                {
                    page.ViewModel.AccessToken = accessToken;
                    page.View.UpdateUserFullInfo(chat, user, userFull, false, true);
                }

                page.ViewModel.TextField?.Focus(FocusState.Programmatic);

                if (App.DataPackages.TryRemove(chat.Id, out DataPackageView package))
                {
                    await page.ViewModel.HandlePackageAsync(package);
                }

                OverlayWindow.Current?.TryHide(ContentDialogResult.None);
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

                state ??= new NavigationState();

                if (message != null)
                {
                    state["message_id"] = message.Value;
                }

                if (accessToken != null)
                {
                    state["access_token"] = accessToken;
                }

                if (createNewWindow)
                {
                    Type target;
                    object parameter;

                    if (thread != null)
                    {
                        target = typeof(ChatThreadPage);
                        parameter = new ChatNavigationArgs(chat.Id, thread.Value);
                    }
                    else if (scheduled)
                    {
                        target = typeof(ChatScheduledPage);
                        parameter = chat.Id;
                    }
                    else
                    {
                        target = typeof(ChatPage);
                        parameter = chat.Id;
                    }

                    // This is horrible here but I don't want to bloat this method with dozens of parameters.
                    var masterDetailPanel = Window.Current.Content.Descendants<MasterDetailPanel>().FirstOrDefault();
                    if (masterDetailPanel != null)
                    {
                        await OpenAsync(target, parameter, size: new Windows.Foundation.Size(masterDetailPanel.ActualDetailWidth, masterDetailPanel.ActualHeight));
                    }
                    else
                    {
                        await OpenAsync(target, parameter);
                    }
                }
                else
                {
                    if (Frame.Content is ChatPage chatPage && thread == null && !scheduled && !force)
                    {
                        chatPage.ViewModel.NavigatedFrom(null, false);

                        chatPage.Deactivate(true);
                        chatPage.Activate(SessionId);
                        chatPage.ViewModel.NavigationService = this;
                        chatPage.ViewModel.Dispatcher = Dispatcher;
                        await chatPage.ViewModel.NavigatedToAsync(chat.Id, Windows.UI.Xaml.Navigation.NavigationMode.New, state);

                        FrameFacade.RaiseNavigated(chat.Id);

                        OverlayWindow.Current?.TryHide(ContentDialogResult.None);
                    }
                    else
                    {
                        Type target;
                        NavigationTransitionInfo info = null;
                        object parameter;

                        if (thread != null)
                        {
                            target = typeof(ChatThreadPage);
                            parameter = new ChatNavigationArgs(chat.Id, thread.Value);

                            if (CurrentPageType == typeof(ChatPage) && chat.Id.Equals((long)CurrentPageParam))
                            {
                                info = new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight };
                            }
                            else
                            {
                                info = new SuppressNavigationTransitionInfo();
                            }
                        }
                        else if (scheduled)
                        {
                            target = typeof(ChatScheduledPage);
                            parameter = chat.Id;
                        }
                        else
                        {
                            target = typeof(ChatPage);
                            parameter = chat.Id;
                        }

                        Navigate(target, parameter, state, info);
                    }
                }
            }
        }

        public async void NavigateToChat(long chatId, long? message = null, long? thread = null, string accessToken = null, NavigationState state = null, bool scheduled = false, bool force = true, bool createNewWindow = false)
        {
            var chat = _clientService.GetChat(chatId);

            // TODO: this should never happen
            chat ??= await _clientService.SendAsync(new GetChat(chatId)) as Chat;

            if (chat == null)
            {
                return;
            }

            NavigateToChat(chat, message, thread, accessToken, state, scheduled, force, createNewWindow);
        }

        public async void NavigateToUser(long userId)
        {
            if (_clientService.TryGetChatFromUser(userId, out Chat chat))
            {
                Navigate(typeof(ProfilePage), chat.Id);
            }
            else
            {
                var response = await _clientService.SendAsync(new CreatePrivateChat(userId, false));
                if (response is Chat created)
                {
                    Navigate(typeof(ProfilePage), created.Id);
                }
            }
        }

        public async void NavigateToPasscode()
        {
            if (_passcodeService.IsEnabled)
            {
                var popup = new SettingsPasscodeConfirmPopup();

                var confirm = await popup.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    Navigate(typeof(SettingsPasscodePage));
                }
            }
            else
            {
                var popup = new SettingsPasscodePopup();

                var confirm = await popup.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    var viewModel = TypeResolver.Current.Resolve<SettingsPasscodeViewModel>(SessionId);
                    if (viewModel != null && await viewModel.ToggleAsync())
                    {
                        Navigate(typeof(SettingsPasscodePage));
                    }
                }
            }
        }
    }
}
