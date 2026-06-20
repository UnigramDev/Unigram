//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Payments;
using Telegram.ViewModels.Settings;
using Telegram.Views;
using Telegram.Views.Host;
using Telegram.Views.Payments;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Settings.Password;
using Telegram.Views.Settings.Popups;
using Telegram.Views.Stars.Popups;
using Telegram.Views.Tabbed;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Telegram.Common
{
    public enum SensitiveContentSource
    {
        Chat,
        Message,
        Settings
    }

    public partial class TLNavigationService : NavigationService
    {
        private readonly IClientService _clientService;
        private readonly IPasscodeService _passcodeService;
        private readonly IViewService _viewService;

        public TLNavigationService(ISession session, WindowContext window, Frame frame, string id)
            : base(session, window, frame, id)
        {
            _clientService = session.Resolve<IClientService>();
            _passcodeService = LifetimeService.Current.Passcode;
            _viewService = session.Resolve<IViewService>();
        }

        public IClientService ClientService => _clientService;

        public async void NavigateToWebApp(User botUser, WebAppUrl url, long launchId = 0, AttachmentMenuBot menuBot = null, WebAppOpenMode openMode = null, OpenUrlSource source = null, InternalLinkType sourceLink = null, string buttonText = null)
        {
            if (sourceLink != null)
            {
                var oldViewId = Window.Id;
                var found = false;

                await WindowContext.ForEachAsync(window =>
                {
                    if (window.Content is WebAppPage webApp && webApp.AreTheSame(sourceLink))
                    {
                        _ = ApplicationViewSwitcher.SwitchAsync(WindowContext.Current.Id, oldViewId);
                        found = true;
                    }
                });

                if (found)
                {
                    return;
                }
            }

            await OpenAsync(new ViewServiceOptions
            {
                Width = 384,
                Height = 640,
                PersistedId = "WebApp",
                ViewMode = openMode is WebAppOpenModeFullScreen ? ViewServiceMode.FullScreen : ViewServiceMode.Default,
                Content = control => new WebAppPage(ClientService, this, botUser, url, launchId, menuBot, source, sourceLink, buttonText)
            });
        }

        public async void NavigateToWebApp(User botUser, string url, string title, long gameChatId = 0, long gameMessageId = 0)
        {
            await OpenAsync(new ViewServiceOptions
            {
                Width = 384,
                Height = 640,
                PersistedId = "WebApp",
                Content = control => new WebAppPage(ClientService, this, botUser, url, title, gameChatId, gameMessageId)
            });
        }

        public async void NavigateToInstant(string url, string fallbackUrl = null)
        {
            var response = await ClientService.SendAsync(new GetWebPageInstantView(url, false));
            if (response is WebPageInstantView instantView)
            {
                NavigateToInstant(instantView, url);
            }
            else
            {
                if (Uri.TryCreate(fallbackUrl ?? url, UriKind.Absolute, out Uri uri))
                {
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }
            }
        }

        public void NavigateToInstant(WebPageInstantView instantView, string url)
        {
            TabViewItem CreateTabViewItem(WindowContext window)
            {
                var frame = new Frame();
                var service = new TLNavigationService(Session, window, frame, "InstantView"); // BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, frame, _clientService.SessionId, "ciccio", false);

                service.Navigate(typeof(InstantPage), new InstantPageArgs(instantView, url));

                var tabViewItem = new TabViewItem
                {
                    Header = "Test",
                    Content = frame,
                    IconSource = new Microsoft.UI.Xaml.Controls.FontIconSource
                    {
                        Glyph = "\uE60E",
                        FontFamily = BootStrapper.Current.Resources["SymbolThemeFontFamily"] as FontFamily
                    }
                };

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

            NavigateToTab(CreateTabViewItem, new ViewServiceOptions
            {
                Width = 820,
                Height = 640,
                PersistedId = "WebBrowser"
            });
        }

        public void NavigateToWeb3(string url)
        {
            NavigateToTab(window => WebBrowserPage.Create(ClientService, url), new ViewServiceOptions
            {
                Width = 820,
                Height = 640,
                PersistedId = "WebBrowser"
            });
        }

        private async void NavigateToTab(Func<WindowContext, TabViewItem> newTab, ViewServiceOptions parameters)
        {
            var already = WindowContext.All.FirstOrDefault(x => x.PersistedId == parameters.PersistedId);
            if (already != null)
            {
                var oldViewId = Window.Id;

                await already.Dispatcher.DispatchAsync(() =>
                {
                    if (WindowContext.Current.Content is TabbedPage page)
                    {
                        page.AddNewTab(newTab(already));
                    }

                    return ApplicationViewSwitcher.SwitchAsync(WindowContext.Current.Id, oldViewId);
                });
            }
            else
            {
                await OpenAsync(new ViewServiceOptions
                {
                    Width = parameters.Width,
                    Height = parameters.Height,
                    PersistedId = parameters.PersistedId,
                    Content = control => new TabbedPage(newTab(WindowContext.Current), string.Equals(parameters.PersistedId, "WebApps"))
                });
            }
        }

        public void ShowLimitReached(PremiumLimitType type)
        {
            ShowPopup(new LimitReachedPopup(this, _clientService, type));
        }

        public void ShowPromo(PremiumSource source = null)
        {
            ShowPopup(new PromoPopup(), source);
        }

        public Task ShowPromoAsync(PremiumSource source = null, ElementTheme requestedTheme = ElementTheme.Default)
        {
            return ShowPopupAsync(new PromoPopup(), source, requestedTheme: requestedTheme);
        }

        public async void ShowPromo(PremiumFeature feature, PremiumSource source = null)
        {
            PremiumSource premiumSource = new PremiumSourceFeature(feature);

            var features = await ClientService.SendAsync(new GetPremiumFeatures(premiumSource)) as PremiumFeatures;
            if (features == null)
            {
                return;
            }

            var appIcons = features.Features.FirstOrDefault(x => x is PremiumFeatureAppIcons);
            if (appIcons != null)
            {
                features.Features.Remove(appIcons);
            }

            var archivedChats = features.Limits.FirstOrDefault(x => x.Type is PremiumLimitTypePinnedArchivedChatCount);
            if (archivedChats != null)
            {
                features.Limits.Remove(archivedChats);
            }

            features.Limits.Add(new PremiumLimit(new PremiumLimitTypeConnectedAccounts(), 3, 4));

            var state = await ClientService.SendAsync(new GetPremiumState()) as PremiumState;
            if (state == null)
            {
                return;
            }

            var option = state.PaymentOptions.LastOrDefault();

            var animations = state.Animations
                .DistinctBy(x => x.Feature.GetType())
                .ToDictionary(x => x.Feature.GetType(), y => y.Animation);

            var stickers = await ClientService.SendAsync(new GetPremiumStickerExamples()) as Stickers;

            var businessFeatures = await ClientService.SendAsync(new GetBusinessFeatures(null)) as BusinessFeatures;
            if (businessFeatures == null)
            {
                return;
            }

            feature = features.Features.FirstOrDefault(x => x.GetType() == feature.GetType());

            var popup = new FeaturesPopup(ClientService, option?.PaymentOption, features.Features, businessFeatures.Features, features.Limits, animations, stickers, feature);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                ShowPromo(source ?? premiumSource);
            }
        }

        public void NavigateToInvoice(MessageViewModel message)
        {
            NavigateToInvoice(new InputInvoiceMessage(message.ChatId, message.Id), message.Content);
        }

        public async void NavigateToInvoice(InputInvoice inputInvoice, MessageContent content)
        {
            var response = await ClientService.SendAsync(new GetPaymentForm(inputInvoice, Theme.Current.Parameters));
            if (response is not PaymentForm paymentForm)
            {
                ShowToast(Strings.PaymentInvoiceLinkInvalid, ToastPopupIcon.Info);
                return;
            }

            // TODO: how can we do this while coming from a mini app?
            if (paymentForm.Type is PaymentFormTypeStars)
            {
                await ShowPopupAsync(new PayPopup(), new PaymentFormArgs(inputInvoice, paymentForm, content));
                return;
            }

            var parameters = new ViewServiceOptions
            {
                Title = Strings.PaymentCheckout,
                Width = 380,
                Height = 580,
                PersistedId = "Payments",
                Content = control =>
                {
                    // TODO: WinUI - control will be replaced by WindowContext.
                    var nav = BootStrapper.Current.NavigationServiceFactory(Session, WindowContext.Current, BootStrapper.BackButton.Ignore, "Payments" + Guid.NewGuid(), false);
                    nav.Navigate(typeof(PaymentFormPage), new PaymentFormArgs(inputInvoice, paymentForm, content));

                    return nav.Frame;

                }
            };

            await _viewService.OpenAsync(parameters);
        }

        public async void NavigateToReceipt(MessageViewModel message)
        {
            var response = await ClientService.SendAsync(new GetPaymentReceipt(message.ChatId, message.Id));
            if (response is not PaymentReceipt paymentReceipt)
            {
                ShowToast(Strings.PaymentInvoiceLinkInvalid, ToastPopupIcon.Info);
                return;
            }

            // TODO: how can we do this while coming from a mini app?
            if (paymentReceipt.Type is PaymentReceiptTypeStars)
            {
                await ShowPopupAsync(new ReceiptPopup(message.ClientService, this, paymentReceipt));
                return;
            }

            var parameters = new ViewServiceOptions
            {
                Title = Strings.PaymentCheckout,
                Width = 380,
                Height = 580,
                PersistedId = "Payments",
                Content = control =>
                {
                    var nav = BootStrapper.Current.NavigationServiceFactory(Session, WindowContext.Current, BootStrapper.BackButton.Ignore, "Payments" + Guid.NewGuid(), false);
                    nav.Navigate(typeof(PaymentFormPage), paymentReceipt);

                    return nav.Frame;

                }
            };

            await _viewService.OpenAsync(parameters);
        }

        public void NavigateToSender(MessageSender sender, NavigationState state = null, NavigationTransitionInfo infoOverride = null)
        {
            if (sender is MessageSenderUser user)
            {
                NavigateToUser(user.UserId, false, state: state, infoOverride: infoOverride);
            }
            else if (sender is MessageSenderChat chat)
            {
                Navigate(typeof(ProfilePage), chat.ChatId, state: state, infoOverride: infoOverride);
            }
        }

        public async void NavigateToChat(Chat chat, long? message = null, MessageTopic topic = null, string accessToken = null, NavigationState state = null, bool scheduled = false, bool force = true, bool createNewWindow = false, bool clearBackStack = false)
        {
            if (Dispatcher.HasThreadAccess is false)
            {
                // This should not happen but it currently does when scheduling a file
                Logger.Error(Environment.StackTrace);

                Dispatcher.Dispatch(() => NavigateToChat(chat, message, topic, accessToken, state, scheduled, force, createNewWindow, clearBackStack));
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

                if (user.Id == _clientService.Options.MyId && chat.ViewAsTopics && topic == null)
                {
                    Navigate(typeof(ProfilePage), new ChatMessageTopic(chat.Id, null), infoOverride: new SuppressNavigationTransitionInfo());
                    return;
                }

                if (user.RestrictionInfo != null && await ShowRestrictionInfoAsync(user.RestrictionInfo, false))
                {
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

                    await ShowPopupAsync(formatted, Strings.AppName, Strings.OK);
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

                if (supergroup.Status is ChatMemberStatusLeft && !supergroup.IsDirectMessagesGroup && !supergroup.IsPublic() && !_clientService.IsChatAccessible(chat))
                {
                    await ShowPopupAsync(Strings.ChannelCantOpenPrivate, Strings.AppName, Strings.OK);
                    return;
                }

                if (supergroup.RestrictionInfo != null && await ShowRestrictionInfoAsync(supergroup.RestrictionInfo, false))
                {
                    return;
                }
            }

            if (Frame?.Content is ChatPage page && page.ViewModel?.ChatId == chat.Id && page.ViewModel?.TopicId.AreTheSame(topic) is true && !scheduled && !createNewWindow)
            {
                var viewModel = page.ViewModel;
                if (message != null)
                {
                    TextQuote quote = null;
                    int checklistTaskId = 0;
                    string pollOptionId = string.Empty;

                    state?.TryRemove("highlight", out quote);
                    state?.TryRemove("checklist_task_id", out checklistTaskId);
                    state?.TryRemove("poll_option_id", out pollOptionId);

                    await viewModel.LoadMessageSliceAsync(null, message.Value, highlight: quote, checklistTaskId: checklistTaskId, pollOptionId: pollOptionId);
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
                    page.ViewModel.Delegate.UpdateUser(chat, user, userFull, false, true);
                }

                page.ViewModel.TextField?.Focus(FocusState.Programmatic);

                if (state != null && state.TryGet("package", out DataPackageView package))
                {
                    await page.ViewModel.HandlePackageAsync(package);
                }

                OverlayWindow.Current?.TryHide(ContentDialogResult.None);
            }
            else
            {
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

                    if (scheduled)
                    {
                        target = typeof(ChatScheduledPage);
                        parameter = chat.Id;
                    }
                    else
                    {
                        target = typeof(ChatPage);
                        parameter = topic == null ? chat.Id : new ChatMessageTopic(chat.Id, topic);
                    }

                    // This is horrible here but I don't want to bloat this method with dozens of parameters.
                    var masterDetailPanel = Window.Content.GetChild<MasterDetailPanel>();
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
                    if (Frame?.Content is ChatPage chatPage && !scheduled && !force)
                    {
                        object parameter;
                        if (topic is not null)
                        {
                            parameter = new ChatMessageTopic(chat.Id, topic);
                        }
                        else
                        {
                            parameter = chat.Id;
                        }

                        chatPage.ViewModel.NavigatedFrom(null, false);

                        chatPage.Deactivate(true);
                        chatPage.Activate(this);
                        chatPage.ViewModel.NavigationService = this;
                        chatPage.ViewModel.Dispatcher = Dispatcher;
                        await chatPage.ViewModel.NavigatedToAsync(parameter, Windows.UI.Xaml.Navigation.NavigationMode.New, state);

                        FrameFacade.RaiseNavigated(parameter);
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

                        if (scheduled)
                        {
                            target = typeof(ChatScheduledPage);
                            parameter = chat.Id;
                        }
                        else
                        {
                            target = typeof(ChatPage);
                            parameter = topic == null ? chat.Id : new ChatMessageTopic(chat.Id, topic);

                            var currentChat = this.GetChatFromBackStack(true, typeof(ProfilePage), typeof(ChatPinnedPage));
                            if (currentChat.ChatId == chat.Id && currentChat.MessageTopic.AreTheSame(topic))
                            {
                                if (CurrentPageType == typeof(ProfilePage) || CurrentPageType == typeof(ChatPinnedPage))
                                {
                                    var cacheKey = Guid.NewGuid().ToString();
                                    var cacheParameter = parameter;

                                    parameter = cacheKey;
                                    CacheKeyToParameter[cacheKey] = cacheParameter;

                                    GoBackAt(0, false);

                                    Frame.BackStack.Add(new Windows.UI.Xaml.Navigation.PageStackEntry(target, parameter, null));
                                    GoBack(state, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
                                    Frame.ForwardStack.Clear();
                                    return;
                                }
                                else if (CurrentPageType == typeof(ChatPage))
                                {
                                    info = new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight };
                                }
                            }
                            else if (currentChat.ChatId == chat.Id && currentChat.MessageTopic == null && topic != null)
                            {
                                info = new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight };
                            }
                            else
                            {
                                info = new SuppressNavigationTransitionInfo();
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

        public async Task<bool> ShowRestrictionInfoAsync(RestrictionInfo info, bool messages)
        {
            if (info.RestrictionReason.Length > 0 || !ClientService.Options.CanIgnoreSensitiveContentRestrictions)
            {
                if (info.RestrictionReason.Length > 0)
                {
                    ShowPopup(info.RestrictionReason, Strings.AppName, Strings.OK);
                }
                else
                {
                    var text = messages
                        ? Strings.MessageShowSensitiveContentMediaTextClosed
                        : Strings.MessageShowSensitiveContentChannelTextClosed;

                    var title = messages
                        ? Strings.MessageShowSensitiveContentMediaTitle
                        : Strings.MessageShowSensitiveContentChannelTitle;

                    ShowPopup(text, title, secondary: Strings.MessageShowSensitiveContentChannelTextClosedButton);
                }

                return true;
            }
            else if (info.HasSensitiveContent)
            {
                return await ShowSensitiveContentAsync(messages ? SensitiveContentSource.Message : SensitiveContentSource.Chat);
            }

            return false;
        }

        public async Task<bool> ShowSensitiveContentAsync(SensitiveContentSource source)
        {
            if (ClientService.AgeVerificationParameters == null)
            {
                var text = source switch
                {
                    SensitiveContentSource.Chat => Strings.MessageShowSensitiveContentChannelText,
                    SensitiveContentSource.Message => Strings.MessageShowSensitiveContentMediaText,
                    _ => Strings.ConfirmSensitiveContentText
                };

                var title = source switch
                {
                    SensitiveContentSource.Chat => Strings.MessageShowSensitiveContentChannelTitle,
                    SensitiveContentSource.Message => Strings.MessageShowSensitiveContentMediaTitle,
                    _ => Strings.ConfirmSensitiveContentTitle
                };

                var popup = new MessagePopup
                {
                    Title = title,
                    Message = text,
                    SecondaryButtonText = Strings.Cancel
                };

                if (source == SensitiveContentSource.Settings)
                {
                    popup.PrimaryButtonText = Strings.Confirm;
                }
                else
                {
                    popup.PrimaryButtonText = Strings.MessageShowSensitiveContentButton;
                    popup.CheckBoxLabel = Strings.MessageShowSensitiveContentAlways;
                }

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    if (popup.IsChecked is true)
                    {
                        ClientService.Options.IgnoreSensitiveContentRestrictions = true;
                    }

                    return false;
                }

                return true;
            }
            else
            {
                var message = LocaleService.Current.GetString("AgeVerificationText" + ClientService.AgeVerificationParameters.Country);

                var confirm = await ShowPopupAsync(message, Strings.AgeVerificationTitle, Strings.AgeVerificationButton, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    // We inline here instead of using MessageHelper.NavigateToMainWebApp to have more control over the process.
                    var response = await ClientService.SendAsync(new SearchPublicChat(ClientService.AgeVerificationParameters.VerificationBotUsername));
                    if (response is Chat chat && ClientService.TryGetUser(chat, out User botUser))
                    {
                        if (botUser.Type is not UserTypeBot { HasMainWebApp: true })
                        {
                            return true;
                        }

                        var responsa = await ClientService.SendAsync(new GetMainWebApp(0, botUser.Id, string.Empty, new WebAppOpenParameters(Theme.Current.Parameters, Constants.WebAppHostName, new WebAppOpenModeFullSize())));
                        if (responsa is MainWebApp webApp)
                        {
                            var sourceLink = new InternalLinkTypeMainWebApp(ClientService.AgeVerificationParameters.VerificationBotUsername, string.Empty, webApp.Mode);
                            var tcs = new TaskCompletionSource<bool>();

                            await OpenAsync(new ViewServiceOptions
                            {
                                Width = 384,
                                Height = 640,
                                PersistedId = "WebApp",
                                ViewMode = ViewServiceMode.Default,
                                Content = control =>
                                {
                                    var page = new WebAppPage(ClientService, this, botUser, webApp.Url, sourceLink: sourceLink);
                                    void handler(object sender, WebAppAgeVerificationCompletedEventArgs args)
                                    {
                                        page.AgeVerificationCompleted -= handler;
                                        tcs.SetResult(args.Passed && args.Age >= ClientService.AgeVerificationParameters.MinAge);
                                    }

                                    page.AgeVerificationCompleted += handler;
                                    return page;
                                }
                            });

                            var passed = await tcs.Task;
                            if (passed)
                            {
                                ClientService.Options.IgnoreSensitiveContentRestrictions = true;

                                ShowToast(Strings.SensitiveContentSettingsToast, ToastPopupIcon.Info);
                                return false;
                            }

                            ShowToast(string.Format("**{0}**\n{1}", Strings.AgeVerificationFailedTitle, Strings.AgeVerificationFailedText), ToastPopupIcon.Error);
                            return true;
                        }

                        return true;
                    }
                    else
                    {
                        ShowToast(Strings.NoUsernameFound, ToastPopupIcon.Info);
                        return true;
                    }
                }

                return true;
            }
        }

        public async void NavigateToChat(long chatId, long? message = null, MessageTopic topic = null, string accessToken = null, NavigationState state = null, bool scheduled = false, bool force = true, bool createNewWindow = false)
        {
            var chat = _clientService.GetChat(chatId);

            // TODO: this should never happen
            chat ??= await _clientService.SendAsync(new GetChat(chatId)) as Chat;

            if (chat == null)
            {
                return;
            }

            NavigateToChat(chat, message, topic, accessToken, state, scheduled, force, createNewWindow);
        }

        public async void NavigateToUser(long userId, bool toChat = false, NavigationState state = null, NavigationTransitionInfo infoOverride = null)
        {
            if (_clientService.TryGetChatFromUser(userId, out Chat chat))
            {
                var user = ClientService.GetUser(userId);
                if (user?.Type is UserTypeBot || toChat)
                {
                    NavigateToChat(chat, state: state);
                }
                else
                {
                    Navigate(typeof(ProfilePage), chat.Id, state: state, infoOverride: infoOverride);
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

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    Navigate(typeof(SettingsPasscodePage));
                }
            }
            else
            {
                var popup = new SettingsPasscodePopup();

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    var viewModel = Session.Resolve<SettingsPasscodeViewModel>();
                    viewModel.NavigationService = this;

                    if (await viewModel.ToggleAsync())
                    {
                        Navigate(typeof(SettingsPasscodePage));
                    }
                }
            }
        }

        public async Task<PasswordState> NavigateToPasswordAsync()
        {
            var response = await ClientService.SendAsync(new GetPasswordState());
            if (response is PasswordState passwordState)
            {
                if (passwordState.HasPassword)
                {
                    var popup = new SettingsPasswordConfirmPopup(ClientService, passwordState);

                    var confirm = await ShowPopupAsync(popup);
                    if (confirm == ContentDialogResult.Primary && !string.IsNullOrEmpty(popup.Password))
                    {
                        Navigate(typeof(SettingsPasswordPage), popup.Password);
                    }
                    else if (popup.RecoveryEmailAddressCodeInfo != null)
                    {
                        var emailCode = new SettingsPasswordEmailCodePopup(ClientService, popup.RecoveryEmailAddressCodeInfo, SettingsPasswordEmailCodeType.Recovery);

                        if (ContentDialogResult.Primary == await ShowPopupAsync(emailCode))
                        {
                            ShowPopup(new SettingsPasswordDonePopup());
                        }
                    }
                }
                else if (passwordState.RecoveryEmailAddressCodeInfo != null)
                {
                    var emailCode = new SettingsPasswordEmailCodePopup(ClientService, passwordState.RecoveryEmailAddressCodeInfo, SettingsPasswordEmailCodeType.Continue);

                    if (ContentDialogResult.Primary == await ShowPopupAsync(emailCode))
                    {
                        ShowPopup(new SettingsPasswordDonePopup());
                    }
                }
                else
                {
                    passwordState = await NavigateToPasswordSetupAsync();
                }

                return passwordState;
            }

            return null;
        }

        public async Task<PasswordState> NavigateToPasswordSetupAsync()
        {
            var intro = new SettingsPasswordIntroPopup();

            if (ContentDialogResult.Primary != await ShowPopupAsync(intro))
            {
                return null;
            }

            var password = new SettingsPasswordCreatePopup();

            if (ContentDialogResult.Primary != await ShowPopupAsync(password))
            {
                return null;
            }

            var hint = new SettingsPasswordHintPopup(null, null, password.Password);

            if (ContentDialogResult.Primary != await ShowPopupAsync(hint))
            {
                return null;
            }

            var emailAddress = new SettingsPasswordEmailAddressPopup(ClientService, new SetPassword(string.Empty, password.Password, hint.Hint, true, string.Empty));

            if (ContentDialogResult.Primary != await ShowPopupAsync(emailAddress))
            {
                return null;
            }

            PasswordState passwordState;

            if (emailAddress.PasswordState?.RecoveryEmailAddressCodeInfo != null)
            {
                var emailCode = new SettingsPasswordEmailCodePopup(ClientService, emailAddress.PasswordState?.RecoveryEmailAddressCodeInfo, SettingsPasswordEmailCodeType.New);

                if (ContentDialogResult.Primary != await ShowPopupAsync(emailCode))
                {
                    return null;
                }

                passwordState = emailCode.PasswordState;
            }
            else
            {
                passwordState = emailAddress.PasswordState;
            }

            await ShowPopupAsync(new SettingsPasswordDonePopup());
            return passwordState;
        }
    }
}
