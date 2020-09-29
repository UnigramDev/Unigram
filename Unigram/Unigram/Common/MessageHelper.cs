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
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class MessageHelper
    {
        #region Text
        public static string GetText(DependencyObject obj)
        {
            return (string)obj.GetValue(TextProperty);
        }

        public static void SetText(DependencyObject obj, string value)
        {
            obj.SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.RegisterAttached("Text", typeof(string), typeof(MessageHelper), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as FrameworkElement;
            var newValue = e.NewValue as string;
            var oldValue = e.OldValue as string;

            //sender.IsTextSelectionEnabled = false;
            sender.Visibility = string.IsNullOrWhiteSpace(newValue) ? Visibility.Collapsed : Visibility.Visible;

            if (oldValue == newValue) return;

            var foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x0f, 0x7d, 0xc7));
            var paragraph = new Span();
            paragraph.Inlines.Add(new Run { Text = newValue });
            //ReplaceAll(null, newValue, paragraph, foreground, true);

            if (sender is TextBlock textBlock)
            {
                textBlock.Inlines.Clear();
                textBlock.Inlines.Add(paragraph);
            }
            else if (sender is RichTextBlock richBlock)
            {
                var block = new Paragraph();
                block.Inlines.Add(paragraph);
                richBlock.Blocks.Clear();
                richBlock.Blocks.Add(block);
            }
        }
        #endregion

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
            if (c >= 0x5BE && c <= 0x10B7F)
            {
                if (c <= 0x85E)
                {
                    if (c == 0x5BE) hasRandALCat = true;
                    else if (c == 0x5C0) hasRandALCat = true;
                    else if (c == 0x5C3) hasRandALCat = true;
                    else if (c == 0x5C6) hasRandALCat = true;
                    else if (0x5D0 <= c && c <= 0x5EA) hasRandALCat = true;
                    else if (0x5F0 <= c && c <= 0x5F4) hasRandALCat = true;
                    else if (c == 0x608) hasRandALCat = true;
                    else if (c == 0x60B) hasRandALCat = true;
                    else if (c == 0x60D) hasRandALCat = true;
                    else if (c == 0x61B) hasRandALCat = true;
                    else if (0x61E <= c && c <= 0x64A) hasRandALCat = true;
                    else if (0x66D <= c && c <= 0x66F) hasRandALCat = true;
                    else if (0x671 <= c && c <= 0x6D5) hasRandALCat = true;
                    else if (0x6E5 <= c && c <= 0x6E6) hasRandALCat = true;
                    else if (0x6EE <= c && c <= 0x6EF) hasRandALCat = true;
                    else if (0x6FA <= c && c <= 0x70D) hasRandALCat = true;
                    else if (c == 0x710) hasRandALCat = true;
                    else if (0x712 <= c && c <= 0x72F) hasRandALCat = true;
                    else if (0x74D <= c && c <= 0x7A5) hasRandALCat = true;
                    else if (c == 0x7B1) hasRandALCat = true;
                    else if (0x7C0 <= c && c <= 0x7EA) hasRandALCat = true;
                    else if (0x7F4 <= c && c <= 0x7F5) hasRandALCat = true;
                    else if (c == 0x7FA) hasRandALCat = true;
                    else if (0x800 <= c && c <= 0x815) hasRandALCat = true;
                    else if (c == 0x81A) hasRandALCat = true;
                    else if (c == 0x824) hasRandALCat = true;
                    else if (c == 0x828) hasRandALCat = true;
                    else if (0x830 <= c && c <= 0x83E) hasRandALCat = true;
                    else if (0x840 <= c && c <= 0x858) hasRandALCat = true;
                    else if (c == 0x85E) hasRandALCat = true;
                }
                else if (c == 0x200F) hasRandALCat = true;
                else if (c >= 0xFB1D)
                {
                    if (c == 0xFB1D) hasRandALCat = true;
                    else if (0xFB1F <= c && c <= 0xFB28) hasRandALCat = true;
                    else if (0xFB2A <= c && c <= 0xFB36) hasRandALCat = true;
                    else if (0xFB38 <= c && c <= 0xFB3C) hasRandALCat = true;
                    else if (c == 0xFB3E) hasRandALCat = true;
                    else if (0xFB40 <= c && c <= 0xFB41) hasRandALCat = true;
                    else if (0xFB43 <= c && c <= 0xFB44) hasRandALCat = true;
                    else if (0xFB46 <= c && c <= 0xFBC1) hasRandALCat = true;
                    else if (0xFBD3 <= c && c <= 0xFD3D) hasRandALCat = true;
                    else if (0xFD50 <= c && c <= 0xFD8F) hasRandALCat = true;
                    else if (0xFD92 <= c && c <= 0xFDC7) hasRandALCat = true;
                    else if (0xFDF0 <= c && c <= 0xFDFC) hasRandALCat = true;
                    else if (0xFE70 <= c && c <= 0xFE74) hasRandALCat = true;
                    else if (0xFE76 <= c && c <= 0xFEFC) hasRandALCat = true;
                    else if (0x10800 <= c && c <= 0x10805) hasRandALCat = true;
                    else if (c == 0x10808) hasRandALCat = true;
                    else if (0x1080A <= c && c <= 0x10835) hasRandALCat = true;
                    else if (0x10837 <= c && c <= 0x10838) hasRandALCat = true;
                    else if (c == 0x1083C) hasRandALCat = true;
                    else if (0x1083F <= c && c <= 0x10855) hasRandALCat = true;
                    else if (0x10857 <= c && c <= 0x1085F) hasRandALCat = true;
                    else if (0x10900 <= c && c <= 0x1091B) hasRandALCat = true;
                    else if (0x10920 <= c && c <= 0x10939) hasRandALCat = true;
                    else if (c == 0x1093F) hasRandALCat = true;
                    else if (c == 0x10A00) hasRandALCat = true;
                    else if (0x10A10 <= c && c <= 0x10A13) hasRandALCat = true;
                    else if (0x10A15 <= c && c <= 0x10A17) hasRandALCat = true;
                    else if (0x10A19 <= c && c <= 0x10A33) hasRandALCat = true;
                    else if (0x10A40 <= c && c <= 0x10A47) hasRandALCat = true;
                    else if (0x10A50 <= c && c <= 0x10A58) hasRandALCat = true;
                    else if (0x10A60 <= c && c <= 0x10A7F) hasRandALCat = true;
                    else if (0x10B00 <= c && c <= 0x10B35) hasRandALCat = true;
                    else if (0x10B40 <= c && c <= 0x10B55) hasRandALCat = true;
                    else if (0x10B58 <= c && c <= 0x10B72) hasRandALCat = true;
                    else if (0x10B78 <= c && c <= 0x10B7F) hasRandALCat = true;
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

        public static async void OpenTelegramScheme(IProtoService protoService, INavigationService navigation, Uri scheme)
        {
            string username = null;
            string message = null;
            string group = null;
            string sticker = null;
            string[] instantView = null;
            Dictionary<string, object> auth = null;
            string botUser = null;
            string botChat = null;
            string comment = null;
            string phone = null;
            string game = null;
            string phoneHash = null;
            string post = null;
            string server = null;
            string port = null;
            string user = null;
            string pass = null;
            string secret = null;
            string phoneCode = null;
            string lang = null;
            string channel = null;
            bool hasUrl = false;

            var query = scheme.Query.ParseQueryString();
            if (scheme.AbsoluteUri.StartsWith("tg:resolve") || scheme.AbsoluteUri.StartsWith("tg://resolve"))
            {
                username = query.GetParameter("domain");

                if (string.Equals(username, "telegrampassport", StringComparison.OrdinalIgnoreCase))
                {
                    username = null;
                    auth = new Dictionary<string, object>();
                    var scope = query.GetParameter("scope");
                    if (!string.IsNullOrEmpty(scope) && scope.StartsWith("{") && scope.EndsWith("}"))
                    {
                        auth.Add("nonce", query.GetParameter("nonce"));
                    }
                    else
                    {
                        auth.Add("payload", query.GetParameter("payload"));
                    }

                    auth.Add("bot_id", int.Parse(query.GetParameter("bot_id")));
                    auth.Add("scope", scope);
                    auth.Add("public_key", query.GetParameter("public_key"));
                    auth.Add("callback_url", query.GetParameter("callback_url"));
                }
                else
                {
                    botUser = query.GetParameter("start");
                    botChat = query.GetParameter("startgroup");
                    game = query.GetParameter("game");
                    post = query.GetParameter("post");
                    comment = query.GetParameter("comment");
                }
            }
            else if (scheme.AbsoluteUri.StartsWith("tg:join") || scheme.AbsoluteUri.StartsWith("tg://join"))
            {
                group = query.GetParameter("invite");
            }
            else if (scheme.AbsoluteUri.StartsWith("tg:addstickers") || scheme.AbsoluteUri.StartsWith("tg://addstickers"))
            {
                sticker = query.GetParameter("set");
            }
            else if (scheme.AbsoluteUri.StartsWith("tg:msg") || scheme.AbsoluteUri.StartsWith("tg://msg") || scheme.AbsoluteUri.StartsWith("tg://share") || scheme.AbsoluteUri.StartsWith("tg:share"))
            {
                message = query.GetParameter("url");
                if (message == null)
                {
                    message = "";
                }
                if (query.GetParameter("text") != null)
                {
                    if (message.Length > 0)
                    {
                        hasUrl = true;
                        message += "\n";
                    }
                    message += query.GetParameter("text");
                }
                if (message.Length > 4096 * 4)
                {
                    message = message.Substring(0, 4096 * 4);
                }
                while (message.EndsWith("\n"))
                {
                    message = message.Substring(0, message.Length - 1);
                }
            }
            else if (scheme.AbsoluteUri.StartsWith("tg:confirmphone") || scheme.AbsoluteUri.StartsWith("tg://confirmphone"))
            {
                phone = query.GetParameter("phone");
                phoneHash = query.GetParameter("hash");
            }
            else if (scheme.AbsoluteUri.StartsWith("tg:passport") || scheme.AbsoluteUri.StartsWith("tg://passport") || scheme.AbsoluteUri.StartsWith("tg:secureid") || scheme.AbsoluteUri.StartsWith("tg://secureid"))
            {
                //url = url.replace("tg:passport", "tg://telegram.org").replace("tg://passport", "tg://telegram.org").replace("tg:secureid", "tg://telegram.org");
                //data = Uri.parse(url);
                //auth = new HashMap<>();
                //String scope = data.getQueryParameter("scope");
                //if (!TextUtils.isEmpty(scope) && scope.startsWith("{") && scope.endsWith("}"))
                //{
                //    auth.put("nonce", data.getQueryParameter("nonce"));
                //}
                //else
                //{
                //    auth.put("payload", data.getQueryParameter("payload"));
                //}
                //auth.put("bot_id", data.getQueryParameter("bot_id"));
                //auth.put("scope", scope);
                //auth.put("public_key", data.getQueryParameter("public_key"));
                //auth.put("callback_url", data.getQueryParameter("callback_url"));
            }
            else if (scheme.AbsoluteUri.StartsWith("tg:socks") || scheme.AbsoluteUri.StartsWith("tg://socks") || scheme.AbsoluteUri.StartsWith("tg:proxy") || scheme.AbsoluteUri.StartsWith("tg://proxy"))
            {
                server = query.GetParameter("server");
                port = query.GetParameter("port");
                user = query.GetParameter("user");
                pass = query.GetParameter("pass");
                secret = query.GetParameter("secret");
            }
            else if (scheme.AbsoluteUri.StartsWith("tg:login") || scheme.AbsoluteUri.StartsWith("tg://login"))
            {
                phoneCode = query.GetParameter("code");
            }
            //tg://setlanguage?lang=he-beta
            else if (scheme.AbsoluteUri.StartsWith("tg:setlanguage") || scheme.AbsoluteUri.StartsWith("tg://setlanguage"))
            {
                lang = query.GetParameter("lang");
            }
            else if (scheme.AbsoluteUri.StartsWith("tg:privatepost") || scheme.AbsoluteUri.StartsWith("tg://privatepost"))
            {
                channel = query.GetParameter("channel");
                post = query.GetParameter("post");
            }

            if (message != null && message.StartsWith("@"))
            {
                message = " " + message;
            }

            if (phone != null || phoneHash != null)
            {
                NavigateToConfirmPhone(protoService, phone, phoneHash);
            }
            if (server != null && int.TryParse(port, out int portCode))
            {
                NavigateToProxy(protoService, server, portCode, user, pass, secret);
            }
            else if (group != null)
            {
                NavigateToInviteLink(protoService, navigation, group);
            }
            else if (sticker != null)
            {
                NavigateToStickerSet(sticker);
            }
            else if (username != null)
            {
                NavigateToUsername(protoService, navigation, username, botUser ?? botChat, post, comment, game);
            }
            else if (message != null)
            {
                NavigateToShare(message, hasUrl);
            }
            else if (phoneCode != null)
            {
                NavigateToSendCode(protoService, phoneCode);
            }
            else if (lang != null)
            {
                NavigateToLanguage(protoService, navigation, lang);
            }
            else if (channel != null && post != null)
            {
                NavigateToMessage(protoService, navigation, channel, post);
            }
            else
            {
                var response = await protoService.SendAsync(new GetDeepLinkInfo(scheme.AbsoluteUri));
                if (response is DeepLinkInfo info)
                {
                    var confirm = await MessagePopup.ShowAsync(info.Text, Strings.Resources.AppName, Strings.Resources.OK, info.NeedUpdateApplication ? Strings.Resources.UpdateApp : null);
                    if (confirm == ContentDialogResult.Secondary)
                    {
                        await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?PFN=" + Package.Current.Id.FamilyName));
                    }
                }
            }
        }

        public static void OpenTelegramUrl(IProtoService protoService, INavigationService navigation, Uri uri)
        {
            if (IsTelegramScheme(uri))
            {
                OpenTelegramScheme(protoService, navigation, uri);
            }
            else
            {
                var url = uri.ToString();
                if (url.Contains("telegra.ph"))
                {
                    navigation.NavigateToInstant(url);
                }
                else if (url.Contains("joinchat"))
                {
                    var index = url.TrimEnd('/').LastIndexOf("/", StringComparison.OrdinalIgnoreCase);
                    if (index != -1)
                    {
                        var text = url.Substring(index).Replace("/", string.Empty);
                        if (!string.IsNullOrEmpty(text))
                        {
                            NavigateToInviteLink(protoService, navigation, text);
                        }
                    }
                }
                else if (url.Contains("addstickers"))
                {
                    var index = url.TrimEnd('/').LastIndexOf("/", StringComparison.OrdinalIgnoreCase);
                    if (index != -1)
                    {
                        var text = url.Substring(index).Replace("/", string.Empty);
                        if (!string.IsNullOrEmpty(text))
                        {
                            NavigateToStickerSet(text);
                        }
                    }
                }
                else
                {
                    var query = url.ParseQueryString();

                    var accessToken = GetAccessToken(query, out PageKind pageKind);
                    var post = query.GetParameter("post");
                    var game = query.GetParameter("game");
                    var comment = query.GetParameter("comment");
                    var result = url.StartsWith("http") ? url : ("https://" + url);

                    if (uri.Segments.Length >= 2)
                    {
                        var username = uri.Segments[1].Replace("/", string.Empty);
                        if (string.IsNullOrEmpty(post) && uri.Segments.Length >= 3)
                        {
                            post = uri.Segments[2].Replace("/", string.Empty);
                        }
                        if (!string.IsNullOrEmpty(username))
                        {
                            if (username.Equals("confirmphone", StringComparison.OrdinalIgnoreCase))
                            {
                                var phone = query.GetParameter("phone");
                                var hash = query.GetParameter("hash");

                                NavigateToConfirmPhone(null, phone, hash);
                            }
                            else if (username.Equals("login", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(post))
                            {
                                NavigateToSendCode(protoService, post);
                            }
                            else if (username.Equals("iv", StringComparison.OrdinalIgnoreCase))
                            {
                                navigation.NavigateToInstant(url);
                            }
                            else if (username.Equals("proxy", StringComparison.OrdinalIgnoreCase) || username.Equals("socks", StringComparison.OrdinalIgnoreCase))
                            {
                                var server = query.GetParameter("server");
                                var port = query.GetParameter("port");
                                var user = query.GetParameter("user");
                                var pass = query.GetParameter("pass");
                                var secret = query.GetParameter("secret");

                                if (server != null && int.TryParse(port, out int portCode))
                                {
                                    NavigateToProxy(protoService, server, portCode, user, pass, secret);
                                }
                            }
                            else if (username.Equals("share"))
                            {
                                var hasUrl = false;
                                var text = query.GetParameter("url");
                                if (text == null)
                                {
                                    text = "";
                                }
                                if (query.GetParameter("text") != null)
                                {
                                    if (text.Length > 0)
                                    {
                                        hasUrl = true;
                                        text += "\n";
                                    }
                                    text += query.GetParameter("text");
                                }
                                if (text.Length > 4096 * 4)
                                {
                                    text = text.Substring(0, 4096 * 4);
                                }
                                while (text.EndsWith("\n"))
                                {
                                    text = text.Substring(0, text.Length - 1);
                                }


                                NavigateToShare(text, hasUrl);
                            }
                            else if (username.Equals("setlanguage", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(post))
                            {
                                NavigateToLanguage(protoService, navigation, post);
                            }
                            else if (username.Equals("c", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(post) && uri.Segments.Length >= 4)
                            {
                                NavigateToMessage(protoService, navigation, post, uri.Segments[3].Replace("/", string.Empty));
                            }
                            else if (username.Equals("bg", StringComparison.OrdinalIgnoreCase))
                            {
                                NavigateToBackground(protoService, navigation, post + uri.Query);
                            }
                            else
                            {
                                NavigateToUsername(protoService, navigation, username, accessToken, post, string.IsNullOrEmpty(comment) ? null : comment, string.IsNullOrEmpty(game) ? null : game, pageKind);
                            }
                        }
                    }
                }
            }
        }

        private static void NavigateToBackground(IProtoService protoService, INavigationService navigation, string slug)
        {
            navigation.Navigate(typeof(BackgroundPage), slug);

            //var response = await protoService.SendAsync(new SearchBackground(slug));
            //if (response is Background background)
            //{

            //}
        }

        private static async void NavigateToMessage(IProtoService protoService, INavigationService navigation, string post, string message)
        {
            if (int.TryParse(post, out int supergroup) && long.TryParse(message, out long msgId))
            {
                var response = await protoService.SendAsync(new CreateSupergroupChat(supergroup, false));
                if (response is Chat chat)
                {
                    navigation.NavigateToChat(chat, msgId << 20);
                }
                else
                {
                    await MessagePopup.ShowAsync(Strings.Resources.LinkNotFound, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
            else
            {
                // TODO: error
            }
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

                    Logs.Logger.Warning(Logs.Target.API, "account.signIn error " + error);
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

        public static async void NavigateToShare(string text, bool hasUrl)
        {
            //await ShareView.GetForCurrentView().ShowAsync(text, hasUrl);
        }

        public static async void NavigateToProxy(IProtoService protoService, string server, int port, string username, string password, string secret)
        {
            var userText = username != null ? $"{Strings.Resources.UseProxyUsername}: {username}\n" : string.Empty;
            var passText = password != null ? $"{Strings.Resources.UseProxyPassword}: {password}\n" : string.Empty;
            var secretText = secret != null ? $"{Strings.Resources.UseProxySecret}: {secret}\n" : string.Empty;
            var secretInfo = secret != null ? $"\n\n{Strings.Resources.UseProxyTelegramInfo2}" : string.Empty;
            var confirm = await MessagePopup.ShowAsync($"{Strings.Resources.EnableProxyAlert}\n\n{Strings.Resources.UseProxyAddress}: {server}\n{Strings.Resources.UseProxyPort}: {port}\n{userText}{passText}{secretText}\n{Strings.Resources.EnableProxyAlert2}{secretInfo}", Strings.Resources.Proxy, Strings.Resources.ConnectingConnectProxy, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                ProxyType type;
                if (secret != null)
                {
                    type = new ProxyTypeMtproto(secret);
                }
                else
                {
                    type = new ProxyTypeSocks5(username ?? string.Empty, password ?? string.Empty);
                }

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

        public static async void NavigateToUsername(IProtoService protoService, INavigationService navigation, string username, string accessToken, string post, string comment, string game, PageKind kind = PageKind.Dialog)
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

            var response = await protoService.SendAsync(new CheckChatInviteLink(link));
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
                        await Windows.System.Launcher.LaunchUriAsync(uri);
                    }
                    catch { }
                }
            }
        }

        #region Entity

        public static async void Hyperlink_ContextRequested(MessageViewModel message, UIElement sender, ContextRequestedEventArgs args)
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

                    var copy = new MenuFlyoutItem { Text = Strings.Resources.Copy, DataContext = link, Icon = new FontIcon { Glyph = Icons.Copy } };
                    copy.Click += LinkCopy_Click;

                    var flyout = new MenuFlyout();
                    flyout.Items.Add(copy);

                    if (ApiInformation.IsTypePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutShowOptions"))
                    {
                        // We don't want to unfocus the text are when the context menu gets opened
                        flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });
                    }
                    else
                    {
                        flyout.ShowAt(sender, point);
                    }

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

                    if (type == null || type is TextEntityTypeUrl || type is TextEntityTypeTextUrl)
                    {
                        var open = new MenuFlyoutItem { Text = Strings.Resources.Open, DataContext = link, Icon = new FontIcon { Glyph = Icons.OpenInNewWindow } };
                        open.Click += LinkOpen_Click;
                        flyout.Items.Add(open);
                    }
                    else if (type is TextEntityTypeBankCardNumber)
                    {
                        var response = await message.ProtoService.SendAsync(new GetBankCardInfo(link));
                        if (response is BankCardInfo info)
                        {
                            var title = new MenuFlyoutItem { Text = info.Title, IsEnabled = false, Icon = new FontIcon { Glyph = Icons.OpenInNewWindow } };
                            flyout.Items.Add(title);

                            foreach (var action in info.Actions)
                            {
                                var open = new MenuFlyoutItem { Text = action.Text, DataContext = link, Icon = new FontIcon { Glyph = Icons.OpenInNewWindow } };
                                open.Click += LinkOpen_Click;
                                flyout.Items.Add(open);
                            }
                        }
                    }

                    var copy = new MenuFlyoutItem { Text = Strings.Resources.Copy, DataContext = link, Icon = new FontIcon { Glyph = Icons.Copy } };
                    copy.Click += LinkCopy_Click;
                    flyout.Items.Add(copy);

                    if (ApiInformation.IsTypePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutShowOptions"))
                    {
                        // We don't want to unfocus the text are when the context menu gets opened
                        flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });
                    }
                    else
                    {
                        flyout.ShowAt(sender, point);
                    }

                    args.Handled = true;
                }
            }
            else
            {
                args.Handled = false;
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

                var open = new MenuFlyoutItem { Text = Strings.Resources.Open, DataContext = link, Icon = new FontIcon { Glyph = Icons.OpenInNewWindow } };
                var copy = new MenuFlyoutItem { Text = Strings.Resources.Copy, DataContext = link, Icon = new FontIcon { Glyph = Icons.Copy } };

                open.Click += LinkOpen_Click;
                copy.Click += LinkCopy_Click;

                var flyout = new MenuFlyout();
                flyout.Items.Add(open);
                flyout.Items.Add(copy);

                if (ApiInformation.IsTypePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutShowOptions"))
                {
                    // We don't want to unfocus the text are when the context menu gets opened
                    flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });
                }
                else
                {
                    flyout.ShowAt(sender, point);
                }

                args.Handled = true;
            }
        }

        private async static void LinkOpen_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            var entity = item.DataContext as string;

            if (TryCreateUri(entity, out Uri uri))
            {
                await Launcher.LaunchUriAsync(uri);
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
