//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Controls.Stories;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Stories;
using Telegram.Views;
using Telegram.Views.Chats.Popups;
using Telegram.Views.Folders;
using Telegram.Views.Folders.Popups;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Settings;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Point = Windows.Foundation.Point;
using User = Telegram.Td.Api.User;

namespace Telegram.Common
{
    public class OpenUrlSource
    {

    }

    public class OpenUrlSourceChat : OpenUrlSource
    {
        public long ChatId { get; }

        public OpenUrlSourceChat(long chatId)
        {
            ChatId = chatId;
        }
    }

    public class MessageHelper
    {
        public static async void CopyLink(IClientService clientService, InternalLinkType type)
        {
            var response = await clientService.SendAsync(new GetInternalLink(type, true));
            if (response is HttpUrl httpUrl)
            {
                CopyLink(httpUrl.Url);
            }
        }

        public static void CopyLink(string link, bool publiz = true)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(link);
            ClipboardEx.TrySetContent(dataPackage);

            Window.Current.ShowToast(publiz ? Strings.LinkCopied : Strings.LinkCopiedPrivate, new LocalFileSource("ms-appx:///Assets/Toasts/LinkCopied.tgs"));
        }

        public static void CopyText(string text)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            ClipboardEx.TrySetContent(dataPackage);

