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
using Unigram.Strings;
using static Unigram.ViewModels.DialogViewModel;
using Unigram.Views.Users;
using Windows.ApplicationModel.Resources;
using Unigram.Converters;

namespace Unigram.Common
{
    public class ServiceHelper
    {
        private static readonly Dictionary<Type, Func<TLMessageBase, TLMessageActionBase, int, string, bool, Paragraph>> _actionsCache;

        static ServiceHelper()
        {
            _actionsCache = new Dictionary<Type, Func<TLMessageBase, TLMessageActionBase, int, string, bool, Paragraph>>();
            _actionsCache.Add(typeof(TLMessageActionDate), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(new DateTimeFormatter("day month").Format(Utils.UnixTimestampToDateTime(((TLMessageActionDate)action).Date))));
            _actionsCache.Add(typeof(TLMessageActionEmpty), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(AppResources.MessageActionEmpty));
            _actionsCache.Add(typeof(TLMessageActionGameScore), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var value = ((TLMessageActionGameScore)action).Score;
                var won = value == 0 || value > 0;
                var serviceMessage = message as TLMessageService;
                if (serviceMessage != null)
                {
                    var game = GetGame(serviceMessage);
                    if (game != null)
                    {
                        var text = game.Title.ToString();
                        //TLMessageCommon tLMessageCommon = serviceMessage.Reply as TLMessageCommonBase;
                        //if (tLMessageCommon != null)
                        //{
                        //    text = ServiceMessageToTextConverter.GetGameFullNameString(text, serviceMessage.Index, serviceMessage.ToId, useActiveLinks);
                        //}

                        if (fromUserId == SettingsHelper.UserId)
                        {
                            return ReplaceLinks(won ? AppResources.YourScoredAtGamePlural : AppResources.YourScoredAtGame, new[] { value.ToString(), game.Title }, new[] { "tg-bold://", "tg-game://" }, useActiveLinks);
                        }

                        return ReplaceLinks(won ? AppResources.UserScoredAtGamePlural : AppResources.UserScoredAtGame, new[] { fromUserFullName, value.ToString(), game.Title }, new[] { "tg-user://" + fromUserId, "tg-bold://", "tg-game://" }, useActiveLinks);
                    }

                    if (fromUserId == SettingsHelper.UserId)
                    {
                        return ReplaceLinks(string.Format(won ? AppResources.YourScoredPlural : AppResources.YourScored, value));
                    }

                    return ReplaceLinks(won ? AppResources.UserScoredPlural : AppResources.UserScored, new[] { fromUserFullName, value.ToString() }, new[] { "tg-user://" + fromUserId, "tg-bold://" }, useActiveLinks);
                }

                return ReplaceLinks(won ? AppResources.UserScoredPlural : AppResources.UserScored, new[] { AppResources.UserNominativeSingular, value.ToString() }, new[] { "tg-bold://", "tg-bold://" }, useActiveLinks);
            });
            _actionsCache.Add(typeof(TLMessageActionChatCreate), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(AppResources.MessageActionChatCreate, new[] { fromUserFullName, ((TLMessageActionChatCreate)action).Title }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            _actionsCache.Add(typeof(TLMessageActionChatEditPhoto), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(AppResources.MessageActionChatEditPhoto, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            _actionsCache.Add(typeof(TLMessageActionChatEditTitle), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(AppResources.MessageActionChatEditTitle, new[] { fromUserFullName, ((TLMessageActionChatEditTitle)action).Title }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            _actionsCache.Add(typeof(TLMessageActionChatDeletePhoto), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(AppResources.MessageActionChatDeletePhoto, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            _actionsCache.Add(typeof(TLMessageActionChatAddUser), delegate (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks)
            {
                var users = ((TLMessageActionChatAddUser)action).Users;
                var names = new List<string>();
                var codes = new List<string>();

                for (int i = 0; i < users.Count; i++)
                {
                    var item = users[i];
                    var user = InMemoryCacheService.Current.GetUser(item);
                    if (user != null)
                    {
                        names.Add(user.FullName);
                        codes.Add($"{{{i + 1}}}");
                    }
                }

                if (users.Count == 1 && users[0] == fromUserId)
                {
                    return ReplaceLinks(AppResources.MessageActionChatAddSelf, new[] { names[0] }, new[] { "tg-user://" + users[0] }, useActiveLinks);
                }

                // TODO: replace last ", " with "and "

                var codesReplace = "{1}";
                if (codes.Count > 1)
                {
                    codesReplace = string.Concat(string.Join(", ", codes.Take(codes.Count - 1)), " and ", codes.Last());
                }

                return ReplaceLinks(string.Format(AppResources.MessageActionChatAddUser, "{0}", codesReplace), new[] { fromUserFullName }.Union(names).ToArray(), new[] { "tg-user://" + fromUserId }.Union(users.Select(x => "tg-user://" + x)).ToArray(), useActiveLinks);
            });
            _actionsCache.Add(typeof(TLMessageActionChatDeleteUser), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var userId = ((TLMessageActionChatDeleteUser)action).UserId;
                var user = InMemoryCacheService.Current.GetUser(userId);
                var userFullName = user.FullName;
                if (userId != fromUserId)
                {
                    return ReplaceLinks(AppResources.MessageActionChatDeleteUser, new[] { fromUserFullName, userFullName }, new[] { "tg-user://" + fromUserId, "tg-user://" + userId }, useActiveLinks);
                }

                if (fromUserId == SettingsHelper.UserId)
                {
                    return ReplaceLinks(AppResources.MessageActionLeftGroupSelf);
                }

                return ReplaceLinks(AppResources.MessageActionUserLeftGroup, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
            });
            //_actionsCache.Add(typeof(TLMessageActionUnreadMessages), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => AppResources.UnreadMessages.ToLowerInvariant());
            //_actionsCache.Add(typeof(TLMessageActionContactRegistered), delegate (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks)
            //{
            //    TLInt userId = ((TLMessageActionContactRegistered)action).UserId;
            //    TLUserBase user = IoC.Get<ICacheService>(null).GetUser(userId);
            //    string text = (user != null) ? user.FirstName.ToString() : AppResources.User;
            //    if (string.IsNullOrEmpty(text) && user != null)
            //    {
            //        text = user.FullName;
            //    }
            //    return string.Format(AppResources.ContactRegistered, text);
            //});
            _actionsCache.Add(typeof(TLMessageActionChatJoinedByLink), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(AppResources.MessageActionChatJoinedByLink, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            //_actionsCache.Add(typeof(TLMessageActionMessageGroup), delegate (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks)
            //{
            //    int value = ((TLMessageActionMessageGroup)action).Group.Count.Value;
            //    return Language.Declension(value, AppResources.CommentNominativeSingular, AppResources.CommentNominativePlural, AppResources.CommentGenitiveSingular, AppResources.CommentGenitivePlural, null, null).ToLower(CultureInfo.get_CurrentUICulture());
            //});
            _actionsCache.Add(typeof(TLMessageActionChatMigrateTo), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var channelId = ((TLMessageActionChatMigrateTo)action).ChannelId;
                var channel = InMemoryCacheService.Current.GetChat(channelId) as TLChannel;
                var fullName = channel != null ? channel.DisplayName : string.Empty;

                return ReplaceLinks(AppResources.MessageActionChatMigrateTo, new[] { fullName }, new[] { "tg-channel://" + channelId }, useActiveLinks);
            });
            _actionsCache.Add(typeof(TLMessageActionChannelMigrateFrom), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(AppResources.MessageActionChannelMigrateFrom));
            _actionsCache.Add(typeof(TLMessageActionHistoryClear), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(string.Empty));
            _actionsCache.Add(typeof(TLMessageActionUnreadMessages), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks("Unread messages"));
            _actionsCache.Add(typeof(TLMessageActionPhoneCall), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                if (message is TLMessageService serviceMessage && action is TLMessageActionPhoneCall phoneCallAction)
                {
                    var loader = ResourceLoader.GetForCurrentView("Resources");
                    var text = string.Empty;

                    var outgoing = serviceMessage.IsOut;
                    var missed = phoneCallAction.Reason is TLPhoneCallDiscardReasonMissed || phoneCallAction.Reason is TLPhoneCallDiscardReasonBusy;

                    var type = loader.GetString(missed ? (outgoing ? "CallCanceled" : "CallMissed") : (outgoing ? "CallOutgoing" : "CallIncoming"));
                    var duration = string.Empty;

                    if (!missed)
                    {
                        duration = BindConvert.Current.CallDuration(phoneCallAction.Duration ?? 0);
                    }

                    return ReplaceLinks(missed ? type : string.Format(AppResources.CallTimeFormat, type, duration));
                }

                return null;
            });
        }

        #region Message
        public static TLMessageBase GetMessage(DependencyObject obj)
        {
            return (TLMessageBase)obj.GetValue(MessageProperty);
        }

        public static void SetMessage(DependencyObject obj, TLMessageBase value)
        {
            // TODO: shitty hack!!!
            var oldValue = obj.GetValue(MessageProperty);
            obj.SetValue(MessageProperty, value);

            if (oldValue == value)
            {
                OnMessageChanged(obj as RichTextBlock, value);
            }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.RegisterAttached("Message", typeof(TLMessageBase), typeof(ServiceHelper), new PropertyMetadata(null, OnMessageChanged));

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as RichTextBlock;
            var newValue = e.NewValue as TLMessageBase;

            OnMessageChanged(sender, newValue);
        }

        private static void OnMessageChanged(RichTextBlock sender, TLMessageBase newValue)
        {
            sender.IsTextSelectionEnabled = false;
            sender.Blocks.Clear();

            var serviceMessage = newValue as TLMessageService;
            if (serviceMessage != null)
            {
                sender.Blocks.Add(Convert(serviceMessage, true));
                return;
            }

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = AppResources.MessageActionEmpty });
            sender.Blocks.Add(paragraph);
        }

        public static string Convert(TLMessageService serviceMessage)
        {
            var paragraph = Convert(serviceMessage, false);
            if (paragraph.Inlines.Count > 0)
            {
                var run = paragraph.Inlines[0] as Run;
                if (run != null)
                {
                    return run.Text;
                }
            }

            return AppResources.MessageActionEmpty;
        }

        public static Paragraph ConvertOld(TLMessageService serviceMessage, bool useActiveLinks)
        {
            var fromId = serviceMessage.FromId;
            var user = InMemoryCacheService.Current.GetUser(fromId) as TLUser;
            var username = user != null ? user.FullName : AppResources.UserNominativeSingular;
            var action = serviceMessage.Action;
            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                return _actionsCache[action.GetType()].Invoke(serviceMessage, action, fromId.Value, username, useActiveLinks);
            }

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = AppResources.MessageActionEmpty });
            return paragraph;
        }

        public static Paragraph Convert(TLMessageService serviceMessage, bool useActiveLinks)
        {
            var fromId = serviceMessage.FromId;
            var user = InMemoryCacheService.Current.GetUser(fromId ?? 0);
            var userFullName = user != null ? user.FullName : AppResources.UserNominativeSingular;
            var action = serviceMessage.Action;

            if (serviceMessage.ToId is TLPeerChannel)
            {
                var channel = InMemoryCacheService.Current.GetChat(serviceMessage.ToId.Id) as TLChannel;
                var flag = channel != null && channel.IsMegaGroup;

                var pinMessageAction = action as TLMessageActionPinMessage;
                if (pinMessageAction != null)
                {
                    var replyToMsgId = serviceMessage.ReplyToMsgId;
                    if (replyToMsgId != null && channel != null)
                    {
                        var repliedMessage = InMemoryCacheService.Current.GetMessage(replyToMsgId, channel.Id) as TLMessage;
                        if (repliedMessage != null)
                        {
                            var gameMedia = repliedMessage.Media as TLMessageMediaGame;
                            if (gameMedia != null)
                            {
                                return ReplaceLinks(AppResources.MessageActionPinGame, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                            }

                            var photoMedia = repliedMessage.Media as TLMessageMediaPhoto;
                            if (photoMedia != null)
                            {
                                return ReplaceLinks(AppResources.MessageActionPinPhoto, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                            }

                            var documentMedia = repliedMessage.Media as TLMessageMediaDocument;
                            if (documentMedia != null)
                            {
                                if (TLMessage.IsSticker(documentMedia.Document))
                                {
                                    var emoji = string.Empty;

                                    var documentSticker = documentMedia.Document as TLDocument;
                                    if (documentSticker != null)
                                    {
                                        var attribute = documentSticker.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                                        if (attribute != null)
                                        {
                                            emoji = $"{attribute.Alt} ";
                                        }
                                    }

                                    return ReplaceLinks(AppResources.MessageActionPinSticker, new[] { userFullName, emoji }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                                }
                                if (TLMessage.IsVoice(documentMedia.Document))
                                {
                                    return ReplaceLinks(AppResources.MessageActionPinVoiceMessage, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                                }
                                if (TLMessage.IsMusic(documentMedia.Document))
                                {
                                    return ReplaceLinks(AppResources.MessageActionPinTrack, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                                }
                                if (TLMessage.IsVideo(documentMedia.Document))
                                {
                                    return ReplaceLinks(AppResources.MessageActionPinVideo, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                                }
                                if (TLMessage.IsGif(documentMedia.Document))
                                {
                                    return ReplaceLinks(AppResources.MessageActionPinGif, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                                }

                                return ReplaceLinks(AppResources.MessageActionPinFile, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                            }

                            var contactMedia = repliedMessage.Media as TLMessageMediaContact;
                            if (contactMedia != null)
                            {
                                return ReplaceLinks(AppResources.MessageActionPinContact, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                            }

                            var geoMedia = repliedMessage.Media as TLMessageMediaGeo;
                            if (geoMedia != null)
                            {
                                return ReplaceLinks(AppResources.MessageActionPinMap, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                            }

                            var venueMedia = repliedMessage.Media as TLMessageMediaVenue;
                            if (venueMedia != null)
                            {
                                return ReplaceLinks(AppResources.MessageActionPinMap, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                            }

                            if (repliedMessage.Message.Length > 0)
                            {
                                if (repliedMessage.Message.Length > 20)
                                {
                                    return ReplaceLinks(AppResources.MessageActionPinText, new[] { userFullName, repliedMessage.Message.Substring(0, 20).Replace("\r\n", "\n").Replace("\n", " ") + "..." }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                                }

                                return ReplaceLinks(AppResources.MessageActionPinText, new[] { userFullName, repliedMessage.Message.Replace("\r\n", "\n").Replace("\n", " ") }, new[] { "tg-user://" + fromId.Value, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                            }

                            return ReplaceLinks(AppResources.MessageActionPinMessage, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                        }
                    }

                    return ReplaceLinks(AppResources.MessageActionPinMessage, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                }

                var channelCreateAction = action as TLMessageActionChannelCreate;
                if (channelCreateAction != null)
                {
                    if (!flag)
                    {
                        return ReplaceLinks(AppResources.MessageActionChannelCreate);
                    }

                    return ReplaceLinks(AppResources.MessageActionChatCreate, new[] { userFullName, channelCreateAction.Title }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                }

                var editPhotoAction = action as TLMessageActionChatEditPhoto;
                if (editPhotoAction != null)
                {
                    if (!flag)
                    {
                        return ReplaceLinks(AppResources.MessageActionChannelEditPhoto);
                    }

                    return ReplaceLinks(AppResources.MessageActionChatEditPhoto, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                }

                var deletePhotoAction = action as TLMessageActionChatDeletePhoto;
                if (deletePhotoAction != null)
                {
                    if (!flag)
                    {
                        return ReplaceLinks(AppResources.MessageActionChannelDeletePhoto);
                    }

                    return ReplaceLinks(AppResources.MessageActionChatDeletePhoto, new[] { userFullName }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                }

                var editTitleAction = action as TLMessageActionChatEditTitle;
                if (editTitleAction != null)
                {
                    if (!flag)
                    {
                        return ReplaceLinks(string.Format(AppResources.MessageActionChannelEditTitle, editTitleAction.Title));
                    }

                    return ReplaceLinks(AppResources.MessageActionChatEditTitle, new[] { userFullName, editTitleAction.Title }, new[] { "tg-user://" + fromId.Value }, useActiveLinks);
                }
            }

            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                return _actionsCache[action.GetType()].Invoke(serviceMessage, action, fromId.Value, userFullName, useActiveLinks);
            }

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = AppResources.MessageActionEmpty });
            return paragraph;
        }
        #endregion

        private static Paragraph ReplaceLinks(string text, string[] users = null, string[] identifiers = null, bool useActiveLinks = true)
        {
            var paragraph = new Paragraph();

            if (users == null)
            {
                paragraph.Inlines.Add(new Run { Text = text });
                return paragraph;
            }

            if (!useActiveLinks)
            {
                paragraph.Inlines.Add(new Run { Text = string.Format(text, users) });
                return paragraph;
            }

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
                        var currentId = identifiers[i];
                        if (currentId.Equals("tg-bold://"))
                        {
                            paragraph.Inlines.Add(new Run { Text = users[i], FontWeight = FontWeights.SemiBold });
                        }
                        else
                        {
                            var hyperlink = new Hyperlink();
                            hyperlink.Click += (s, args) => Hyperlink_Navigate(currentId);
                            hyperlink.UnderlineStyle = UnderlineStyle.None;
                            hyperlink.Foreground = new SolidColorBrush(Colors.White);
                            hyperlink.Inlines.Add(new Run { Text = users[i], FontWeight = FontWeights.SemiBold });
                            paragraph.Inlines.Add(hyperlink);
                        }
                    }
                    else
                    {
                        paragraph.Inlines.Add(new Run { Text = users[i] });
                    }
                }

                if (lastIndex < text.Length)
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

        private static void Hyperlink_Navigate(string userId)
        {
            if (userId.StartsWith("tg-user://"))
            {
                var navigationService = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                if (navigationService != null)
                {
                    navigationService.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = int.Parse(userId.Replace("tg-user://", string.Empty)) });
                }
            }
        }

        public static TLGame GetGame(TLMessageService message)
        {
            var reply = message.Reply as TLMessage;
            if (reply != null)
            {
                var gameMedia = reply.Media as TLMessageMediaGame;
                if (gameMedia != null)
                {
                    return gameMedia.Game;
                }
            }

            return null;
        }
    }
}
