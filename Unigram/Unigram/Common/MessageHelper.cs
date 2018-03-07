using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Template10.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Views;
using Unigram.ViewModels;
using Unigram.Views.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Unigram.Views.SignIn;
using Windows.Foundation;
using Windows.UI.Xaml.Input;
using Unigram.ViewModels.Dialogs;
using Unigram.Services;
using Template10.Services.NavigationService;
using Telegram.Td.Api;
using Unigram.Entities;

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

        public static bool IsTelegramUrl(Uri uri)
        {
            if (Constants.TelegramHosts.Contains(uri.Host))
            {
                return true;
            }

            //var config = InMemoryCacheService.Current.GetConfig();
            //if (config != null && Uri.TryCreate(config.MeUrlPrefix, UriKind.Absolute, out Uri meUri))
            //{
            //    return uri.Host.Equals(meUri.Host, StringComparison.OrdinalIgnoreCase);
            //}

            return false;
        }

        public static void OpenTelegramUrl(IProtoService protoService, INavigationService navigation, string url)
        {
            // TODO: in-app navigation
            if (url.Contains("joinchat"))
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
                var result = url.StartsWith("http") ? url : ("https://" + url);

                if (Uri.TryCreate(result, UriKind.Absolute, out Uri uri))
                {
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
                            else if (username.Equals("iv", StringComparison.OrdinalIgnoreCase))
                            {
                                navigation.Navigate(typeof(InstantPage), url);
                            }
                            else if (username.Equals("socks", StringComparison.OrdinalIgnoreCase))
                            {
                                var server = query.GetParameter("server");
                                var port = query.GetParameter("port");
                                var user = query.GetParameter("user");
                                var pass = query.GetParameter("pass");

                                if (server != null && int.TryParse(port, out int portCode))
                                {
                                    NavigateToSocks(protoService, server, portCode, user, pass);
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
                            else
                            {
                                NavigateToUsername(protoService, navigation, username, accessToken, post, string.IsNullOrEmpty(game) ? null : game);
                            }
                        }
                    }
                }
            }
        }

        public static async void NavigateToShare(string text, bool hasUrl)
        {
            await ForwardView.GetForCurrentView().ShowAsync(text, hasUrl);
        }

        public static async void NavigateToSocks(IProtoService protoService, string server, int port, string username, string password)
        {
            var userText = username != null ? string.Format($"{Strings.Resources.UseProxyUsername}: {username}\n", username) : string.Empty;
            var passText = password != null ? string.Format($"{Strings.Resources.UseProxyPassword}: {password}\n", password) : string.Empty;
            var confirm = await TLMessageDialog.ShowAsync($"{Strings.Resources.EnableProxyAlert}\n\n{Strings.Resources.UseProxyAddress}: {server}\n{Strings.Resources.UseProxyPort}: {port}\n{userText}{passText}\n{Strings.Resources.EnableProxyAlert2}", Strings.Resources.Proxy, Strings.Resources.ConnectingToProxyEnable, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var proxy = ApplicationSettings.Current.Proxy;
                proxy.Server = server = server ?? string.Empty;
                proxy.Port = port;
                proxy.Username = username = username ?? string.Empty;
                proxy.Password = password = password ?? string.Empty;
                proxy.IsEnabled = true;

                protoService.Send(new SetProxy(new ProxySocks5(server, port, username, password)));
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
            //    //Telegram.Api.Helpers.Execute.ShowDebugMessage(string.Format("account.sendConfirmPhoneCode error {0}", error));
            //};
        }

        public static async void NavigateToStickerSet(string text)
        {
            await StickerSetView.GetForCurrentView().ShowAsync(text);
        }

        public static async void NavigateToUsername(IProtoService protoService, INavigationService navigation, string username, string accessToken, string post, string game)
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
                        navigation.NavigateToChat(chat, accessToken: accessToken);
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
                        navigation.NavigateToChat(chat, message: message << 20);
                    }
                    else
                    {
                        navigation.NavigateToChat(chat);
                    }
                }
            }
            else
            {
                await new TLMessageDialog("No user found with this username", "Argh!").ShowQueuedAsync();
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
                    var dialog = new JoinChatView(protoService, info);

                    var confirm = await dialog.ShowAsync();
                    if (confirm == ContentDialogBaseResult.OK)
                    {
                        var import = await protoService.SendAsync(new JoinChatByInviteLink(link));
                        if (import is Ok)
                        {
                            await TLMessageDialog.ShowAsync("Joined", Strings.Resources.AppName, Strings.Resources.OK);
                        }
                        else if (import is Error error)
                        {
                            if (!error.CodeEquals(ErrorCode.BAD_REQUEST))
                            {
                                Execute.ShowDebugMessage("messages.importChatInvite error " + error);
                                return;
                            }
                            if (error.TypeEquals(ErrorType.INVITE_HASH_EMPTY) || error.TypeEquals(ErrorType.INVITE_HASH_INVALID) || error.TypeEquals(ErrorType.INVITE_HASH_EXPIRED))
                            {
                                //MessageBox.Show(Strings.Additional.GroupNotExistsError, Strings.Additional.Error, 0);
                                return;
                            }
                            else if (error.TypeEquals(ErrorType.USERS_TOO_MUCH))
                            {
                                //MessageBox.Show(Strings.Additional.UsersTooMuch, Strings.Additional.Error, 0);
                                return;
                            }
                            else if (error.TypeEquals(ErrorType.BOTS_TOO_MUCH))
                            {
                                //MessageBox.Show(Strings.Additional.BotsTooMuch, Strings.Additional.Error, 0);
                                return;
                            }
                            else if (error.TypeEquals(ErrorType.USER_ALREADY_PARTICIPANT))
                            {
                                return;
                            }

                            Execute.ShowDebugMessage("messages.importChatInvite error " + error);
                        }
                    }
                }
            }
            else if (response is Error error)
            {
                if (!error.CodeEquals(ErrorCode.BAD_REQUEST))
                {
                    Execute.ShowDebugMessage("messages.checkChatInvite error " + error);
                    return;
                }
                if (error.TypeEquals(ErrorType.INVITE_HASH_EMPTY) || error.TypeEquals(ErrorType.INVITE_HASH_INVALID) || error.TypeEquals(ErrorType.INVITE_HASH_EXPIRED))
                {
                    //MessageBox.Show(Strings.Additional.GroupNotExistsError, Strings.Additional.Error, 0);
                    await TLMessageDialog.ShowAsync("This invite link is broken or has expired.", "Warning", "OK");
                    return;
                }

                Execute.ShowDebugMessage("messages.checkChatInvite error " + error);
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

        #region Entity

        public static void Hyperlink_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
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

                    var copy = new MenuFlyoutItem { Text = Strings.Resources.Copy, DataContext = link };

                    copy.Click += LinkCopy_Click;

                    var flyout = new MenuFlyout();
                    flyout.Items.Add(copy);
                    flyout.ShowAt(sender, point);

                    args.Handled = true;
                }
                else
                {
                    var hyperlink = text.GetHyperlinkFromPoint(point);
                    if (hyperlink == null)
                    {
                        return;
                    }

                    var link = GetEntity(hyperlink);
                    if (link == null)
                    {
                        return;
                    }

                    var open = new MenuFlyoutItem { Text = Strings.Resources.Open, DataContext = link };
                    var copy = new MenuFlyoutItem { Text = Strings.Resources.Copy, DataContext = link };

                    open.Click += LinkOpen_Click;
                    copy.Click += LinkCopy_Click;

                    var flyout = new MenuFlyout();
                    flyout.Items.Add(open);
                    flyout.Items.Add(copy);
                    flyout.ShowAt(sender, point);

                    args.Handled = true;
                }
            }
        }

        private async static void LinkOpen_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuFlyoutItem;
            var entity = item.DataContext as string;

            var url = entity;
            if (entity.StartsWith("http") == false)
            {
                url = "http://" + url;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
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

        public static string GetEntity(DependencyObject obj)
        {
            return (string)obj.GetValue(EntityProperty);
        }

        public static void SetEntity(DependencyObject obj, string value)
        {
            obj.SetValue(EntityProperty, value);
        }

        public static readonly DependencyProperty EntityProperty =
            DependencyProperty.RegisterAttached("Entity", typeof(string), typeof(MessageHelper), new PropertyMetadata(null));

        #endregion
    }

    public enum MessageCommandType
    {
        Invoke,
        Mention,
        Hashtag
    }
}