            Window.Current.ShowToast(Strings.TextCopied, new LocalFileSource("ms-appx:///Assets/Toasts/Copied.tgs"));
        }

        public static bool TryCreateUri(string url, out Uri uri)
        {
            if (!url.StartsWith("http://")
                && !url.StartsWith("https://")
                && !url.StartsWith("tg:")
                && !url.StartsWith("ftp:"))
            {
                url = "http://" + url;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out uri);
        }

        public static bool IsTelegramUrl(Uri uri)
        {
            var host = uri.Host;

            var splitHostName = uri.Host.Split('.');
            if (splitHostName.Length >= 2)
            {
                host = splitHostName[^2] + "." +
                       splitHostName[^1];
            }

            if (Constants.TelegramHosts.Contains(host))
            {
                return true;
            }

            return IsTelegramScheme(uri);
        }

        public static bool IsTelegramScheme(Uri uri)
        {
            return string.Equals(uri.Scheme, "tg", StringComparison.OrdinalIgnoreCase);
        }

        public static async void OpenTelegramUrl(IClientService clientService, INavigationService navigation, Uri uri, OpenUrlSource source = null)
        {
            var url = uri.ToString();
            if (url.Contains("telegra.ph"))
            {
                navigation.NavigateToInstant(url);
                return;
            }

            var response = await clientService.SendAsync(new GetInternalLinkType(url));
            if (response is InternalLinkType internalLink)
            {
                OpenTelegramUrl(clientService, navigation, internalLink, source);
            }
            else if (!string.Equals(uri.Scheme, "tg", StringComparison.OrdinalIgnoreCase))
            {
                OpenLoginUrl(clientService, navigation, url, await clientService.SendAsync(new GetExternalLinkInfo(url)));
            }
        }

        private static async void OpenLoginUrl(IClientService clientService, INavigationService navigation, string url, BaseObject info)
        {
            if (info is LoginUrlInfoOpen infoOpen)
            {
                OpenUrl(null, null, infoOpen.Url, !infoOpen.SkipConfirmation);
            }
            else if (info is LoginUrlInfoRequestConfirmation requestConfirmation)
            {
                var dialog = new LoginUrlInfoPopup(clientService, requestConfirmation);

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm != ContentDialogResult.Primary || !dialog.HasAccepted)
                {
                    return;
                }

                var response = await clientService.SendAsync(new GetExternalLink(url, dialog.HasWriteAccess));
                if (response is HttpUrl httpUrl)
                {
                    OpenUrl(null, null, httpUrl.Url, false);
                }
                else if (response is Error)
                {
                    OpenUrl(null, null, url, false);
                }
            }
        }

        public static void OpenTelegramUrl(IClientService clientService, INavigationService navigation, InternalLinkType internalLink, OpenUrlSource source = null)
        {
            if (internalLink is InternalLinkTypeActiveSessions)
            {
                navigation.Navigate(typeof(SettingsSessionsPage));
            }
            else if (internalLink is InternalLinkTypeAuthenticationCode authenticationCode)
            {
                if (clientService.AuthorizationState is AuthorizationStateWaitCode)
                {
                    clientService.Send(new CheckAuthenticationCode(authenticationCode.Code));
                }
            }
            else if (internalLink is InternalLinkTypeAttachmentMenuBot attachmentMenuBot)
            {
                NavigateToAttachmentMenuBot(clientService, navigation, attachmentMenuBot, source);
            }
            else if (internalLink is InternalLinkTypeBackground background)
            {
                NavigateToBackground(clientService, navigation, background.BackgroundName);
            }
            else if (internalLink is InternalLinkTypeBotStart botStart)
            {
                NavigateToBotStart(clientService, navigation, botStart.BotUsername, botStart.StartParameter, botStart.Autostart, false);
            }
            else if (internalLink is InternalLinkTypeBotStartInGroup botStartInGroup)
            {
                // Not yet supported: AdministratorRights
                NavigateToBotStart(clientService, navigation, botStartInGroup.BotUsername, botStartInGroup.StartParameter, false, true);
            }
            else if (internalLink is InternalLinkTypeChangePhoneNumber)
            {
                navigation.Navigate(typeof(SettingsProfilePage));
            }
            else if (internalLink is InternalLinkTypeChatBoost chatBoost)
            {
                NavigateToChatBoost(clientService, navigation, chatBoost.Url);
            }
            else if (internalLink is InternalLinkTypeChatInvite chatInvite)
            {
                NavigateToInviteLink(clientService, navigation, chatInvite.InviteLink);
            }
            else if (internalLink is InternalLinkTypeChatFolderInvite chatFolderInvite)
            {
                NavigateToChatFolderInviteLink(clientService, navigation, chatFolderInvite.InviteLink);
            }
            else if (internalLink is InternalLinkTypeChatFolderSettings)
            {
                navigation.Navigate(typeof(FoldersPage));
            }
            else if (internalLink is InternalLinkTypeGame game)
            {
                NavigateToUsername(clientService, navigation, game.BotUsername, null, game.GameShortName);
            }
            else if (internalLink is InternalLinkTypeInstantView instantView)
            {
                navigation.NavigateToInstant(instantView.Url);
            }
            else if (internalLink is InternalLinkTypeInvoice invoice)
            {
                NavigateToInvoice(navigation, invoice.InvoiceName);
            }
            else if (internalLink is InternalLinkTypeLanguagePack languagePack)
            {
                NavigateToLanguage(clientService, navigation, languagePack.LanguagePackId);
            }
            else if (internalLink is InternalLinkTypeMessage message)
            {
                NavigateToMessage(clientService, navigation, message.Url);
            }
            else if (internalLink is InternalLinkTypeMessageDraft messageDraft)
            {
                NavigateToShare(navigation, messageDraft.Text, messageDraft.ContainsLink);
            }
            else if (internalLink is InternalLinkTypePassportDataRequest)
            {

            }
            else if (internalLink is InternalLinkTypePremiumFeatures premiumFeatures)
            {
                navigation.ShowPromo(new PremiumSourceLink(premiumFeatures.Referrer));
            }
            else if (internalLink is InternalLinkTypePremiumGiftCode premiumGiftCode)
            {
                NavigateToPremiumGiftCode(clientService, navigation, premiumGiftCode.Code);
            }
            else if (internalLink is InternalLinkTypePrivacyAndSecuritySettings)
            {
                navigation.Navigate(typeof(SettingsPrivacyAndSecurityPage));
            }
            else if (internalLink is InternalLinkTypePhoneNumberConfirmation phoneNumberConfirmation)
            {
                NavigateToConfirmPhone(clientService, phoneNumberConfirmation.PhoneNumber, phoneNumberConfirmation.Hash);
            }
            else if (internalLink is InternalLinkTypeProxy proxy)
            {
                NavigateToProxy(clientService, proxy.Server, proxy.Port, proxy.Type);
            }
            else if (internalLink is InternalLinkTypePublicChat publicChat)
            {
                NavigateToUsername(clientService, navigation, publicChat.ChatUsername, null, null);
            }
            else if (internalLink is InternalLinkTypeQrCodeAuthentication)
            {

            }
            else if (internalLink is InternalLinkTypeSettings)
            {

            }
            else if (internalLink is InternalLinkTypeStickerSet stickerSet)
            {
                NavigateToStickerSet(stickerSet.StickerSetName);
            }
            else if (internalLink is InternalLinkTypeStory story)
            {
                NavigateToStory(clientService, navigation, story.StorySenderUsername, story.StoryId);
            }
            else if (internalLink is InternalLinkTypeTheme theme)
            {
                NavigateToTheme(clientService, theme.ThemeName);
            }
            else if (internalLink is InternalLinkTypeThemeSettings)
            {
                navigation.Navigate(typeof(SettingsAppearancePage));
            }
            else if (internalLink is InternalLinkTypeUnknownDeepLink unknownDeepLink)
            {
                NavigateToUnknownDeepLink(clientService, unknownDeepLink.Link);
            }
            else if (internalLink is InternalLinkTypeUserPhoneNumber phoneNumber)
            {
                NavigateToPhoneNumber(clientService, navigation, phoneNumber.PhoneNumber);
            }
            else if (internalLink is InternalLinkTypeUserToken userToken)
            {
                NavigateToUserToken(clientService, navigation, userToken.Token);
            }
            else if (internalLink is InternalLinkTypeVideoChat videoChat)
            {
                NavigateToUsername(clientService, navigation, videoChat.ChatUsername, videoChat.InviteHash, null);
            }
            else if (internalLink is InternalLinkTypeWebApp webApp)
            {
                NavigateToWebApp(clientService, navigation, webApp.BotUsername, webApp.StartParameter, webApp.WebAppShortName, source);
            }
        }

        private static async void NavigateToPremiumGiftCode(IClientService clientService, INavigationService navigation, string code)
        {
            var response = await clientService.SendAsync(new CheckPremiumGiftCode(code));
            if (response is PremiumGiftCodeInfo info)
            {
                await new GiftCodePopup(clientService, navigation, info, code).ShowQueuedAsync();
            }
            else
            {
                // TODO: error
            }
        }

        private static async void NavigateToAttachmentMenuBot(IClientService clientService, INavigationService navigation, InternalLinkTypeAttachmentMenuBot attachmentMenuBot, OpenUrlSource source)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(attachmentMenuBot.BotUsername));
            if (response is Chat chat && clientService.TryGetUser(chat, out User botUser))
            {
                if (botUser.Type is not UserTypeBot userTypeBot || !userTypeBot.CanBeAddedToAttachmentMenu)
                {
                    return;
                }

                var sourceChat = source switch
                {
                    OpenUrlSourceChat sourceMessage => clientService.GetChat(sourceMessage.ChatId),
                    _ => null
                };

                var response2 = await clientService.SendAsync(new GetAttachmentMenuBot(botUser.Id));
                if (response2 is AttachmentMenuBot menuBot)
                {
                    OpenMiniApp(clientService, navigation, botUser, menuBot, attachmentMenuBot.Url, sourceChat);
                }
            }
        }

        public static async void OpenMiniApp(IClientService clientService, INavigationService navigation, User user, AttachmentMenuBot bot, string url, Chat sourceChat = null, Action<bool> continuation = null)
        {
            if (bot.ShowDisclaimerInSideMenu || !clientService.IsBotAddedToAttachmentMenu(bot.BotUserId))
            {
                var textBlock = new TextBlock();

                var markdown = ClientEx.ParseMarkdown(Strings.BotWebAppDisclaimerCheck);
                if (markdown != null && markdown.Entities.Count == 1)
                {
                    markdown.Entities[0].Type = new TextEntityTypeTextUrl(Strings.WebAppDisclaimerUrl);
                    TextBlockHelper.SetFormattedText(textBlock, markdown);
                }
                else
                {
                    textBlock.Text = Strings.BotWebAppDisclaimerCheck;
                }

                var popup = new MessagePopup
                {
                    Title = Strings.TermsOfUse,
                    Message = Strings.BotWebAppDisclaimerSubtitle,
                    CheckBoxLabel = textBlock,
                    PrimaryButtonText = Strings.Continue,
                    SecondaryButtonText = Strings.Cancel,
                    IsCheckedRequired = true
                };

                var confirm = await popup.ShowQueuedAsync();
                if (confirm != ContentDialogResult.Primary)
                {
                    continuation?.Invoke(false);
                    return;
                }

                await clientService.SendAsync(new ToggleBotIsAddedToAttachmentMenu(bot.BotUserId, true, true));
            }

            continuation?.Invoke(true);

            var response = await clientService.SendAsync(new GetWebAppUrl(bot.BotUserId, url, Theme.Current.Parameters, Strings.AppName));
            if (response is HttpUrl httpUrl)
            {
                await new WebBotPopup(clientService, navigation, user, httpUrl.Url, bot, sourceChat).ShowQueuedAsync();
            }
        }

        private static async void NavigateToChatBoost(IClientService clientService, INavigationService navigation, string url)
        {
            var response = await clientService.SendAsync(new GetChatBoostLinkInfo(url));
            if (response is ChatBoostLinkInfo linkInfo)
            {
                if (linkInfo.ChatId == 0 || !clientService.TryGetChat(linkInfo.ChatId, out Chat chat))
                {
                    Window.Current.ShowToast(Strings.NoUsernameFound, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                    return;
                }

                var response2 = await clientService.SendAsync(new GetAvailableChatBoostSlots());
                var response3 = await clientService.SendAsync(new GetChatBoostStatus(linkInfo.ChatId));

                if (response2 is ChatBoostSlots result && response3 is ChatBoostStatus status)
                {
                    await new ChatBoostPopup(clientService, navigation, chat, status, result).ShowQueuedAsync();
                }
            }
        }

        private static async void NavigateToStory(IClientService clientService, INavigationService navigation, string username, int storyId)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat)
            {
                var response2 = await clientService.SendAsync(new GetStory(chat.Id, storyId, false));
                if (response2 is Story item)
                {
                    var settings = TLContainer.Current.Resolve<ISettingsService>(clientService.SessionId);
                    var aggregator = TLContainer.Current.Resolve<IEventAggregator>(clientService.SessionId);

                    var story = new StoryViewModel(clientService, item);

                    var activeStories = new ActiveStoriesViewModel(clientService, settings, aggregator, story);
                    var viewModel = new StoryListViewModel(clientService, settings, aggregator, activeStories);
                    viewModel.NavigationService = navigation;

                    var window = new StoriesWindow();
                    window.Update(viewModel, activeStories, StoryOrigin.Card, Rect.Empty, null);
                    _ = window.ShowAsync();
                }
                else
                {
                    Window.Current.ShowToast(Strings.StoryNotFound, new LocalFileSource("ms-appx:///Assets/Toasts/ExpiredStory.tgs"));
                }
            }
            else
            {
                Window.Current.ShowToast(Strings.NoUsernameFound, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        private static async void NavigateToWebApp(IClientService clientService, INavigationService navigation, string botUsername, string startParameter, string webAppShortName, OpenUrlSource source)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(botUsername));
            if (response is Chat chat && clientService.TryGetUser(chat, out User user))
            {
                if (user.Type is not UserTypeBot)
                {
                    return;
                }

                var responss = await clientService.SendAsync(new SearchWebApp(user.Id, webAppShortName));
                if (responss is FoundWebApp foundWebApp)
                {
                    var popup = new MessagePopup
                    {
                        Title = Strings.AppName,
                        Message = Strings.BotWebViewStartPermission,
                        PrimaryButtonText = Strings.Start,
                        SecondaryButtonText = Strings.Cancel,
                    };

                    if (foundWebApp.RequestWriteAccess)
                    {
                        var textBlock = new TextBlock
                        {
                            TextWrapping = TextWrapping.Wrap
                        };

                        var markdown = ClientEx.ParseMarkdown(string.Format(Strings.OpenUrlOption2, user.FirstName));
                        if (markdown != null)
                        {
                            TextBlockHelper.SetFormattedText(textBlock, markdown);
                        }
                        else
                        {
                            textBlock.Text = Strings.OpenUrlOption2;
                        }

                        popup.CheckBoxLabel = textBlock;
                    }

                    var confirm = await popup.ShowQueuedAsync();
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    var chatId = source switch
                    {
                        OpenUrlSourceChat sourceMessage => sourceMessage.ChatId,
                        _ => 0
                    };

                    var responsa = await clientService.SendAsync(new GetWebAppLinkUrl(chatId, user.Id, webAppShortName, startParameter, Theme.Current.Parameters, Strings.AppName, foundWebApp.RequestWriteAccess && popup.IsChecked is true));
                    if (responsa is HttpUrl url)
                    {
                        await new WebBotPopup(clientService, navigation, user, url.Url).ShowQueuedAsync();
                    }
                }
                else
                {
                    navigation.NavigateToChat(chat);
                }
            }
            else
            {
                Window.Current.ShowToast(Strings.NoUsernameFound, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        private static async void NavigateToUnknownDeepLink(IClientService clientService, string url)
        {
            var response = await clientService.SendAsync(new GetDeepLinkInfo(url));
            if (response is DeepLinkInfo info)
            {
                var confirm = await MessagePopup.ShowAsync(info.Text, Strings.AppName, Strings.OK, info.NeedUpdateApplication ? Strings.UpdateApp : null);
                if (confirm == ContentDialogResult.Secondary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?PFN=" + Package.Current.Id.FamilyName));
                }
            }
        }

        private static async void NavigateToBackground(IClientService clientService, INavigationService navigation, string slug)
        {
            var response = await clientService.SendAsync(new SearchBackground(slug));
            if (response is Background background)
            {
                await navigation.ShowPopupAsync(typeof(BackgroundPopup), new BackgroundParameters(background));
            }
        }

        private static async void NavigateToMessage(IClientService clientService, INavigationService navigation, string url)
        {
            var response = await clientService.SendAsync(new GetMessageLinkInfo(url));
            if (response is MessageLinkInfo info && info.ChatId != 0)
            {
                if (info.Message != null)
                {
                    if (info.MessageThreadId != 0)
                    {
                        navigation.NavigateToThread(info.ChatId, info.Message.Id, message: info.Message.Id);
                    }
                    else
                    {
                        navigation.NavigateToChat(info.ChatId, message: info.Message.Id);
                    }
                }
                else
                {
                    navigation.NavigateToChat(info.ChatId);
                }
            }
            else
            {
                await MessagePopup.ShowAsync(Strings.LinkNotFound, Strings.AppName, Strings.OK);
            }
        }

        private static async void NavigateToTheme(IClientService clientService, string slug)
        {
            await MessagePopup.ShowAsync(Strings.ThemeNotSupported, Strings.Theme, Strings.OK);
        }

        private static void NavigateToInvoice(INavigationService navigation, string invoiceName)
        {
            navigation.NavigateToInvoice(new InputInvoiceName(invoiceName));
        }

        public static async void NavigateToLanguage(IClientService clientService, INavigationService navigation, string languagePackId)
        {
            var response = await clientService.SendAsync(new GetLanguagePackInfo(languagePackId));
            if (response is LanguagePackInfo info)
            {
                if (info.Id == SettingsService.Current.LanguagePackId)
                {
                    var confirm = await MessagePopup.ShowAsync(string.Format(Strings.LanguageSame, info.Name), Strings.Language, Strings.OK, Strings.Settings);
                    if (confirm != ContentDialogResult.Secondary)
                    {
                        return;
                    }

                    navigation.Navigate(typeof(SettingsLanguagePage));
                }
                else if (info.TotalStringCount == 0)
                {
                    await MessagePopup.ShowAsync(string.Format(Strings.LanguageUnknownCustomAlert, info.Name), Strings.LanguageUnknownTitle, Strings.OK);
                }
                else
                {
                    var message = info.IsOfficial
                        ? Strings.LanguageAlert
                        : Strings.LanguageCustomAlert;

                    var start = message.IndexOf('[');
                    var end = message.IndexOf(']');
                    if (start != -1 && end != -1)
                    {
                        message = message.Insert(end + 1, $"({info.TranslationUrl})");
                    }

                    var confirm = await MessagePopup.ShowAsync(string.Format(message, info.Name, (int)Math.Ceiling(info.TranslatedStringCount / (float)info.TotalStringCount * 100)), Strings.LanguageTitle, Strings.Change, Strings.Cancel);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    var set = await LocaleService.Current.SetLanguageAsync(info, true);
                    if (set is Ok)
                    {
                        //ApplicationLanguages.PrimaryLanguageOverride = info.Id;
                        //ResourceContext.GetForCurrentView().Reset();
                        //ResourceContext.GetForViewIndependentUse().Reset();

                        //TLWindowContext.Current.NavigationServices.Remove(NavigationService);
                        //BootStrapper.Current.NavigationService.Reset();

                        WindowContext.ForEach(window =>
                        {
                            ResourceContext.GetForCurrentView().Reset();
                            ResourceContext.GetForViewIndependentUse().Reset();

                            if (window.Content is FrameworkElement frameworkElement)
                            {
                                //window.CoreWindow.FlowDirection = _localeService.FlowDirection == FlowDirection.RightToLeft
                                //    ? CoreWindowFlowDirection.RightToLeft
                                //    : CoreWindowFlowDirection.LeftToRight;

                                frameworkElement.FlowDirection = LocaleService.Current.FlowDirection;
                            }

                            if (window.Content is RootPage root)
                            {
                                root.UpdateComponent();
                            }
                        });
                    }
                }
            }
        }

        public static async void NavigateToSendCode(IClientService clientService, string phoneCode)
        {
            if (clientService.AuthorizationState is AuthorizationStateWaitCode)
            {
                if (clientService.Options.TryGetValue("x_firstname", out string firstValue))
                {
                }

                if (clientService.Options.TryGetValue("x_lastname", out string lastValue))
                {
                }

                var response = await clientService.SendAsync(new CheckAuthenticationCode(phoneCode));
                if (response is Error error)
                {
                    if (error.MessageEquals(ErrorType.PHONE_NUMBER_INVALID))
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.InvalidPhoneNumber, Strings.OK);
                    }
                    else if (error.MessageEquals(ErrorType.PHONE_CODE_EMPTY) || error.MessageEquals(ErrorType.PHONE_CODE_INVALID))
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.InvalidCode, Strings.OK);
                    }
                    else if (error.MessageEquals(ErrorType.PHONE_CODE_EXPIRED))
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.CodeExpired, Strings.OK);
                    }
                    else if (error.MessageEquals(ErrorType.FIRSTNAME_INVALID))
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.InvalidFirstName, Strings.OK);
                    }
                    else if (error.MessageEquals(ErrorType.LASTNAME_INVALID))
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.InvalidLastName, Strings.OK);
                    }
                    else if (error.Message.StartsWith("FLOOD_WAIT"))
                    {
                        await MessagePopup.ShowAsync(Strings.FloodWait, Strings.AppName, Strings.OK);
                    }
                    else if (error.Code != -1000)
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.AppName, Strings.OK);
                    }

                    Logger.Error("account.signIn error " + error);
                }
            }
            else
            {
                if (phoneCode.Length > 3)
                {
                    phoneCode = phoneCode.Substring(0, 3) + "-" + phoneCode.Substring(3);
                }

                await MessagePopup.ShowAsync(string.Format(Strings.OtherLoginCode, phoneCode), Strings.AppName, Strings.OK);
            }
        }

        public static async void NavigateToShare(INavigationService navigation, FormattedText text, bool hasUrl)
        {
            await navigation.ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationPostText(text));
        }

        public static async void NavigateToProxy(IClientService clientService, string server, int port, ProxyType type)
        {
            string userText = string.Empty;
            string passText = string.Empty;
            string secretText = string.Empty;
            string secretInfo = string.Empty;

            if (type is ProxyTypeHttp http)
            {
                userText = !string.IsNullOrEmpty(http.Username) ? $"{Strings.UseProxyUsername}: {http.Username}\n" : string.Empty;
                passText = !string.IsNullOrEmpty(http.Password) ? $"{Strings.UseProxyPassword}: {http.Password}\n" : string.Empty;
            }
            else if (type is ProxyTypeSocks5 socks5)
            {
                userText = !string.IsNullOrEmpty(socks5.Username) ? $"{Strings.UseProxyUsername}: {socks5.Username}\n" : string.Empty;
                passText = !string.IsNullOrEmpty(socks5.Password) ? $"{Strings.UseProxyPassword}: {socks5.Password}\n" : string.Empty;
            }
            else if (type is ProxyTypeMtproto mtproto)
            {
                secretText = !string.IsNullOrEmpty(mtproto.Secret) ? $"{Strings.UseProxySecret}: {mtproto.Secret}\n" : string.Empty;
                secretInfo = !string.IsNullOrEmpty(mtproto.Secret) ? $"\n\n{Strings.UseProxyTelegramInfo2}" : string.Empty;
            }

            var confirm = await MessagePopup.ShowAsync($"{Strings.EnableProxyAlert}\n\n{Strings.UseProxyAddress}: {server}\n{Strings.UseProxyPort}: {port}\n{userText}{passText}{secretText}\n{Strings.EnableProxyAlert2}{secretInfo}", Strings.Proxy, Strings.ConnectingConnectProxy, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                clientService.Send(new AddProxy(server ?? string.Empty, port, true, type));
            }
        }

        public static async void NavigateToConfirmPhone(IClientService clientService, string phone, string hash)
        {
            //var response = await clientService.SendConfirmPhoneCodeAsync(hash, false);
            //if (response.IsSucceeded)
            //{
            //    var state = new SignInSentCodePage.NavigationParameters
            //    {
            //        PhoneNumber = phone,
            //        //Result = response.Result,
            //    };

            //    App.Current.NavigationService.Navigate(typeof(SignInSentCodePage), state);

            //    //Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
            //    //{
            //    //    if (frame != null)
            //    //    {
            //    //        frame.CloseBlockingProgress();
            //    //    }
            //    //    TelegramViewBase.NavigateToConfirmPhone(result);
            //    //});
            //}
            //else
            //{
            //    //if (error.CodeEquals(ErrorCode.BAD_REQUEST) && error.TypeEquals(ErrorType.USERNAME_NOT_OCCUPIED))
            //    //{
            //    //    return;
            //    //}
            //    //Telegram.Api.Helpers.Logs.Log.Write(string.Format("account.sendConfirmPhoneCode error {0}", error));
            //};
        }

        public static async void NavigateToStickerSet(string text)
        {
            await StickersPopup.ShowAsync(text);
        }

        public static async void NavigateToPhoneNumber(IClientService clientService, INavigationService navigation, string phoneNumber)
        {
            await NavigateToUserByResponse(clientService, navigation, new SearchUserByPhoneNumber(phoneNumber));
        }

        public static async void NavigateToUserToken(IClientService clientService, INavigationService navigation, string userToken)
        {
            await NavigateToUserByResponse(clientService, navigation, new SearchUserByToken(userToken));
        }

        private static async Task NavigateToUserByResponse(IClientService clientService, INavigationService navigation, Function request)
        {
            var response = await clientService.SendAsync(request);
            if (response is User user)
            {
                var chat = await clientService.SendAsync(new CreatePrivateChat(user.Id, false)) as Chat;
                if (chat != null)
                {
                    navigation.Navigate(typeof(ProfilePage), chat.Id);
                }
                else
                {
                    Window.Current.ShowToast(Strings.NoUsernameFound, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                }
            }
            else
            {
                Window.Current.ShowToast(Strings.NoUsernameFound, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        public static async void NavigateToBotStart(IClientService clientService, INavigationService navigation, string username, string startParameter, bool autoStart, bool group)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat && clientService.TryGetUser(chat, out User user))
            {
                if (group)
                {
                    await navigation.ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationStartBot(user, startParameter));
                }
                else if (autoStart)
                {
                    clientService.Send(new SendBotStartMessage(user.Id, chat.Id, startParameter));
                    navigation.NavigateToChat(chat);
                }
                else
                {
                    navigation.NavigateToChat(chat, accessToken: startParameter);
                }
            }
            else
            {
                Window.Current.ShowToast(Strings.NoUsernameFound, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        public static async void NavigateToUsername(IClientService clientService, INavigationService navigation, string username, string videoChat, string game)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat)
            {
                if (game != null)
                {

                }
                else if (clientService.TryGetUser(chat, out User user))
                {
                    if (user.Type is UserTypeBot)
                    {
                        navigation.NavigateToChat(chat);
                    }
                    else
                    {
                        navigation.Navigate(typeof(ProfilePage), chat.Id);
                    }
                }
                else if (videoChat != null)
                {
                    navigation.NavigateToChat(chat, state: new NavigationState { { "videoChat", videoChat } });
                }
                else
                {
                    navigation.NavigateToChat(chat);
                }
            }
            else
            {
                Window.Current.ShowToast(Strings.NoUsernameFound, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        public static async void NavigateToInviteLink(IClientService clientService, INavigationService navigation, string link)
        {
            var response = await clientService.CheckChatInviteLinkAsync(link);
            if (response is ChatInviteLinkInfo info)
            {
                if (info.ChatId != 0)
                {
                    navigation.NavigateToChat(info.ChatId);
                }
                else
                {
                    var popup = new JoinChatPopup(clientService, info);

                    var confirm = await popup.ShowQueuedAsync();
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    var import = await clientService.SendAsync(new JoinChatByInviteLink(link));
                    if (import is Chat chat)
                    {
                        navigation.NavigateToChat(chat);
                    }
                    else if (import is Error error)
                    {
                        if (error.MessageEquals(ErrorType.INVITE_REQUEST_SENT))
                        {
                            await MessagePopup.ShowAsync(info.Type is InviteLinkChatTypeChannel ? Strings.RequestToJoinChannelSentDescription : Strings.RequestToJoinGroupSentDescription, Strings.RequestToJoinSent, Strings.OK);
                            return;

                            var message = Strings.RequestToJoinSent + Environment.NewLine + (info.Type is InviteLinkChatTypeChannel ? Strings.RequestToJoinChannelSentDescription : Strings.RequestToJoinGroupSentDescription);
                            var entity = new TextEntity(0, Strings.RequestToJoinSent.Length, new TextEntityTypeBold());

                            var text = new FormattedText(message, new[] { entity });

                            Window.Current.ShowToast(text, new LocalFileSource("ms-appx:///Assets/Toasts/JoinRequested.tgs"));
                        }
                        else if (error.MessageEquals(ErrorType.FLOOD_WAIT))
                        {
                            await MessagePopup.ShowAsync(Strings.FloodWait, Strings.AppName, Strings.OK);
                        }
                        else if (error.MessageEquals(ErrorType.USERS_TOO_MUCH))
                        {
                            await MessagePopup.ShowAsync(Strings.JoinToGroupErrorFull, Strings.AppName, Strings.OK);
                        }
                        else
                        {
                            await MessagePopup.ShowAsync(Strings.JoinToGroupErrorNotExist, Strings.AppName, Strings.OK);
                        }
                    }
                }
            }
            else if (response is Error error)
            {
                if (error.MessageEquals(ErrorType.FLOOD_WAIT))
                {
                    await MessagePopup.ShowAsync(Strings.FloodWait, Strings.AppName, Strings.OK);
                }
                else
                {
                    await MessagePopup.ShowAsync(Strings.JoinToGroupErrorNotExist, Strings.AppName, Strings.OK);
                }
            }
        }

        public static async void NavigateToChatFolderInviteLink(IClientService clientService, INavigationService navigation, string link)
        {
            var response = await clientService.SendAsync(new CheckChatFolderInviteLink(link));
            if (response is ChatFolderInviteLinkInfo info)
            {
                var tsc = new TaskCompletionSource<object>();

                var confirm = await navigation.ShowPopupAsync(typeof(AddFolderPopup), info, tsc);
                if (confirm == ContentDialogResult.Primary)
                {
                    var result = await tsc.Task;
                    if (result is IList<long> chats)
                    {
                        if (info.ChatFolderInfo.Id == 0)
                        {
                            var import = await clientService.SendAsync(new AddChatFolderByInviteLink(link, chats));
                            if (import is Error error)
                            {
                                if (error.MessageEquals(ErrorType.CHATLISTS_TOO_MUCH))
                                {
                                    navigation.ShowLimitReached(new PremiumLimitTypeShareableChatFolderCount());
                                }
                                else if (error.MessageEquals(ErrorType.FILTER_INCLUDE_TOO_MUCH))
                                {
                                    navigation.ShowLimitReached(new PremiumLimitTypeChatFolderChosenChatCount());
                                }
                                else if (error.MessageEquals(ErrorType.CHANNELS_TOO_MUCH))
                                {
                                    navigation.ShowLimitReached(new PremiumLimitTypeSupergroupCount());
                                }
                                else
                                {
                                    await MessagePopup.ShowAsync(Strings.FolderLinkExpiredAlert, Strings.AppName, Strings.OK);
                                }
                            }
                        }
                        else if (chats.Count > 0)
                        {
                            clientService.Send(new ProcessChatFolderNewChats(info.ChatFolderInfo.Id, chats));
                        }
                    }
                }
            }
            else if (response is Error error)
            {
                await MessagePopup.ShowAsync(Strings.FolderLinkExpiredAlert, Strings.AppName, Strings.OK);
            }
        }

        public static bool IsValidUsername(string username)
        {
            if (username.Length <= 2)
            {
                return false;
            }
            if (username.Length > 32)
            {
                return false;
            }
            if (username[0] != '@')
            {
                return false;
            }
            for (int i = 1; i < username.Length; i++)
            {
                if (!IsValidUsernameSymbol(username[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsValidCommandSymbol(char symbol)
        {
            return (symbol >= 'a' && symbol <= 'z') || (symbol >= 'A' && symbol <= 'Z') || (symbol >= '0' && symbol <= '9') || symbol == '_';
        }

        public static bool IsValidUsernameSymbol(char symbol)
        {
            return (symbol >= 'a' && symbol <= 'z') || (symbol >= 'A' && symbol <= 'Z') || (symbol >= '0' && symbol <= '9') || symbol == '_';
        }

        public static async void OpenUrl(IClientService clientService, INavigationService navigationService, string url, bool untrust = false, OpenUrlSource source = null)
        {
            if (TryCreateUri(url, out Uri uri))
            {
                if (clientService != null && navigationService != null && IsTelegramUrl(uri))
                {
                    OpenTelegramUrl(clientService, navigationService, uri, source);
                }
                else
                {
                    if (untrust)
                    {
                        var confirm = await MessagePopup.ShowAsync(string.Format(Strings.OpenUrlAlert, url), Strings.OpenUrlTitle, Strings.Open, Strings.Cancel);
                        if (confirm != ContentDialogResult.Primary)
                        {
                            return;
                        }
                    }

                    try
                    {
                        await Launcher.LaunchUriAsync(uri);
                    }
                    catch { }
                }
            }
        }

        #region Entity

        public static void Hyperlink_ContextRequested(ITranslateService service, UIElement sender, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point point))
            {
                var flyout = new MenuFlyout();

                if (sender is RichTextBlock text)
                {
                    Hyperlink_ContextRequested(flyout, service, text, point);
                }

                if (flyout.Items.Count > 0)
                {
                    // We don't want to unfocus the text are when the context menu gets opened
                    flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });
                    args.Handled = true;
                }
                else
                {
                    args.Handled = false;
                }
            }
            else
            {
                args.Handled = false;
            }
        }

        public static void Hyperlink_ContextRequested(ITranslateService service, UIElement sender, string text, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point point))
            {
                var flyout = new MenuFlyout();

                Hyperlink_ContextRequested(flyout, service, text, point);

                if (flyout.Items.Count > 0)
                {
                    // We don't want to unfocus the text are when the context menu gets opened
                    flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });
                    args.Handled = true;
                }
                else
                {
                    args.Handled = false;
                }
            }
            else
            {
                args.Handled = false;
            }
        }

        public static void Hyperlink_ContextRequested(MenuFlyout flyout, ITranslateService service, string text, Point point)
        {
            if (point.X < 0 || point.Y < 0)
            {
                point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
            }

            var length = text.Length;
            if (length > 0)
            {
                flyout.CreateFlyoutItem(LinkCopy_Click, text, Strings.Copy, Icons.DocumentCopy);

                if (service != null && service.CanTranslate(text))
                {
                    flyout.CreateFlyoutItem(LinkTranslate_Click, Tuple.Create(service, text), Strings.TranslateMessage, Icons.Translate);
                }
            }
        }

        public static void Hyperlink_ContextRequested(MenuFlyout flyout, ITranslateService service, RichTextBlock text, Point point)
        {
            if (point.X < 0 || point.Y < 0)
            {
                point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
            }

            if (text.SelectedText.Length > 0)
            {
                Hyperlink_ContextRequested(flyout, service, text.SelectedText, point);
            }
            else
            {
                var hyperlink = text.GetHyperlinkFromPoint(point);
                if (hyperlink == null)
                {
                    return;
                }

                var link = GetEntityData(hyperlink);
                if (link == null)
                {
                    return;
                }

                var type = GetEntityType(hyperlink);
                if (type is null or TextEntityTypeUrl or TextEntityTypeTextUrl)
                {
                    var action = GetEntityAction(hyperlink);
                    if (action != null)
                    {
                        flyout.CreateFlyoutItem(action, Strings.Open, Icons.OpenIn);
                    }
                    else
                    {
                        flyout.CreateFlyoutItem(LinkOpen_Click, link, Strings.Open, Icons.OpenIn);
                    }

                    flyout.CreateFlyoutItem(LinkCopy_Click, link, Strings.Copy, Icons.DocumentCopy);
                }
                else
                {
                    flyout.CreateFlyoutItem(CopyText, link, Strings.Copy, Icons.DocumentCopy);
                }
            }
        }

        public static void Hyperlink_ContextRequested(UIElement sender, string link, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                var flyout = new MenuFlyout();
                flyout.CreateFlyoutItem(LinkOpen_Click, link, Strings.Open, Icons.OpenIn);
                flyout.CreateFlyoutItem(LinkCopy_Click, link, Strings.Copy, Icons.DocumentCopy);

                // We don't want to unfocus the text are when the context menu gets opened
                flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });

                args.Handled = true;
            }
        }

        private static async void LinkOpen_Click(string link)
        {
            if (TryCreateUri(link, out Uri uri))
            {
                try
                {
                    await Launcher.LaunchUriAsync(uri);
                }
                catch
                {
                    Logger.Error();
                }
            }
        }

        private static void LinkCopy_Click(string link)
        {
            CopyLink(link);
        }

        private static async void LinkTranslate_Click(Tuple<ITranslateService, string> tuple)
        {
            var entity = tuple.Item2;
            var service = tuple.Item1;

            var language = LanguageIdentification.IdentifyLanguage(entity);
            var popup = new TranslatePopup(service, entity, language, LocaleService.Current.CurrentCulture.TwoLetterISOLanguageName, true);
            await popup.ShowQueuedAsync();
        }



        public static Action GetEntityAction(DependencyObject obj)
        {
            return (Action)obj.GetValue(EntityActionProperty);
        }

        public static void SetEntityAction(DependencyObject obj, Action value)
        {
            obj.SetValue(EntityActionProperty, value);
        }

        public static readonly DependencyProperty EntityActionProperty =
            DependencyProperty.RegisterAttached("EntityAction", typeof(Action), typeof(MessageHelper), new PropertyMetadata(null));





        public static string GetEntityData(DependencyObject obj)
        {
            return (string)obj.GetValue(EntityDataProperty);
        }

        public static void SetEntityData(DependencyObject obj, string value)
        {
            obj.SetValue(EntityDataProperty, value);
        }

        public static readonly DependencyProperty EntityDataProperty =
            DependencyProperty.RegisterAttached("EntityData", typeof(string), typeof(MessageHelper), new PropertyMetadata(null));





        public static TextEntityType GetEntityType(DependencyObject obj)
        {
            return (TextEntityType)obj.GetValue(EntityTypeProperty);
        }

        public static void SetEntityType(DependencyObject obj, TextEntityType value)
        {
            obj.SetValue(EntityTypeProperty, value);
        }

        public static readonly DependencyProperty EntityTypeProperty =
            DependencyProperty.RegisterAttached("EntityType", typeof(TextEntityType), typeof(MessageHelper), new PropertyMetadata(null));





        #endregion
    }

    public enum MessageCommandType
    {
        Invoke,
        Mention,
        Hashtag
    }
}
