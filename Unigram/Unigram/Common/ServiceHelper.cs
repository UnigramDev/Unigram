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
        private static readonly Dictionary<Type, Func<TLMessageService, TLMessageActionBase, int, string, bool, Paragraph>> _actionsCache;

        static ServiceHelper()
        {
            _actionsCache = new Dictionary<Type, Func<TLMessageService, TLMessageActionBase, int, string, bool, Paragraph>>();
            _actionsCache.Add(typeof(TLMessageActionDate), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, DateTimeToFormatConverter.ConvertDayGrouping(Utils.UnixTimestampToDateTime(((TLMessageActionDate)action).Date))));
            _actionsCache.Add(typeof(TLMessageActionEmpty), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, Strings.Messages.MessageActionEmpty));
            _actionsCache.Add(typeof(TLMessageActionGameScore), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
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
                            return ReplaceLinks(serviceMessage, won ? Strings.Gaming.YourScoredAtGamePlural : Strings.Gaming.YourScoredAtGame, new[] { value.ToString(), game.Title }, new[] { "tg-bold://", "tg-game://" }, useActiveLinks);
                        }

                        return ReplaceLinks(serviceMessage, won ? Strings.Gaming.UserScoredAtGamePlural : Strings.Gaming.UserScoredAtGame, new[] { fromUserFullName, value.ToString(), game.Title }, new[] { "tg-user://" + fromUserId, "tg-bold://", "tg-game://" }, useActiveLinks);
                    }

                    if (fromUserId == SettingsHelper.UserId)
                    {
                        return ReplaceLinks(serviceMessage, string.Format(won ? Strings.Gaming.YourScoredPlural : Strings.Gaming.YourScored, value));
                    }

                    return ReplaceLinks(serviceMessage, won ? Strings.Gaming.UserScoredPlural : Strings.Gaming.UserScored, new[] { fromUserFullName, value.ToString() }, new[] { "tg-user://" + fromUserId, "tg-bold://" }, useActiveLinks);
                }

                return ReplaceLinks(serviceMessage, won ? Strings.Gaming.UserScoredPlural : Strings.Gaming.UserScored, new[] { Strings.Nouns.UserNominativeSingular, value.ToString() }, new[] { "tg-bold://", "tg-bold://" }, useActiveLinks);
            });
            _actionsCache.Add(typeof(TLMessageActionChatCreate), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, Strings.Messages.MessageActionChatCreate, new[] { fromUserFullName, ((TLMessageActionChatCreate)action).Title }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            _actionsCache.Add(typeof(TLMessageActionChatEditPhoto), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, Strings.Messages.MessageActionChatEditPhoto, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            _actionsCache.Add(typeof(TLMessageActionChatEditTitle), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, Strings.Messages.MessageActionChatEditTitle, new[] { fromUserFullName, ((TLMessageActionChatEditTitle)action).Title }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            _actionsCache.Add(typeof(TLMessageActionChatDeletePhoto), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, Strings.Messages.MessageActionChatDeletePhoto, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            _actionsCache.Add(typeof(TLMessageActionChatAddUser), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
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
                    return ReplaceLinks(message, Strings.Messages.MessageActionChatAddSelf, new[] { names[0] }, new[] { "tg-user://" + users[0] }, useActiveLinks);
                }

                // TODO: replace last ", " with "and "

                var codesReplace = "{1}";
                if (codes.Count > 1)
                {
                    codesReplace = string.Concat(string.Join(", ", codes.Take(codes.Count - 1)), " and ", codes.Last());
                }

                var tags = users.Select(x => "tg-user://" + x).ToList();
                names.Insert(0, fromUserFullName);
                tags.Insert(0, "tg-user://" + fromUserId);

                return ReplaceLinks(message, string.Format(Strings.Messages.MessageActionChatAddUser, "{0}", codesReplace), names.ToArray(), tags.ToArray(), useActiveLinks);
            });
            _actionsCache.Add(typeof(TLMessageActionChatDeleteUser), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var userId = ((TLMessageActionChatDeleteUser)action).UserId;
                var user = InMemoryCacheService.Current.GetUser(userId);
                var userFullName = user.FullName;
                if (userId != fromUserId)
                {
                    return ReplaceLinks(message, Strings.Messages.MessageActionChatDeleteUser, new[] { fromUserFullName, userFullName }, new[] { "tg-user://" + fromUserId, "tg-user://" + userId }, useActiveLinks);
                }

                if (fromUserId == SettingsHelper.UserId)
                {
                    return ReplaceLinks(message, Strings.Messages.MessageActionLeftGroupSelf);
                }

                return ReplaceLinks(message, Strings.Messages.MessageActionUserLeftGroup, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
            });
            _actionsCache.Add(typeof(TLMessageActionScreenshotTaken), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                if (fromUserId == SettingsHelper.UserId)
                {
                    return ReplaceLinks(message, Strings.Messages.MessageActionYouScreenshotMessages);
                }

                return ReplaceLinks(message, Strings.Messages.MessageActionScreenshotMessages, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
            });
            //_actionsCache.Add(typeof(TLMessageActionUnreadMessages), (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => Strings.Resources.UnreadMessages.ToLowerInvariant());
            //_actionsCache.Add(typeof(TLMessageActionContactRegistered), delegate (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks)
            //{
            //    TLInt userId = ((TLMessageActionContactRegistered)action).UserId;
            //    TLUserBase user = IoC.Get<ICacheService>(null).GetUser(userId);
            //    string text = (user != null) ? user.FirstName.ToString() : Strings.Resources.User;
            //    if (string.IsNullOrEmpty(text) && user != null)
            //    {
            //        text = user.FullName;
            //    }
            //    return string.Format(Strings.Resources.ContactRegistered, text);
            //});
            _actionsCache.Add(typeof(TLMessageActionChatJoinedByLink), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, Strings.Messages.MessageActionChatJoinedByLink, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            //_actionsCache.Add(typeof(TLMessageActionMessageGroup), delegate (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks)
            //{
            //    int value = ((TLMessageActionMessageGroup)action).Group.Count.Value;
            //    return Language.Declension(value, Strings.Resources.CommentNominativeSingular, Strings.Resources.CommentNominativePlural, Strings.Resources.CommentGenitiveSingular, Strings.Resources.CommentGenitivePlural, null, null).ToLower(CultureInfo.get_CurrentUICulture());
            //});
            _actionsCache.Add(typeof(TLMessageActionChatMigrateTo), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var channelId = ((TLMessageActionChatMigrateTo)action).ChannelId;
                var channel = InMemoryCacheService.Current.GetChat(channelId) as TLChannel;
                var fullName = channel != null ? channel.DisplayName : string.Empty;
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    return ReplaceLinks(message, Strings.Messages.MessageActionChatMigrateToGeneric);
                }

                return ReplaceLinks(message, Strings.Messages.MessageActionChatMigrateTo, new[] { fullName }, new[] { "tg-channel://" + channelId }, useActiveLinks);
            });
            _actionsCache.Add(typeof(TLMessageActionCustomAction), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, ((TLMessageActionCustomAction)action).Message));
            _actionsCache.Add(typeof(TLMessageActionChannelMigrateFrom), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, Strings.Messages.MessageActionChannelMigrateFrom));
            _actionsCache.Add(typeof(TLMessageActionHistoryClear), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, "History was cleared"));
            _actionsCache.Add(typeof(TLMessageActionUnreadMessages), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, "Unread messages"));
            _actionsCache.Add(typeof(TLMessageActionPhoneCall), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                if (message is TLMessageService serviceMessage && action is TLMessageActionPhoneCall phoneCallAction)
                {
                    var loader = ResourceLoader.GetForViewIndependentUse("Resources");
                    var text = string.Empty;

                    var outgoing = serviceMessage.IsOut;
                    var missed = phoneCallAction.Reason is TLPhoneCallDiscardReasonMissed || phoneCallAction.Reason is TLPhoneCallDiscardReasonBusy;

                    var type = loader.GetString(missed ? (outgoing ? "CallCanceled" : "CallMissed") : (outgoing ? "CallOutgoing" : "CallIncoming"));
                    var duration = string.Empty;

                    if (!missed && (phoneCallAction.Duration ?? 0) > 0)
                    {
                        duration = BindConvert.Current.CallDuration(phoneCallAction.Duration ?? 0);
                    }

                    return ReplaceLinks(serviceMessage, missed || (phoneCallAction.Duration ?? 0) < 1 ? type : string.Format(Strings.Statuses.CallTimeFormat, type, duration));
                }

                return null;
            });
            _actionsCache.Add(typeof(TLMessageActionPaymentSent), (TLMessageService message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                if (message is TLMessageService serviceMessage && action is TLMessageActionPaymentSent paymentSentAction)
                {
                    if (serviceMessage.Reply is TLMessage replied && replied.Media is TLMessageMediaInvoice invoiceMedia)
                    {
                        var amount = BindConvert.Current.FormatAmount(paymentSentAction.TotalAmount, paymentSentAction.Currency);
                        var userName = replied.From?.FullName ?? "User";
                        var userId = replied.FromId ?? fromUserId;

                        return ReplaceLinks(serviceMessage, "You have just successfully transferred {0} to {1} for {2}", new[] { amount, userName, invoiceMedia.Title }, new[] { "tg-bold://", "tg-user://" + userId, "tg-message://" + replied.Id }, useActiveLinks);
                    }
                    else if (serviceMessage.Parent is TLUser user)
                    {
                        var amount = BindConvert.Current.FormatAmount(paymentSentAction.TotalAmount, paymentSentAction.Currency);
                        var userName = user?.FullName ?? "User";
                        var userId = user?.Id ?? fromUserId;

                        return ReplaceLinks(serviceMessage, "You have just successfully transferred {0} to {1}", new[] { amount, userName }, new[] { "tg-bold://", "tg-user://" + userId }, useActiveLinks);
                    }
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

            var message = newValue as TLMessage;
            if (message != null && message.Media is TLMessageMediaPhoto photoMedia && photoMedia.HasTTLSeconds && (photoMedia.Photo is TLPhotoEmpty || !photoMedia.HasPhoto))
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run { Text = "Photo has expired" });
                sender.Blocks.Add(paragraph);
                return;
            }
            else if (message != null && message.Media is TLMessageMediaDocument documentMedia && documentMedia.HasTTLSeconds && (documentMedia.Document is TLDocumentEmpty || !documentMedia.HasDocument))
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run { Text = "Video has expired" });
                sender.Blocks.Add(paragraph);
                return;
            }

            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run { Text = Strings.Messages.MessageActionEmpty });
                sender.Blocks.Add(paragraph);
            }
        }

        public static string Convert(TLMessageService serviceMessage)
        {
            var paragraph = Convert(serviceMessage, false);
            if (paragraph != null && paragraph.Inlines.Count > 0)
            {
                var run = paragraph.Inlines[0] as Run;
                if (run != null)
                {
                    return run.Text;
                }
            }

            return Strings.Messages.MessageActionEmpty;
        }

        public static Paragraph ConvertOld(TLMessageService serviceMessage, bool useActiveLinks)
        {
            var fromId = serviceMessage.FromId;
            var user = InMemoryCacheService.Current.GetUser(fromId) as TLUser;
            var username = user != null ? user.FullName : Strings.Nouns.UserNominativeSingular;
            var action = serviceMessage.Action;
            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                return _actionsCache[action.GetType()].Invoke(serviceMessage, action, fromId.Value, username, useActiveLinks);
            }

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = Strings.Messages.MessageActionEmpty });
            return paragraph;
        }

        public static Paragraph Convert(TLMessageService serviceMessage, bool useActiveLinks)
        {
            var fromId = serviceMessage.FromId;
            var user = InMemoryCacheService.Current.GetUser(fromId ?? 0);
            var userFullName = user != null ? user.FullName : Strings.Nouns.UserNominativeSingular;
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
                                return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinGame }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                            }

                            var photoMedia = repliedMessage.Media as TLMessageMediaPhoto;
                            if (photoMedia != null)
                            {
                                return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinPhoto }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
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

                                    return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, string.Format(Strings.Messages.MessageActionPinSticker, emoji) }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                                }
                                if (TLMessage.IsVoice(documentMedia.Document))
                                {
                                    return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinVoiceMessage }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                                }
                                if (TLMessage.IsMusic(documentMedia.Document))
                                {
                                    return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinTrack }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                                }
                                if (TLMessage.IsVideo(documentMedia.Document))
                                {
                                    return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinVideo }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                                }
                                if (TLMessage.IsGif(documentMedia.Document))
                                {
                                    return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinGif }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                                }

                                return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinFile }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                            }

                            var contactMedia = repliedMessage.Media as TLMessageMediaContact;
                            if (contactMedia != null)
                            {
                                return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinContact }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                            }

                            var geoMedia = repliedMessage.Media as TLMessageMediaGeo;
                            if (geoMedia != null)
                            {
                                return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinMap }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                            }

                            var venueMedia = repliedMessage.Media as TLMessageMediaVenue;
                            if (venueMedia != null)
                            {
                                return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinMap }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                            }

                            if (repliedMessage.Message.Length > 0)
                            {
                                if (repliedMessage.Message.Length > 20)
                                {
                                    return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPinText, new[] { userFullName, repliedMessage.Message.Substring(0, 20).Replace("\r\n", "\n").Replace("\n", " ") + "…" }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                                }

                                return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPinText, new[] { userFullName, repliedMessage.Message.Replace("\r\n", "\n").Replace("\n", " ") }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                            }

                            return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinMessage }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
                        }

                        return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionPin, new[] { userFullName, Strings.Messages.MessageActionPinMessage }, new[] { "tg-user://" + fromId, "tg-message://" + serviceMessage.ReplyToMsgId }, useActiveLinks);
                    }
                }

                var channelCreateAction = action as TLMessageActionChannelCreate;
                if (channelCreateAction != null)
                {
                    if (!flag)
                    {
                        return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionChannelCreate);
                    }

                    return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionChatCreate, new[] { userFullName, channelCreateAction.Title }, new[] { "tg-user://" + fromId }, useActiveLinks);
                }

                var editPhotoAction = action as TLMessageActionChatEditPhoto;
                if (editPhotoAction != null)
                {
                    if (!flag)
                    {
                        return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionChannelEditPhoto);
                    }

                    return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionChatEditPhoto, new[] { userFullName }, new[] { "tg-user://" + fromId }, useActiveLinks);
                }

                var deletePhotoAction = action as TLMessageActionChatDeletePhoto;
                if (deletePhotoAction != null)
                {
                    if (!flag)
                    {
                        return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionChannelDeletePhoto);
                    }

                    return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionChatDeletePhoto, new[] { userFullName }, new[] { "tg-user://" + fromId }, useActiveLinks);
                }

                var editTitleAction = action as TLMessageActionChatEditTitle;
                if (editTitleAction != null)
                {
                    if (!flag)
                    {
                        return ReplaceLinks(serviceMessage, string.Format(Strings.Messages.MessageActionChannelEditTitle, editTitleAction.Title));
                    }

                    return ReplaceLinks(serviceMessage, Strings.Messages.MessageActionChatEditTitle, new[] { userFullName, editTitleAction.Title }, new[] { "tg-user://" + fromId }, useActiveLinks);
                }
            }

            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                return _actionsCache[action.GetType()].Invoke(serviceMessage, action, fromId ?? 0, userFullName, useActiveLinks);
            }

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = Strings.Messages.MessageActionEmpty });
            return paragraph;
        }
        #endregion

        private static Paragraph ReplaceLinks(TLMessageCommonBase message, string text, string[] users = null, string[] identifiers = null, bool useActiveLinks = true)
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
                            hyperlink.Click += (s, args) => Hyperlink_Navigate(message, currentId);
                            hyperlink.UnderlineStyle = UnderlineStyle.None;
                            hyperlink.Foreground = new SolidColorBrush(Colors.White);
                            hyperlink.Inlines.Add(new Run { Text = users[i], FontWeight = FontWeights.SemiBold });
                            paragraph.Inlines.Add(hyperlink);
                        }
                    }
                    else if (users?.Length > i)
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

        private static void Hyperlink_Navigate(TLMessageCommonBase message, string currentId)
        {
            if (currentId.StartsWith("tg-user://"))
            {
                var navigationService = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                if (navigationService != null)
                {
                    navigationService.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = int.Parse(currentId.Replace("tg-user://", string.Empty)) });
                }
            }
            else if (currentId.StartsWith("tg-message://"))
            {
                var navigationService = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                if (navigationService != null && navigationService.Content is DialogPage dialogPage)
                {
                    dialogPage.ViewModel.MessageOpenReplyCommand.Execute(message);
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

        public static TLMessageMediaInvoice GetInvoice(TLMessageService message)
        {
            var reply = message.Reply as TLMessage;
            if (reply != null)
            {
                var invoiceMedia = reply.Media as TLMessageMediaInvoice;
                if (invoiceMedia != null)
                {
                    return invoiceMedia;
                }
            }

            return null;
        }
    }
}
