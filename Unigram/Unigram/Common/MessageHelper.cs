using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.Views;
using Unigram.Views.Host;
using Unigram.Views.Popups;
using Unigram.Views.Settings;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources.Core;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace Unigram.Common
{
    public class MessageHelper
    {
        public static bool IsAnyCharacterRightToLeft(string s)
        {
            if (s == null)
            {
                return false;
            }

            //if (s.Length > 2)
            //{
            //    s = s.Substring(s.Length - 2);
            //}

            for (int i = 0; i < s.Length; i += char.IsSurrogatePair(s, i) ? 2 : 1)
            {
                var codepoint = char.ConvertToUtf32(s, i);
                if (IsRandALCat(codepoint))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRandALCat(int c)
        {
            bool hasRandALCat = false;
            if (c is >= 0x5BE and <= 0x10B7F)
            {
                if (c <= 0x85E)
                {
                    if (c == 0x5BE) hasRandALCat = true;
                    else if (c == 0x5C0) hasRandALCat = true;
                    else if (c == 0x5C3) hasRandALCat = true;
                    else if (c == 0x5C6) hasRandALCat = true;
                    else if (c is >= 0x5D0 and <= 0x5EA) hasRandALCat = true;
                    else if (c is >= 0x5F0 and <= 0x5F4) hasRandALCat = true;
                    else if (c == 0x608) hasRandALCat = true;
                    else if (c == 0x60B) hasRandALCat = true;
                    else if (c == 0x60D) hasRandALCat = true;
                    else if (c == 0x61B) hasRandALCat = true;
                    else if (c is >= 0x61E and <= 0x64A) hasRandALCat = true;
                    else if (c is >= 0x66D and <= 0x66F) hasRandALCat = true;
                    else if (c is >= 0x671 and <= 0x6D5) hasRandALCat = true;
                    else if (c is >= 0x6E5 and <= 0x6E6) hasRandALCat = true;
                    else if (c is >= 0x6EE and <= 0x6EF) hasRandALCat = true;
                    else if (c is >= 0x6FA and <= 0x70D) hasRandALCat = true;
                    else if (c == 0x710) hasRandALCat = true;
                    else if (c is >= 0x712 and <= 0x72F) hasRandALCat = true;
                    else if (c is >= 0x74D and <= 0x7A5) hasRandALCat = true;
                    else if (c == 0x7B1) hasRandALCat = true;
                    else if (c is >= 0x7C0 and <= 0x7EA) hasRandALCat = true;
                    else if (c is >= 0x7F4 and <= 0x7F5) hasRandALCat = true;
                    else if (c == 0x7FA) hasRandALCat = true;
                    else if (c is >= 0x800 and <= 0x815) hasRandALCat = true;
                    else if (c == 0x81A) hasRandALCat = true;
                    else if (c == 0x824) hasRandALCat = true;
                    else if (c == 0x828) hasRandALCat = true;
                    else if (c is >= 0x830 and <= 0x83E) hasRandALCat = true;
                    else if (c is >= 0x840 and <= 0x858) hasRandALCat = true;
                    else if (c == 0x85E) hasRandALCat = true;
                }
                else if (c == 0x200F) hasRandALCat = true;
                else if (c >= 0xFB1D)
                {
                    if (c == 0xFB1D) hasRandALCat = true;
                    else if (c is >= 0xFB1F and <= 0xFB28) hasRandALCat = true;
                    else if (c is >= 0xFB2A and <= 0xFB36) hasRandALCat = true;
                    else if (c is >= 0xFB38 and <= 0xFB3C) hasRandALCat = true;
                    else if (c == 0xFB3E) hasRandALCat = true;
                    else if (c is >= 0xFB40 and <= 0xFB41) hasRandALCat = true;
                    else if (c is >= 0xFB43 and <= 0xFB44) hasRandALCat = true;
                    else if (c is >= 0xFB46 and <= 0xFBC1) hasRandALCat = true;
                    else if (c is >= 0xFBD3 and <= 0xFD3D) hasRandALCat = true;
                    else if (c is >= 0xFD50 and <= 0xFD8F) hasRandALCat = true;
                    else if (c is >= 0xFD92 and <= 0xFDC7) hasRandALCat = true;
                    else if (c is >= 0xFDF0 and <= 0xFDFC) hasRandALCat = true;
                    else if (c is >= 0xFE70 and <= 0xFE74) hasRandALCat = true;
                    else if (c is >= 0xFE76 and <= 0xFEFC) hasRandALCat = true;
                    else if (c is >= 0x10800 and <= 0x10805) hasRandALCat = true;
                    else if (c == 0x10808) hasRandALCat = true;
                    else if (c is >= 0x1080A and <= 0x10835) hasRandALCat = true;
                    else if (c is >= 0x10837 and <= 0x10838) hasRandALCat = true;
                    else if (c == 0x1083C) hasRandALCat = true;
                    else if (c is >= 0x1083F and <= 0x10855) hasRandALCat = true;
                    else if (c is >= 0x10857 and <= 0x1085F) hasRandALCat = true;
                    else if (c is >= 0x10900 and <= 0x1091B) hasRandALCat = true;
                    else if (c is >= 0x10920 and <= 0x10939) hasRandALCat = true;
                    else if (c == 0x1093F) hasRandALCat = true;
                    else if (c == 0x10A00) hasRandALCat = true;
                    else if (c is >= 0x10A10 and <= 0x10A13) hasRandALCat = true;
                    else if (c is >= 0x10A15 and <= 0x10A17) hasRandALCat = true;
                    else if (c is >= 0x10A19 and <= 0x10A33) hasRandALCat = true;
                    else if (c is >= 0x10A40 and <= 0x10A47) hasRandALCat = true;
                    else if (c is >= 0x10A50 and <= 0x10A58) hasRandALCat = true;
                    else if (c is >= 0x10A60 and <= 0x10A7F) hasRandALCat = true;
                    else if (c is >= 0x10B00 and <= 0x10B35) hasRandALCat = true;
                    else if (c is >= 0x10B40 and <= 0x10B55) hasRandALCat = true;
                    else if (c is >= 0x10B58 and <= 0x10B72) hasRandALCat = true;
                    else if (c is >= 0x10B78 and <= 0x10B7F) hasRandALCat = true;
                }
            }

            return hasRandALCat;
        }

        public static bool TryCreateUri(string url, out Uri uri)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                return true;
            }

            return Uri.TryCreate("http://" + url, UriKind.Absolute, out uri);
        }

        public static bool IsTelegramUrl(Uri uri)
        {
            var host = uri.Host;
            if (host.StartsWith("www."))
            {
                host = host.Substring("www.".Length);
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

        public static async void OpenTelegramUrl(IProtoService protoService, INavigationService navigation, Uri uri)
        {
            var url = uri.ToString();
            if (url.Contains("telegra.ph"))
            {
                navigation.NavigateToInstant(url);
                return;
            }

            var response = await protoService.SendAsync(new GetInternalLinkType(url));
            if (response is InternalLinkTypeActiveSessions)
            {

            }
            else if (response is InternalLinkTypeAuthenticationCode)
            {

            }
            else if (response is InternalLinkTypeBackground background)
            {
                NavigateToBackground(protoService, navigation, background.BackgroundName);
            }
            else if (response is InternalLinkTypeBotStart botStart)
            {
                NavigateToUsername(protoService, navigation, botStart.BotUsername, botStart.StartParameter, null, null, null, null);
            }
            else if (response is InternalLinkTypeBotStartInGroup botStartInGroup)
            {
                NavigateToUsername(protoService, navigation, botStartInGroup.BotUsername, botStartInGroup.StartParameter, null, null, null, null, PageKind.Search);
            }
            else if (response is InternalLinkTypeChangePhoneNumber)
            {

            }
            else if (response is InternalLinkTypeChatInvite)
            {
                NavigateToInviteLink(protoService, navigation, uri.ToString());
            }
            else if (response is InternalLinkTypeFilterSettings)
            {

            }
            else if (response is InternalLinkTypeGame game)
            {
                NavigateToUsername(protoService, navigation, game.BotUsername, null, null, null, null, game.GameShortName);
            }
            else if (response is InternalLinkTypeLanguagePack languagePack)
            {
                NavigateToLanguage(protoService, navigation, languagePack.LanguagePackId);
            }
            else if (response is InternalLinkTypeMessage)
            {
                NavigateToMessage(protoService, navigation, uri.ToString());
            }
            else if (response is InternalLinkTypeMessageDraft messageDraft)
            {
                NavigateToShare(messageDraft.Text, messageDraft.ContainsLink);
            }
            else if (response is InternalLinkTypePassportDataRequest)
            {

            }
            else if (response is InternalLinkTypePhoneNumberConfirmation phoneNumberConfirmation)
            {
                NavigateToConfirmPhone(protoService, phoneNumberConfirmation.PhoneNumber, phoneNumberConfirmation.Hash);
            }
            else if (response is InternalLinkTypeProxy proxy)
            {
                NavigateToProxy(protoService, proxy.Server, proxy.Port, proxy.Type);
            }
            else if (response is InternalLinkTypePublicChat publicChat)
            {
                NavigateToUsername(protoService, navigation, publicChat.ChatUsername, null, null, null, null, null);
            }
            else if (response is InternalLinkTypeQrCodeAuthentication)
            {

            }
            else if (response is InternalLinkTypeSettings)
            {

            }
            else if (response is InternalLinkTypeStickerSet stickerSet)
            {
                NavigateToStickerSet(stickerSet.StickerSetName);
            }
            else if (response is InternalLinkTypeTheme theme)
            {
                NavigateToTheme(protoService, theme.ThemeName);
            }
            else if (response is InternalLinkTypeThemeSettings)
            {

            }
            else if (response is InternalLinkTypeUnknownDeepLink)
            {
                NavigateToUnknownDeepLink(protoService, uri.ToString());
            }
            else if (response is InternalLinkTypeVoiceChat voiceChat)
            {
                NavigateToUsername(protoService, navigation, voiceChat.ChatUsername, null, voiceChat.InviteHash, null, null, null);
            }
        }

        private static async void NavigateToUnknownDeepLink(IProtoService protoService, string url)
        {
            var response = await protoService.SendAsync(new GetDeepLinkInfo(url));
            if (response is DeepLinkInfo info)
            {
                var confirm = await MessagePopup.ShowAsync(info.Text, Strings.Resources.AppName, Strings.Resources.OK, info.NeedUpdateApplication ? Strings.Resources.UpdateApp : null);
                if (confirm == ContentDialogResult.Secondary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?PFN=" + Package.Current.Id.FamilyName));
                }
            }
        }

        private static async void NavigateToBackground(IProtoService protoService, INavigationService navigation, string slug)
        {
            await new BackgroundPopup(slug).ShowQueuedAsync();

            //var response = await protoService.SendAsync(new SearchBackground(slug));
            //if (response is Background background)
            //{

            //}
        }

        private static async void NavigateToMessage(IProtoService protoService, INavigationService navigation, string url)
        {
            var response = await protoService.SendAsync(new GetMessageLinkInfo(url));
            if (response is MessageLinkInfo info && info.ChatId != 0)
            {
                if (info.Message != null)
                {
                    if (info.ForComment)
                    {
                        navigation.NavigateToThread(info.ChatId, info.Message.MessageThreadId, message: info.Message.Id);
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
                await MessagePopup.ShowAsync(Strings.Resources.LinkNotFound, Strings.Resources.AppName, Strings.Resources.OK);
            }
        }

        private static async void NavigateToTheme(IProtoService protoService, string slug)
        {
            await MessagePopup.ShowAsync(Strings.Resources.ThemeNotSupported, Strings.Resources.Theme, Strings.Resources.OK);
        }

        public static async void NavigateToLanguage(IProtoService protoService, INavigationService navigation, string languagePackId)
        {
            var response = await protoService.SendAsync(new GetLanguagePackInfo(languagePackId));
            if (response is LanguagePackInfo info)
            {
                if (info.Id == SettingsService.Current.LanguagePackId)
                {
                    var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.LanguageSame, info.Name), Strings.Resources.Language, Strings.Resources.OK, Strings.Resources.Settings);
                    if (confirm != ContentDialogResult.Secondary)
                    {
                        return;
                    }

                    navigation.Navigate(typeof(SettingsLanguagePage));
                }
                else if (info.TotalStringCount == 0)
                {
                    await MessagePopup.ShowAsync(string.Format(Strings.Resources.LanguageUnknownCustomAlert, info.Name), Strings.Resources.LanguageUnknownTitle, Strings.Resources.OK);
                }
                else
                {
                    var message = info.IsOfficial
                        ? Strings.Resources.LanguageAlert
                        : Strings.Resources.LanguageCustomAlert;

                    var start = message.IndexOf('[');
                    var end = message.IndexOf(']');
                    if (start != -1 && end != -1)
                    {
                        message = message.Insert(end + 1, $"({info.TranslationUrl})");
                    }

                    var confirm = await MessagePopup.ShowAsync(string.Format(message, info.Name, (int)Math.Ceiling(info.TranslatedStringCount / (float)info.TotalStringCount * 100)), Strings.Resources.LanguageTitle, Strings.Resources.Change, Strings.Resources.Cancel);
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

                        //TLWindowContext.GetForCurrentView().NavigationServices.Remove(NavigationService);
                        //BootStrapper.Current.NavigationService.Reset();

                        foreach (var window in WindowContext.ActiveWrappers)
                        {
                            window.Dispatcher.Dispatch(() =>
                            {
                                ResourceContext.GetForCurrentView().Reset();
                                ResourceContext.GetForViewIndependentUse().Reset();

                                if (window.Content is RootPage root)
                                {
                                    window.Dispatcher.Dispatch(() =>
                                    {
                                        root.UpdateComponent();
                                    });
                                }
                            });
                        }
                    }
                }
            }
        }

        public static async void NavigateToSendCode(IProtoService protoService, string phoneCode)
        {
            var state = protoService.GetAuthorizationState();
            if (state is AuthorizationStateWaitCode)
            {
                var firstName = string.Empty;
                var lastName = string.Empty;

                if (protoService.Options.TryGetValue("x_firstname", out string firstValue))
                {
                    firstName = firstValue;
                }

                if (protoService.Options.TryGetValue("x_lastname", out string lastValue))
                {
                    lastName = lastValue;
                }

                var response = await protoService.SendAsync(new CheckAuthenticationCode(phoneCode));
                if (response is Error error)
                {
                    if (error.TypeEquals(ErrorType.PHONE_NUMBER_INVALID))
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.Resources.InvalidPhoneNumber, Strings.Resources.OK);
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_EMPTY) || error.TypeEquals(ErrorType.PHONE_CODE_INVALID))
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.Resources.InvalidCode, Strings.Resources.OK);
                    }
                    else if (error.TypeEquals(ErrorType.PHONE_CODE_EXPIRED))
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.Resources.CodeExpired, Strings.Resources.OK);
                    }
                    else if (error.TypeEquals(ErrorType.FIRSTNAME_INVALID))
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.Resources.InvalidFirstName, Strings.Resources.OK);
                    }
                    else if (error.TypeEquals(ErrorType.LASTNAME_INVALID))
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.Resources.InvalidLastName, Strings.Resources.OK);
                    }
                    else if (error.Message.StartsWith("FLOOD_WAIT"))
                    {
                        await MessagePopup.ShowAsync(Strings.Resources.FloodWait, Strings.Resources.AppName, Strings.Resources.OK);
                    }
                    else if (error.Code != -1000)
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.Resources.AppName, Strings.Resources.OK);
                    }

                    Logs.Logger.Warning(Logs.LogTarget.API, "account.signIn error " + error);
                }
            }
            else
            {
                if (phoneCode.Length > 3)
                {
                    phoneCode = phoneCode.Substring(0, 3) + "-" + phoneCode.Substring(3);
                }

                await MessagePopup.ShowAsync(string.Format(Strings.Resources.OtherLoginCode, phoneCode), Strings.Resources.AppName, Strings.Resources.OK);
            }
        }

        public static async void NavigateToShare(FormattedText text, bool hasUrl)
        {
            await SharePopup.GetForCurrentView().ShowAsync(text);
        }

        public static async void NavigateToProxy(IProtoService protoService, string server, int port, ProxyType type)
        {
            string userText = string.Empty;
            string passText = string.Empty;
            string secretText = string.Empty;
            string secretInfo = string.Empty;

            if (type is ProxyTypeHttp http)
            {
                userText = !string.IsNullOrEmpty(http.Username) ? $"{Strings.Resources.UseProxyUsername}: {http.Username}\n" : string.Empty;
                passText = !string.IsNullOrEmpty(http.Password) ? $"{Strings.Resources.UseProxyPassword}: {http.Password}\n" : string.Empty;
            }
            else if (type is ProxyTypeSocks5 socks5)
            {
                userText = !string.IsNullOrEmpty(socks5.Username) ? $"{Strings.Resources.UseProxyUsername}: {socks5.Username}\n" : string.Empty;
                passText = !string.IsNullOrEmpty(socks5.Password) ? $"{Strings.Resources.UseProxyPassword}: {socks5.Password}\n" : string.Empty;
            }
            else if (type is ProxyTypeMtproto mtproto)
            {
                secretText = !string.IsNullOrEmpty(mtproto.Secret) ? $"{Strings.Resources.UseProxySecret}: {mtproto.Secret}\n" : string.Empty;
                secretInfo = !string.IsNullOrEmpty(mtproto.Secret) ? $"\n\n{Strings.Resources.UseProxyTelegramInfo2}" : string.Empty;
            }

            var confirm = await MessagePopup.ShowAsync($"{Strings.Resources.EnableProxyAlert}\n\n{Strings.Resources.UseProxyAddress}: {server}\n{Strings.Resources.UseProxyPort}: {port}\n{userText}{passText}{secretText}\n{Strings.Resources.EnableProxyAlert2}{secretInfo}", Strings.Resources.Proxy, Strings.Resources.ConnectingConnectProxy, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                protoService.Send(new AddProxy(server ?? string.Empty, port, true, type));
            }
        }

        public static async void NavigateToConfirmPhone(IProtoService protoService, string phone, string hash)
        {
            //var response = await protoService.SendConfirmPhoneCodeAsync(hash, false);
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
            await StickerSetPopup.GetForCurrentView().ShowAsync(text);
        }

        public static async void NavigateToUsername(IProtoService protoService, INavigationService navigation, string username, string accessToken, string voiceChat, string post, string comment, string game, PageKind kind = PageKind.Dialog)
        {
            if (username.StartsWith("@"))
            {
                username = username.Substring(1);
            }

            var response = await protoService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat)
            {
                if (game != null)
                {

                }
                else if (chat.Type is ChatTypePrivate privata)
                {
                    var user = protoService.GetUser(privata.UserId);
                    if (user != null && user.Type is UserTypeBot)
                    {
                        if (kind == PageKind.Search)
                        {
                            await SharePopup.GetForCurrentView().ShowAsync(user, accessToken);
                        }
                        else
                        {
                            navigation.NavigateToChat(chat, accessToken: accessToken);
                        }
                    }
                    else
                    {
                        navigation.Navigate(typeof(ProfilePage), chat.Id);
                    }
                }
                else
                {
                    if (long.TryParse(post, out long message))
                    {
                        if (long.TryParse(comment, out long threaded))
                        {
                            var info = await protoService.SendAsync(new GetMessageThread(chat.Id, message << 20)) as MessageThreadInfo;
                            if (info != null)
                            {
                                navigation.NavigateToThread(info.ChatId, info.MessageThreadId, message: threaded << 20);
                            }
                            else
                            {
                                navigation.NavigateToChat(chat, message: message << 20);
                            }
                        }
                        else
                        {
                            navigation.NavigateToChat(chat, message: message << 20);
                        }
                    }
                    else if (voiceChat != null)
                    {
                        navigation.NavigateToChat(chat, state: new NavigationState { { "voiceChat", voiceChat } });
                    }
                    else
                    {
                        navigation.NavigateToChat(chat);
                    }
                }
            }
            else
            {
                await MessagePopup.ShowAsync(Strings.Resources.NoUsernameFound, Strings.Resources.AppName, Strings.Resources.OK);
            }
        }

        public static async void NavigateToInviteLink(IProtoService protoService, INavigationService navigation, string link)
        {
            if (!link.StartsWith("http"))
            {
                link = "https://t.me/joinchat/" + link;
            }

            var response = await protoService.CheckChatInviteLinkAsync(link);
            if (response is ChatInviteLinkInfo info)
            {
                if (info.ChatId != 0)
                {
                    navigation.NavigateToChat(info.ChatId);
                }
                else
                {
                    var dialog = new JoinChatPopup(protoService, info);

                    var confirm = await dialog.ShowQueuedAsync();
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    var import = await protoService.SendAsync(new JoinChatByInviteLink(link));
                    if (import is Chat chat)
                    {
                        navigation.NavigateToChat(chat);
                    }
                    else if (import is Error error)
                    {
                        if (error.TypeEquals(ErrorType.FLOOD_WAIT))
                        {
                            await MessagePopup.ShowAsync(Strings.Resources.FloodWait, Strings.Resources.AppName, Strings.Resources.OK);
                        }
                        else if (error.TypeEquals(ErrorType.USERS_TOO_MUCH))
                        {
                            await MessagePopup.ShowAsync(Strings.Resources.JoinToGroupErrorFull, Strings.Resources.AppName, Strings.Resources.OK);
                        }
                        else
                        {
                            await MessagePopup.ShowAsync(Strings.Resources.JoinToGroupErrorNotExist, Strings.Resources.AppName, Strings.Resources.OK);
                        }
                    }
                }
            }
            else if (response is Error error)
            {
                if (error.TypeEquals(ErrorType.FLOOD_WAIT))
                {
                    await MessagePopup.ShowAsync(Strings.Resources.FloodWait, Strings.Resources.AppName, Strings.Resources.OK);
                }
                else
                {
                    await MessagePopup.ShowAsync(Strings.Resources.JoinToGroupErrorNotExist, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
        }

        public static string GetAccessToken(Dictionary<string, string> uriParams, out PageKind pageKind)
        {
            pageKind = PageKind.Dialog;

            var result = string.Empty;
            if (uriParams.ContainsKey("start"))
            {
                result = uriParams["start"];
            }
            else if (uriParams.ContainsKey("startgroup"))
            {
                pageKind = PageKind.Search;
                result = uriParams["startgroup"];
            }

            return result;
        }

        public enum PageKind
        {
            Dialog,
            Profile,
            Search
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

        public static async void OpenUrl(IProtoService protoService, INavigationService navigationService, string url, bool untrust)
        {
            if (TryCreateUri(url, out Uri uri))
            {
                if (IsTelegramUrl(uri))
                {
                    OpenTelegramUrl(protoService, navigationService, uri);
                }
                else
                {
                    //if (message?.Media is TLMessageMediaWebPage webpageMedia)
                    //{
                    //    if (webpageMedia.WebPage is TLWebPage webpage && webpage.HasCachedPage && webpage.Url.Equals(navigation))
                    //    {
                    //        var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                    //        if (service != null)
                    //        {
                    //            service.Navigate(typeof(InstantPage), webpageMedia);
                    //            return;
                    //        }
                    //    }
                    //}

                    if (untrust)
                    {
                        var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.OpenUrlAlert, url), Strings.Resources.OpenUrlTitle, Strings.Resources.Open, Strings.Resources.Cancel);
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

        public static void Hyperlink_ContextRequested(MessageViewModel message, UIElement sender, ContextRequestedEventArgs args)
        {
            var text = sender as RichTextBlock;
            if (args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                var length = text.SelectedText.Length;
                if (length > 0)
                {
                    var link = text.SelectedText;

                    var copy = new MenuFlyoutItem { Text = Strings.Resources.Copy, DataContext = link, Icon = new FontIcon { Glyph = Icons.DocumentCopy, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily } };
                    copy.Click += LinkCopy_Click;

                    var flyout = new MenuFlyout();
                    flyout.Items.Add(copy);

                    // We don't want to unfocus the text are when the context menu gets opened
                    flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });

                    args.Handled = true;
                }
                else
                {
                    var hyperlink = text.GetHyperlinkFromPoint(point);
                    if (hyperlink == null)
                    {
                        args.Handled = false;
                        return;
                    }

                    var link = GetEntityData(hyperlink);
                    if (link == null)
                    {
                        args.Handled = false;
                        return;
                    }

                    var type = GetEntityType(hyperlink);
                    //if (type == null)
                    //{
                    //    args.Handled = false;
                    //    return;
                    //}

                    var flyout = new MenuFlyout();

                    if (type is null or TextEntityTypeUrl or TextEntityTypeTextUrl)
                    {
                        var open = new MenuFlyoutItem { Text = Strings.Resources.Open, DataContext = link, Icon = new FontIcon { Glyph = Icons.OpenIn, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily } };

                        var action = GetEntityAction(hyperlink);
                        if (action != null)
                        {
                            open.Click += (s, args) => action();
                        }
                        else
                        {
                            open.Click += LinkOpen_Click;
                        }

                        flyout.Items.Add(open);
                    }

                    var copy = new MenuFlyoutItem { Text = Strings.Resources.Copy, DataContext = link, Icon = new FontIcon { Glyph = Icons.DocumentCopy, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily } };
                    copy.Click += LinkCopy_Click;
                    flyout.Items.Add(copy);

                    // We don't want to unfocus the text when the context menu gets opened
                    flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });

                    args.Handled = true;
                }
            }
            else
            {
                args.Handled = false;
            }
        }

        public static IList<MenuFlyoutItemBase> Hyperlink_ContextRequested(MessageViewModel message, RichTextBlock text, Point point)
        {
            if (point.X < 0 || point.Y < 0)
            {
                point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
            }

            var items = new List<MenuFlyoutItemBase>();

            var length = text.SelectedText.Length;
            if (length > 0)
            {
                var link = text.SelectedText;

                var copy = new MenuFlyoutItem { Text = Strings.Resources.Copy, DataContext = link, Icon = new FontIcon { Glyph = Icons.DocumentCopy, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily } };
                copy.Click += LinkCopy_Click;

                items.Add(copy);
            }
            else
            {
                var hyperlink = text.GetHyperlinkFromPoint(point);
                if (hyperlink == null)
                {
                    return items;
                }

                var link = GetEntityData(hyperlink);
                if (link == null)
                {
                    return items;
                }

                var type = GetEntityType(hyperlink);
                if (type is null or TextEntityTypeUrl or TextEntityTypeTextUrl)
                {
                    var open = new MenuFlyoutItem { Text = Strings.Resources.Open, DataContext = link, Icon = new FontIcon { Glyph = Icons.OpenIn, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily } };

                    var action = GetEntityAction(hyperlink);
                    if (action != null)
                    {
                        open.Click += (s, args) => action();
                    }
                    else
                    {
                        open.Click += LinkOpen_Click;
                    }

                    items.Add(open);
                }

                var copy = new MenuFlyoutItem { Text = Strings.Resources.Copy, DataContext = link, Icon = new FontIcon { Glyph = Icons.DocumentCopy, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily } };
                copy.Click += LinkCopy_Click;
                items.Add(copy);
            }

            return items;
        }

        public static void Hyperlink_ContextRequested(UIElement sender, string link, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                var open = new MenuFlyoutItem { Text = Strings.Resources.Open, DataContext = link, Icon = new FontIcon { Glyph = Icons.OpenIn, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily } };
                var copy = new MenuFlyoutItem { Text = Strings.Resources.Copy, DataContext = link, Icon = new FontIcon { Glyph = Icons.DocumentCopy, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily } };

                open.Click += LinkOpen_Click;
                copy.Click += LinkCopy_Click;

                var flyout = new MenuFlyout();
                flyout.Items.Add(open);
                flyout.Items.Add(copy);

                // We don't want to unfocus the text are when the context menu gets opened
                flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });

                args.Handled = true;
            }
        }

        private static async void LinkOpen_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            var entity = item.DataContext as string;

            if (TryCreateUri(entity, out Uri uri))
            {
                try
                {
                    await Launcher.LaunchUriAsync(uri);
                }
                catch { }
            }
        }

        private static void LinkCopy_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            var entity = item.DataContext as string;

            var dataPackage = new DataPackage();
            dataPackage.SetText(entity);
            ClipboardEx.TrySetContent(dataPackage);
        }

        public static void CopyText(string text)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            ClipboardEx.TrySetContent(dataPackage);
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
