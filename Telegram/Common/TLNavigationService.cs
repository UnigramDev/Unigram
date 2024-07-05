//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.ViewService;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Payments;
using Telegram.ViewModels.Settings;
using Telegram.Views;
using Telegram.Views.Payments;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Settings.Password;
using Telegram.Views.Settings.Popups;
using Telegram.Views.Stars.Popups;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

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

        public async void NavigateToWebApp(User botUser, string url, long launchId = 0, AttachmentMenuBot menuBot = null, Chat sourceChat = null)
        {
            await OpenAsync(new ViewServiceParams
            {
                Width = 384,
                Height = 640,
                PersistedId = "WebApp",
                Content = control => new WebAppPage(ClientService, botUser, url, launchId, menuBot, sourceChat)
            });
        }

        public async void NavigateToInstant(string url, string fallbackUrl = null)
        {
            var response1 = await ClientService.SendAsync(new GetWebPageInstantView(url, true));
            var response2 = await ClientService.SendAsync(new GetLinkPreview(new FormattedText(url, Array.Empty<TextEntity>()), null));

            if (response1 is WebPageInstantView instantView)
            {
                TabViewItem CreateTabViewItem()
                {
                    var frame = new Frame();
                    var service = new TLNavigationService(ClientService, null, frame, ClientService.SessionId, "InstantView"); // BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, frame, _clientService.SessionId, "ciccio", false);

                    service.Navigate(typeof(InstantPage), new InstantPageArgs(instantView, url));

                    var tabViewItem = new TabViewItem
                    {
                        Header = "Test",
                        Content = frame
                    };

                    if (response2 is LinkPreview linkPreview)
                    {
                        var thumbnail = linkPreview.GetMinithumbnail();
                        if (thumbnail != null)
                        {
                            double ratioX = (double)20 / thumbnail.Width;
                            double ratioY = (double)20 / thumbnail.Height;
                            double ratio = Math.Max(ratioX, ratioY);

                            var width = (int)(thumbnail.Width * ratio);
                            var height = (int)(thumbnail.Height * ratio);

                            var bitmap = new BitmapImage
                            {
                                DecodePixelWidth = width,
                                DecodePixelHeight = height,
                                DecodePixelType = DecodePixelType.Logical
                            };

                            using (var stream = new InMemoryRandomAccessStream())
                            {
                                try
                                {
                                    PlaceholderImageHelper.WriteBytes(thumbnail.Data, stream);
                                    _ = bitmap.SetSourceAsync(stream);
                                }
                                catch
                                {
                                    // Throws when the data is not a valid encoded image,
                                    // not so frequent, but if it happens during ContainerContentChanging it crashes the app.
                                }
                            }

                            tabViewItem.IconSource = new Microsoft.UI.Xaml.Controls.ImageIconSource
                            {
                                ImageSource = bitmap,
                            };
                        }
                    }

                    if (service.Content is Page page)
                    {
                        tabViewItem.SetBinding(TabViewItem.HeaderProperty, new Binding
                        {
                            Path = new PropertyPath("Title"),
                            Source = page.DataContext
                        });
                    }

                    return tabViewItem;
                }

                var oldViewId = WindowContext.Current.Id;

                var already = WindowContext.All.FirstOrDefault(x => x.PersistedId == "InstantView");
                if (already != null)
                {
                    await already.Dispatcher.DispatchAsync(async () =>
                    {
                        if (WindowContext.Current.Content is TabView tabView)
                        {
                            tabView.TabItems.Insert(tabView.SelectedIndex + 1, CreateTabViewItem());
                            tabView.SelectedIndex++;
                        }

                        await ApplicationViewSwitcher.SwitchAsync(WindowContext.Current.Id, oldViewId);
                    });
                }
                else
                {
                    await OpenAsync(new ViewServiceParams
                    {
                        Width = 820,
                        Height = 640,
                        PersistedId = "InstantView",
                        Content = control =>
                        {
                            var footer = new Border
                            {
                                Background = new SolidColorBrush(Colors.Transparent)
                            };

                            var header = new Border
                            {
                                Background = new SolidColorBrush(Colors.Transparent)
                            };

                            var tabView = new TabView
                            {
                                TabStripHeader = header,
                                TabStripFooter = footer,
                                IsAddTabButtonVisible = false
                            };

                            tabView.TabItems.Add(CreateTabViewItem());
                            tabView.TabCloseRequested += (s, args) =>
                            {
                                if (s.TabItems.Count > 1)
                                {
                                    s.TabItems.Remove(args.Tab);
                                }
                                else
                                {
                                    _ = WindowContext.Current.ConsolidateAsync();
                                }
                            };

                            Window.Current.SetTitleBar(footer);
                            BackdropMaterial.SetApplyToRootOrPageBackground(tabView, true);

                            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
                            coreTitleBar.ExtendViewIntoTitleBar = true;

                            coreTitleBar.LayoutMetricsChanged += (s, args) =>
                            {
                                // To ensure that the tabs in the titlebar are not occluded by shell
                                // content, we must ensure that we account for left and right overlays.
                                // In LTR layouts, the right inset includes the caption buttons and the
                                // drag region, which is flipped in RTL. 

                                // The SystemOverlayLeftInset and SystemOverlayRightInset values are
                                // in terms of physical left and right. Therefore, we need to flip
                                // then when our flow direction is RTL.
                                if (tabView.FlowDirection == FlowDirection.LeftToRight)
                                {
                                    footer.MinWidth = s.SystemOverlayRightInset;
                                    header.MinWidth = s.SystemOverlayLeftInset;
                                }
                                else
                                {
                                    footer.MinWidth = s.SystemOverlayLeftInset;
                                    header.MinWidth = s.SystemOverlayRightInset;
                                }

                                // Ensure that the height of the custom regions are the same as the titlebar.
                                footer.Height = header.Height = s.Height;
                            };

                            return tabView;
                        }
                    });
                }
            }

            //var response = await ClientService.SendAsync(new GetWebPageInstantView(url, true));
            //if (response is WebPageInstantView instantView)
            //{
            //    Navigate(typeof(InstantPage), new InstantPageArgs(instantView, url));
            //}
            //else
            //{
            //    if (Uri.TryCreate(fallbackUrl ?? url, UriKind.Absolute, out Uri uri))
            //    {
            //        await Windows.System.Launcher.LaunchUriAsync(uri);
            //    }
            //}
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

        public void NavigateToInvoice(MessageViewModel message)
        {
            NavigateToInvoice(new InputInvoiceMessage(message.ChatId, message.Id));
        }

        public async void NavigateToInvoice(InputInvoice inputInvoice)
        {
            var response = await ClientService.SendAsync(new GetPaymentForm(inputInvoice, Theme.Current.Parameters));
            if (response is not PaymentForm paymentForm)
            {
                ToastPopup.Show(Strings.PaymentInvoiceLinkInvalid, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                return;
            }

            // TODO: how can we do this while coming from a mini app?
            if (paymentForm.Type is PaymentFormTypeStars)
            {
                await ShowPopupAsync(typeof(PayPopup), new PaymentFormArgs(inputInvoice, paymentForm));
                return;
            }

            var parameters = new ViewServiceParams
            {
                Title = Strings.PaymentCheckout,
                Width = 380,
                Height = 580,
                PersistedId = "Payments",
                Content = control =>
                {
                    var nav = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, SessionId, "Payments" + Guid.NewGuid(), false);
                    nav.Navigate(typeof(PaymentFormPage), new PaymentFormArgs(inputInvoice, paymentForm));

                    return BootStrapper.Current.CreateRootElement(nav);

                }
            };

            await _viewService.OpenAsync(parameters);
        }

        public async void NavigateToReceipt(MessageViewModel message)
        {
            var response = await ClientService.SendAsync(new GetPaymentReceipt(message.ChatId, message.Id));
            if (response is not PaymentReceipt paymentReceipt)
            {
                ToastPopup.Show(Strings.PaymentInvoiceLinkInvalid, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                return;
            }

            // TODO: how can we do this while coming from a mini app?
            if (paymentReceipt.Type is PaymentReceiptTypeStars)
            {
                await ShowPopupAsync(new ReceiptPopup(message.ClientService, this, paymentReceipt));
                return;
            }

            var parameters = new ViewServiceParams
            {
                Title = Strings.PaymentCheckout,
                Width = 380,
                Height = 580,
                PersistedId = "Payments",
                Content = control =>
                {
                    var nav = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, SessionId, "Payments" + Guid.NewGuid(), false);
                    nav.Navigate(typeof(PaymentFormPage), paymentReceipt);

                    return BootStrapper.Current.CreateRootElement(nav);

                }
            };

            await _viewService.OpenAsync(parameters);
        }

        public void NavigateToSender(MessageSender sender)
        {
            if (sender is MessageSenderUser user)
            {
                NavigateToUser(user.UserId, false);
            }
            else if (sender is MessageSenderChat chat)
            {
                Navigate(typeof(ProfilePage), chat.ChatId);
            }
        }

        public async void NavigateToChat(Chat chat, long? message = null, long thread = 0, long savedMessagesTopicId = 0, string accessToken = null, NavigationState state = null, bool scheduled = false, bool force = true, bool createNewWindow = false, bool clearBackStack = false)
        {
            if (Dispatcher.HasThreadAccess is false)
            {
                // This should not happen but it currently does when scheduling a file
                Logger.Error(Environment.StackTrace);

                Dispatcher.Dispatch(() => NavigateToChat(chat, message, thread, savedMessagesTopicId, accessToken, state, scheduled, force, createNewWindow, clearBackStack));
                return;
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

                if (user.Id == _clientService.Options.MyId && chat.ViewAsTopics && savedMessagesTopicId == 0)
                {
                    Navigate(typeof(ProfilePage), chat.Id, infoOverride: new SuppressNavigationTransitionInfo());
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

            // TODO: do current page matching for ChatSavedPage and ChatThreadPage as well.
            if (Frame.Content is ChatPage page && page.ViewModel != null && chat.Id.Equals((long)CurrentPageParam) && thread == 0 && savedMessagesTopicId == 0 && !scheduled && !createNewWindow)
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

                if (App.DataPackages.TryRemove(chat.Id, out DataPackageView package1))
                {
                    await page.ViewModel.HandlePackageAsync(package1);
                }
                else if (state != null && state.TryGet("package", out DataPackageView package2))
                {
                    await page.ViewModel.HandlePackageAsync(package2);
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

                    if (thread != 0)
                    {
                        target = typeof(ChatThreadPage);
                        parameter = new ChatMessageIdNavigationArgs(chat.Id, thread);
                    }
                    else if (savedMessagesTopicId != 0)
                    {
                        target = typeof(ChatSavedPage);
                        parameter = new ChatSavedMessagesTopicIdNavigationArgs(chat.Id, savedMessagesTopicId);
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
                    var masterDetailPanel = Window.Current.Content.GetChild<MasterDetailPanel>();
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
                    // TODO: do current page matching for ChatSavedPage and ChatThreadPage as well.
                    if (Frame.Content is ChatPage chatPage && thread == 0 && savedMessagesTopicId == 0 && !scheduled && !force)
                    {
                        chatPage.ViewModel.NavigatedFrom(null, false);

                        chatPage.Deactivate(true);
                        chatPage.Activate(this);
                        chatPage.ViewModel.NavigationService = this;
                        chatPage.ViewModel.Dispatcher = Dispatcher;
                        await chatPage.ViewModel.NavigatedToAsync(chat.Id, Windows.UI.Xaml.Navigation.NavigationMode.New, state);

                        FrameFacade.RaiseNavigated(chat.Id);
                        Frame.ForwardStack.Clear();

                        if (clearBackStack)
                        {
                            GoBackAt(0, false);
                        }

                        OverlayWindow.Current?.TryHide(ContentDialogResult.None);
                    }
                    else
                    {
                        Type target;
                        NavigationTransitionInfo info = null;
                        object parameter;

                        if (thread != 0)
                        {
                            target = typeof(ChatThreadPage);
                            parameter = new ChatMessageIdNavigationArgs(chat.Id, thread);

                            if (CurrentPageType == typeof(ChatPage) && chat.Id.Equals((long)CurrentPageParam))
                            {
                                info = new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight };
                            }
                            else
                            {
                                info = new SuppressNavigationTransitionInfo();
                            }
                        }
                        else if (savedMessagesTopicId != 0)
                        {
                            target = typeof(ChatSavedPage);
                            parameter = new ChatSavedMessagesTopicIdNavigationArgs(chat.Id, savedMessagesTopicId);

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

                            if (CurrentPageType == typeof(ProfilePage) && CurrentPageParam is long profileId && profileId == chat.Id)
                            {
                                var cacheKey = Guid.NewGuid().ToString();
                                var chatId = (long)parameter;

                                parameter = cacheKey;
                                CacheKeyToChatId[cacheKey] = chatId;

                                GoBackAt(0, false);

                                Frame.BackStack.Add(new Windows.UI.Xaml.Navigation.PageStackEntry(target, parameter, null));
                                GoBack(state, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
                                Frame.ForwardStack.Clear();
                                return;
                            }
                        }

                        if (Navigate(target, parameter, state, info))
                        {
                            if (clearBackStack)
                            {
                                GoBackAt(0, false);
                            }
                        }
                    }
                }
            }
        }

        public async void NavigateToChat(long chatId, long? message = null, long thread = 0, long savedMessagesTopicId = 0, string accessToken = null, NavigationState state = null, bool scheduled = false, bool force = true, bool createNewWindow = false)
        {
            var chat = _clientService.GetChat(chatId);

            // TODO: this should never happen
            chat ??= await _clientService.SendAsync(new GetChat(chatId)) as Chat;

            if (chat == null)
            {
                return;
            }

            NavigateToChat(chat, message, thread, savedMessagesTopicId, accessToken, state, scheduled, force, createNewWindow);
        }

        public async void NavigateToUser(long userId, bool toChat = false)
        {
            if (_clientService.TryGetChatFromUser(userId, out Chat chat))
            {
                var user = ClientService.GetUser(userId);
                if (user?.Type is UserTypeBot || toChat)
                {
                    NavigateToChat(chat);
                }
                else
                {
                    Navigate(typeof(ProfilePage), chat.Id);
                }
            }
            else
            {
                var response = await _clientService.SendAsync(new CreatePrivateChat(userId, false));
                if (response is Chat created)
                {
                    var user = ClientService.GetUser(userId);
                    if (user?.Type is UserTypeBot || toChat)
                    {
                        NavigateToChat(created);
                    }
                    else
                    {
                        Navigate(typeof(ProfilePage), created.Id);
                    }
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

        public async Task<PasswordState> NavigateToPasswordAsync()
        {
            var intro = new SettingsPasswordIntroPopup();

            if (ContentDialogResult.Primary != await intro.ShowQueuedAsync())
            {
                return null;
            }

            var password = new SettingsPasswordCreatePopup();

            if (ContentDialogResult.Primary != await password.ShowQueuedAsync())
            {
                return null;
            }

            var hint = new SettingsPasswordHintPopup(null, null, password.Password);

            if (ContentDialogResult.Primary != await hint.ShowQueuedAsync())
            {
                return null;
            }

            var emailAddress = new SettingsPasswordEmailAddressPopup(ClientService, new SetPassword(string.Empty, password.Password, hint.Hint, true, string.Empty));

            if (ContentDialogResult.Primary != await emailAddress.ShowQueuedAsync())
            {
                return null;
            }

            PasswordState passwordState;

            if (emailAddress.PasswordState?.RecoveryEmailAddressCodeInfo != null)
            {
                var emailCode = new SettingsPasswordEmailCodePopup(ClientService, emailAddress.PasswordState?.RecoveryEmailAddressCodeInfo, SettingsPasswordEmailCodeType.New);

                if (ContentDialogResult.Primary != await emailCode.ShowQueuedAsync())
                {
                    return null;
                }

                passwordState = emailCode.PasswordState;
            }
            else
            {
                passwordState = emailAddress.PasswordState;
            }

            await new SettingsPasswordDonePopup().ShowQueuedAsync();
            return passwordState;
        }
    }
}
