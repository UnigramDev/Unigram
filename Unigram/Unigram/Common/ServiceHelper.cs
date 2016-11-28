using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Views;
using Windows.Globalization.DateTimeFormatting;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class ServiceHelper
    {
        private static readonly Dictionary<Type, Func<TLMessageActionBase, int, string, Paragraph>> _actionsCache;

        static ServiceHelper()
        {
            _actionsCache = new Dictionary<Type, Func<TLMessageActionBase, int, string, Paragraph>>();
            _actionsCache.Add(typeof(TLMessageActionDate), (TLMessageActionBase action, int fromUserId, string fromUserFullName) => ReplaceLinks(new DateTimeFormatter("day month").Format(Utils.UnixTimestampToDateTime(((TLMessageActionDate)action).Date))));
            //_actionsCache.Add(typeof(TLMessageActionEmpty), (TLMessageActionBase action, int fromUserId, string fromUserFullName) => ReplaceLinks(Resources.MessageActionEmpty));
            //_actionsCache.Add(typeof(TLMessageActionChatCreate), (TLMessageActionBase action, int fromUserId, string fromUserFullName) => ReplaceLinks(Resources.MessageActionChatCreate, new[] { fromUserFullName, ((TLMessageActionChatCreate)action).Title }, new[] { fromUserId }));
            //_actionsCache.Add(typeof(TLMessageActionChatEditPhoto), (TLMessageActionBase action, int fromUserId, string fromUserFullName) => ReplaceLinks(Resources.MessageActionChatEditPhoto, new[] { fromUserFullName }, new[] { fromUserId }));
            //_actionsCache.Add(typeof(TLMessageActionChatEditTitle), (TLMessageActionBase action, int fromUserId, string fromUserFullName) => ReplaceLinks(Resources.MessageActionChatEditTitle, new[] { fromUserFullName, ((TLMessageActionChatEditTitle)action).Title }, new[] { fromUserId }));
            //_actionsCache.Add(typeof(TLMessageActionChatDeletePhoto), (TLMessageActionBase action, int fromUserId, string fromUserFullName) => ReplaceLinks(Resources.MessageActionChatDeletePhoto, new[] { fromUserFullName }, new[] { fromUserId }));
            //_actionsCache.Add(typeof(TLMessageActionChatAddUser), (TLMessageActionBase action, int fromUserId, string fromUserFullName) =>
            //{
            //    var userId = ((TLMessageActionChatAddUser)action).UserId;
            //    var user = InMemoryCacheService.Current.GetUser(userId) as TLUser;
            //    var username = (user != null) ? user.FullName : Resources.User;

            //    return ReplaceLinks(Resources.MessageActionChatAddUser, new[] { fromUserFullName, username }, new[] { fromUserId, userId.Value });
            //});
            //_actionsCache.Add(typeof(TLMessageActionChatDeleteUser), (TLMessageActionBase action, int fromUserId, string fromUserFullName) =>
            //{
            //    var userId = ((TLMessageActionChatDeleteUser)action).UserId;
            //    var user = InMemoryCacheService.Current.GetUser(userId) as TLUser;
            //    var username = (user != null) ? user.FullName : Resources.User;
            //    if (userId == fromUserId)
            //    {
            //        return ReplaceLinks(Resources.MessageActionUserLeftGroup, new[] { fromUserFullName }, new[] { fromUserId });
            //    }

            //    return ReplaceLinks(Resources.MessageActionChatDeleteUser, new[] { fromUserFullName, username }, new[] { fromUserId, userId });
            //});
            //_actionsCache.Add(typeof(TLMessageActionUnreadMessages), (TLMessageActionBase action, int fromUserId, string fromUserFullName) => ReplaceLinks(Resources.UnreadMessages));
            //_actionsCache.Add(typeof(TLMessageActionContactRegistered), (TLMessageActionBase action, int fromUserId, string fromUserFullName) =>
            //{
            //    var userId = ((TLMessageActionContactRegistered)action).UserId;
            //    var user = InMemoryCacheService.Current.GetUser(userId) as TLUser;
            //    var username = (user != null) ? user.FirstName.ToString() : Resources.User;
            //    if (string.IsNullOrEmpty(username) && user != null)
            //    {
            //        username = user.FullName;
            //    }

            //    return ReplaceLinks(Resources.ContactRegistered, new[] { username }, new[] { userId.Value });
            //});
            //_actionsCache.Add(typeof(TLMessageActionChatJoinedByLink), delegate (TLMessageActionBase action, int fromUserId, string fromUserFullName)
            //{
            //    var inviterId = ((TLMessageActionChatJoinedByLink)action).InviterId;
            //    var user = InMemoryCacheService.Current.GetUser(inviterId) as TLUser;
            //    var username = (user != null) ? user.FullName : Resources.User;
            //    return ReplaceLinks(Resources.MessageActionChatJoinedByLink, new[] { username }, new[] { inviterId });
            //});
        }

        #region Message
        public static TLMessageBase GetMessage(DependencyObject obj)
        {
            return (TLMessageBase)obj.GetValue(MessageProperty);
        }

        public static void SetMessage(DependencyObject obj, TLMessageBase value)
        {
            obj.SetValue(MessageProperty, value);
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.RegisterAttached("Message", typeof(TLMessageBase), typeof(ServiceHelper), new PropertyMetadata(null, OnMessageChanged));

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as RichTextBlock;
            var newValue = e.NewValue as TLMessageBase;

            sender.IsTextSelectionEnabled = false;
            sender.Blocks.Clear();

            var serviceMessage = newValue as TLMessageService;
            if (serviceMessage != null)
            {
                sender.Blocks.Add(Convert(serviceMessage, null));
                return;
            }

            var paragraph = new Paragraph();
            //paragraph.Inlines.Add(new Run { Text = Resources.MessageActionEmpty });
            sender.Blocks.Add(paragraph);
        }

        public static Paragraph Convert(TLMessageService serviceMessage, object parameter = null)
        {
            var fromId = serviceMessage.FromId;
            var user = InMemoryCacheService.Current.GetUser(fromId) as TLUser;
            var username = (user != null) ? user.FullName : "Resources.User";
            var action = serviceMessage.Action;
            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                return _actionsCache[action.GetType()].Invoke(action, fromId.Value, username);
            }

            var paragraph = new Paragraph();
            //paragraph.Inlines.Add(new Run { Text = Resources.MessageActionEmpty });
            return paragraph;
        }
        #endregion

        private static Paragraph ReplaceLinks(string text, string[] users = null, int[] identifiers = null)
        {
            var paragraph = new Paragraph();
            var regex = new Regex("({[0-9]?})");
            var matches = regex.Matches(text);
            if (matches.Count > 0)
            {
                var lastIndex = 0;
                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];

                    if (match.Index - lastIndex > 0)
                    {
                        paragraph.Inlines.Add(new Run { Text = text.Substring(lastIndex, match.Index - lastIndex) });
                    }

                    lastIndex = match.Index + match.Length;

                    if (users?.Length > i && identifiers?.Length > i)
                    {
                        var currentId = identifiers[i] + 0;
                        var hyperlink = new Hyperlink();
                        hyperlink.Click += (s, args) => Hyperlink_Navigate(currentId);
                        hyperlink.UnderlineStyle = UnderlineStyle.None;
                        hyperlink.Foreground = new SolidColorBrush(Colors.White);
                        hyperlink.Inlines.Add(new Run { Text = users[i], FontWeight = FontWeights.SemiBold });
                        paragraph.Inlines.Add(hyperlink);
                    }
                    else
                    {
                        paragraph.Inlines.Add(new Run { Text = users[i] });
                    }
                }

                if (lastIndex < text.Length - 1)
                {
                    paragraph.Inlines.Add(new Run { Text = text.Substring(lastIndex, text.Length - lastIndex) });
                }
            }
            else
            {
                paragraph.Inlines.Add(new Run { Text = text });
            }

            return paragraph;
        }

        private static void Hyperlink_Navigate(int userId)
        {
            var navigationService = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
            if (navigationService != null)
            {
                navigationService.Navigate(typeof(UserInfoPage), new TLPeerUser { UserId = userId });
            }
        }
    }
}
