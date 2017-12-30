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
using Unigram.Views.Channels;

namespace Unigram.Common
{
    public class ServiceHelper
    {
        private static readonly Dictionary<Type, Func<TLMessageService, TLMessageActionBase, TLUser, bool, (string content, IList<TLMessageEntityBase> entities)>> _actionsCache;

        static ServiceHelper()
        {
            _actionsCache = new Dictionary<Type, Func<TLMessageService, TLMessageActionBase, TLUser, bool, (string, IList<TLMessageEntityBase>)>>();
            _actionsCache.Add(typeof(TLMessageActionDate), (TLMessageService message, TLMessageActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                return (DateTimeToFormatConverter.ConvertDayGrouping(Utils.UnixTimestampToDateTime(((TLMessageActionDate)action).Date)), null);
            });
            _actionsCache.Add(typeof(TLMessageActionContactRegistered), (TLMessageService message, TLMessageActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                return (string.Format(Strings.Android.NotificationContactJoined, fromUser.FullName), null);
            });
            _actionsCache.Add(typeof(TLMessageActionGameScore), (TLMessageService message, TLMessageActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var value = ((TLMessageActionGameScore)action).Score;
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var game = GetGame(message);
                if (game == null)
                {
                    if (fromUser != null && fromUser.Id == SettingsHelper.UserId)
                    {
                        content = string.Format(Strings.Android.ActionYouScored, LocaleHelper.Declension("Points", value));
                    }
                    else
                    {
                        content = ReplaceWithLink(string.Format(Strings.Android.ActionUserScored, LocaleHelper.Declension("Points", value)), "un1", fromUser, ref entities);
                    }
                }
                else
                {
                    if (fromUser != null && fromUser.Id == SettingsHelper.UserId)
                    {
                        content = string.Format(Strings.Android.ActionYouScoredInGame, LocaleHelper.Declension("Points", value));
                    }
                    else
                    {
                        content = ReplaceWithLink(string.Format(Strings.Android.ActionUserScoredInGame, LocaleHelper.Declension("Points", value)), "un1", fromUser, ref entities);
                    }

                    content = ReplaceWithLink(content, "un2", game, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLMessageActionChannelCreate), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;

                if (message.Parent is TLChannel channel && channel.IsMegaGroup)
                {
                    content = Strings.Android.ActionCreateMega;
                }
                else
                {
                    content = Strings.Android.ActionCreateChannel;
                }

                return (content, null);
            });
            _actionsCache.Add(typeof(TLMessageActionChatCreate), (TLMessageService message, TLMessageActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                if (message.IsOut)
                {
                    content = Strings.Android.ActionYouCreateGroup;
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.ActionCreateGroup, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLMessageActionChatEditPhoto), (TLMessageService message, TLMessageActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                if (message.Parent is TLChannel channel && channel.IsBroadcast)
                {
                    content = Strings.Android.ActionChannelChangedPhoto;
                }
                else
                {
                    if (message.IsOut)
                    {
                        content = Strings.Android.ActionYouChangedPhoto;
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Android.ActionChangedPhoto, "un1", fromUser, ref entities);
                    }
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLMessageActionChatEditTitle), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var action = actionBase as TLMessageActionChatEditTitle;

                if (message.Parent is TLChannel channel && channel.IsBroadcast)
                {
                    content = Strings.Android.ActionChannelChangedTitle.Replace("un2", action.Title);
                }
                else
                {
                    if (message.IsOut)
                    {
                        content = Strings.Android.ActionYouChangedTitle.Replace("un2", action.Title);
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Android.ActionChangedTitle.Replace("un2", action.Title), "un1", fromUser, ref entities);
                    }
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLMessageActionChatDeletePhoto), (TLMessageService message, TLMessageActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                if (message.Parent is TLChannel channel && channel.IsBroadcast)
                {
                    content = Strings.Android.ActionChannelRemovedPhoto;
                }
                else
                {
                    if (message.IsOut)
                    {
                        content = Strings.Android.ActionYouRemovedPhoto;
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Android.ActionRemovedPhoto, "un1", fromUser, ref entities);
                    }
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLMessageActionChatAddUser), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                var action = actionBase as TLMessageActionChatAddUser;

                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                int singleUserId = 0;
                if (singleUserId == 0 && action.Users.Count == 1)
                {
                    singleUserId = action.Users[0];
                }

                if (singleUserId != 0)
                {
                    var whoUser = InMemoryCacheService.Current.GetUser(singleUserId);
                    if (singleUserId == message.FromId)
                    {
                        var channel = message.Parent as TLChannel;
                        if (channel != null && channel.IsBroadcast)
                        {
                            content = Strings.Android.ChannelJoined;
                        }
                        else
                        {
                            if (channel != null && channel.IsMegaGroup)
                            {
                                if (singleUserId == SettingsHelper.UserId)
                                {
                                    content = Strings.Android.ChannelMegaJoined;
                                }
                                else
                                {
                                    content = ReplaceWithLink(Strings.Android.ActionAddUserSelfMega, "un1", fromUser, ref entities);
                                }
                            }
                            else if (message.IsOut)
                            {
                                content = Strings.Android.ActionAddUserSelfYou;
                            }
                            else
                            {
                                content = ReplaceWithLink(Strings.Android.ActionAddUserSelf, "un1", fromUser, ref entities);
                            }
                        }
                    }
                    else
                    {
                        if (message.IsOut)
                        {
                            content = ReplaceWithLink(Strings.Android.ActionYouAddUser, "un2", whoUser, ref entities);
                        }
                        else if (singleUserId == SettingsHelper.UserId)
                        {
                            var channel = message.Parent as TLChannel;
                            if (channel != null)
                            {
                                if (channel.IsMegaGroup)
                                {
                                    content = ReplaceWithLink(Strings.Android.MegaAddedBy, "un1", fromUser, ref entities);
                                }
                                else
                                {
                                    content = ReplaceWithLink(Strings.Android.ChannelAddedBy, "un1", fromUser, ref entities);
                                }
                            }
                            else
                            {
                                content = ReplaceWithLink(Strings.Android.ActionAddUserYou, "un1", fromUser, ref entities);
                            }
                        }
                        else
                        {
                            content = ReplaceWithLink(Strings.Android.ActionAddUser, "un1", fromUser, ref entities);
                            content = ReplaceWithLink(content, "un2", whoUser, ref entities);
                        }
                    }
                }
                else
                {
                    if (message.IsOut)
                    {
                        content = ReplaceWithLink(Strings.Android.ActionYouAddUser, "un2", action.Users, null, ref entities);
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Android.ActionAddUser, "un1", fromUser, ref entities);
                        content = ReplaceWithLink(content, "un2", action.Users, null, ref entities);
                    }
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLMessageActionChatDeleteUser), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                var action = actionBase as TLMessageActionChatDeleteUser;

                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                if (action.UserId == message.FromId)
                {
                    if (message.IsOut)
                    {
                        content = Strings.Android.ActionYouLeftUser;
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Android.ActionLeftUser, "un1", fromUser, ref entities);
                    }
                }
                else
                {
                    var whoUser = InMemoryCacheService.Current.GetUser(action.UserId);
                    if (message.IsOut)
                    {
                        content = ReplaceWithLink(Strings.Android.ActionYouKickUser, "un2", whoUser, ref entities);
                    }
                    else if (action.UserId == SettingsHelper.UserId)
                    {
                        content = ReplaceWithLink(Strings.Android.ActionKickUserYou, "un1", fromUser, ref entities);
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Android.ActionKickUser, "un1", fromUser, ref entities);
                        content = ReplaceWithLink(content, "un2", whoUser, ref entities);
                    }
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLMessageActionScreenshotTaken), (TLMessageService message, TLMessageActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                if (message.IsOut)
                {
                    content = Strings.Android.ActionTakeScreenshootYou;
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.ActionTakeScreenshoot, "un1", fromUser, ref entities);
                }

                return (content, entities);
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
            _actionsCache.Add(typeof(TLMessageActionChatJoinedByLink), (TLMessageService message, TLMessageActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                if (message.IsOut)
                {
                    content = Strings.Android.ActionInviteYou;
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.ActionInviteUser, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            //_actionsCache.Add(typeof(TLMessageActionMessageGroup), delegate (TLMessageBase message, TLMessageActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks)
            //{
            //    int value = ((TLMessageActionMessageGroup)action).Group.Count.Value;
            //    return Language.Declension(value, Strings.Resources.CommentNominativeSingular, Strings.Resources.CommentNominativePlural, Strings.Resources.CommentGenitiveSingular, Strings.Resources.CommentGenitivePlural, null, null).ToLower(CultureInfo.get_CurrentUICulture());
            //});
            _actionsCache.Add(typeof(TLMessageActionChatMigrateTo), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                return (Strings.Android.ActionMigrateFromGroup, null);
            });
            _actionsCache.Add(typeof(TLMessageActionChannelMigrateFrom), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                return (Strings.Android.ActionMigrateFromGroup, null);
            });
            _actionsCache.Add(typeof(TLMessageActionCustomAction), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                return (((TLMessageActionCustomAction)actionBase).Message, null);
            });
            _actionsCache.Add(typeof(TLMessageActionHistoryClear), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                return (Strings.Android.HistoryCleared, null);
            });
            _actionsCache.Add(typeof(TLMessageActionUnreadMessages), (TLMessageService message, TLMessageActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                return (Strings.Resources.UnreadMessages, null);
                //return (LocaleHelper.Declension("NewMessages", 0), null);
            });
            _actionsCache.Add(typeof(TLMessageActionPhoneCall), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                var action = actionBase as TLMessageActionPhoneCall;
                var isMissed = action.Reason is TLPhoneCallDiscardReasonMissed;

                var content = string.Empty;

                if (message.FromId == SettingsHelper.UserId)
                {
                    if (isMissed)
                    {
                        content = Strings.Android.CallMessageOutgoingMissed;
                    }
                    else
                    {
                        content = Strings.Android.CallMessageOutgoing;
                    }
                }
                else
                {
                    if (isMissed)
                    {
                        content = Strings.Android.CallMessageIncomingMissed;
                    }
                    else if (action.Reason is TLPhoneCallDiscardReasonBusy)
                    {
                        content = Strings.Android.CallMessageIncomingDeclined;
                    }
                    else
                    {
                        content = Strings.Android.CallMessageIncoming;
                    }
                }
                if (action.Duration > 0)
                {
                    String duration = LocaleHelper.FormatCallDuration(action.Duration ?? 0);
                    content = string.Format(Strings.Android.CallMessageWithDuration, content, duration);
                    //String _messageText = messageText.toString();
                    //int start = _messageText.indexOf(duration);
                    //if (start != -1)
                    //{
                    //    SpannableString sp = new SpannableString(messageText);
                    //    int end = start + duration.length();
                    //    if (start > 0 && _messageText.charAt(start - 1) == '(')
                    //    {
                    //        start--;
                    //    }
                    //    if (end < _messageText.length() && _messageText.charAt(end) == ')')
                    //    {
                    //        end++;
                    //    }
                    //    sp.setSpan(new TypefaceSpan(Typeface.DEFAULT), start, end, 0);
                    //    messageText = sp;
                    //}
                }

                return (content, null);
            });
            _actionsCache.Add(typeof(TLMessageActionPaymentSent), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                var action = actionBase as TLMessageActionPaymentSent;

                var content = string.Empty;

                String name;
                if (message.Reply is TLMessage replied)
                {
                    name = replied.From?.FullName ?? string.Empty;
                }
                else
                {
                    name = string.Empty;
                }
                if (message.Reply is TLMessage reply && reply.Media is TLMessageMediaInvoice invoice)
                {
                    content = string.Format(Strings.Android.PaymentSuccessfullyPaid, LocaleHelper.FormatCurrency(action.TotalAmount, action.Currency), name, invoice.Title);
                }
                else
                {
                    content = string.Format(Strings.Android.PaymentSuccessfullyPaidNoItem, LocaleHelper.FormatCurrency(action.TotalAmount, action.Currency), name);
                }

                return (content, null);
            });
            _actionsCache.Add(typeof(TLMessageActionPinMessage), (TLMessageService message, TLMessageActionBase actionBase, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var channel = message.Parent;
                var reply = message.Reply as TLMessage;

                if (reply == null)
                {
                    content = ReplaceWithLink(Strings.Android.ActionPinnedNoText, "un1", fromUser ?? (TLObject)channel, ref entities);
                }
                else
                {
                    if (reply.IsMusic())
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedMusic, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.IsVideo())
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedVideo, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.IsGif())
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedGif, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.IsVoice())
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedVoice, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.IsRoundVideo())
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedRound, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.IsSticker())
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedSticker, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.Media is TLMessageMediaDocument)
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedFile, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.Media is TLMessageMediaGeo)
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedGeo, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.Media is TLMessageMediaGeoLive)
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedGeoLive, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.Media is TLMessageMediaContact)
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedContact, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.Media is TLMessageMediaPhoto)
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedPhoto, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (reply.Media is TLMessageMediaGame gameMedia)
                    {
                        content = ReplaceWithLink(string.Format(Strings.Android.ActionPinnedGame, "\uD83C\uDFAE " + gameMedia.Game.Title), "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else if (!string.IsNullOrEmpty(reply.Message))
                    {
                        var mess = reply.Message;
                        if (mess.Length > 20)
                        {
                            mess = mess.Substring(0, Math.Min(mess.Length, 20)) + "...";
                        }

                        content = ReplaceWithLink(string.Format(Strings.Android.ActionPinnedText, mess), "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Android.ActionPinnedNoText, "un1", fromUser ?? (TLObject)channel, ref entities);
                    }
                }

                return (content, entities);
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

        #endregion

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
                paragraph.Inlines.Add(new Run { Text = Strings.Android.AttachPhotoExpired });
                sender.Blocks.Add(paragraph);
                return;
            }
            else if (message != null && message.Media is TLMessageMediaDocument documentMedia && documentMedia.HasTTLSeconds && (documentMedia.Document is TLDocumentEmpty || !documentMedia.HasDocument))
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run { Text = Strings.Android.AttachVideoExpired });
                sender.Blocks.Add(paragraph);
                return;
            }

            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run { Text = "Empty" });
                sender.Blocks.Add(paragraph);
            }
        }

        public static string Convert(TLMessageService serviceMessage)
        {
            var action = serviceMessage.Action;
            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                return _actionsCache[action.GetType()].Invoke(serviceMessage, action, serviceMessage.From, false).content;
            }

            return "Empty";
        }

        public static Paragraph Convert(TLMessageService serviceMessage, bool useActiveLinks)
        {
            var action = serviceMessage.Action;
            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                var result = _actionsCache[action.GetType()].Invoke(serviceMessage, action, serviceMessage.From, true);
                if (useActiveLinks && result.entities != null)
                {
                    return ReplaceEntities(serviceMessage, result.content, result.entities);
                }
                else
                {
                    var paragraphAction = new Paragraph();
                    paragraphAction.Inlines.Add(new Run { Text = result.content });
                    return paragraphAction;
                }
            }

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = "Empty" });
            return paragraph;
        }

        public static Paragraph ReplaceEntities(TLMessageCommonBase message, string text, IList<TLMessageEntityBase> entities)
        {
            var paragraph = new Paragraph();
            var span = new Span();

            ReplaceEntities(message, span, text, entities);

            paragraph.Inlines.Add(span);
            return paragraph;
        }

        public static void ReplaceEntities(TLMessageCommonBase message, Span paragraph, string text, IList<TLMessageEntityBase> entities)
        {
            var previous = 0;

            foreach (var entity in entities.OrderBy(x => x.Offset))
            {
                if (entity.Offset > previous)
                {
                    paragraph.Inlines.Add(new Run { Text = text.Substring(previous, entity.Offset - previous) });
                }

                if (entity.Length + entity.Offset > text.Length)
                {
                    previous = entity.Offset + entity.Length;
                    continue;
                }

                var type = entity.TypeId;
                if (entity is TLMessageEntityBold)
                {
                    paragraph.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontWeight = FontWeights.SemiBold });
                }
                else if (entity is TLMessageEntityTextUrl textUrl)
                {
                    var data = textUrl.Url;
                    var hyperlink = new Hyperlink();
                    hyperlink.Click += (s, args) => Hyperlink_Navigate(message, data);
                    hyperlink.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Foreground = new SolidColorBrush(Colors.White);
                    hyperlink.FontWeight = FontWeights.SemiBold;
                    paragraph.Inlines.Add(hyperlink);
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                paragraph.Inlines.Add(new Run { Text = text.Substring(previous) });
            }
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
            if (currentId.StartsWith("tg-channel://"))
            {
                var navigationService = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                if (navigationService != null)
                {
                    navigationService.Navigate(typeof(ChannelDetailsPage), new TLPeerChannel { ChannelId = int.Parse(currentId.Replace("tg-channel://", string.Empty)) });
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

        public static string ReplaceWithLink(string source, string param, TLObject obj, ref List<TLMessageEntityBase> entities)
        {
            var start = source.IndexOf(param);
            if (start >= 0)
            {
                String name;
                String id;
                if (obj is TLUser user)
                {
                    name = user.FullName;
                    id = "tg-user://" + user.Id;
                }
                else if (obj is TLChat chat)
                {
                    name = chat.Title;
                    id = "tg-chat://" + chat.Id;
                }
                else if (obj is TLChannel channel)
                {
                    name = channel.Title;
                    id = "tg-channel://" + channel.Id;
                }
                else if (obj is TLGame game)
                {
                    name = game.Title;
                    id = "tg-game://";
                }
                else
                {
                    name = "";
                    id = "0";
                }

                name = name.Replace('\n', ' ');
                source = source.Replace(param, name);

                if (entities != null)
                {
                    entities.Add(new TLMessageEntityTextUrl { Offset = start, Length = name.Length, Url = id });
                }
            }

            return source;
        }

        private static string ReplaceWithLink(string source, string param, IList<int> uids, Dictionary<int, TLUser> usersDict, ref List<TLMessageEntityBase> entities)
        {
            int start = source.IndexOf(param);
            if (start >= 0)
            {
                var names = new StringBuilder();
                for (int a = 0; a < uids.Count; a++)
                {
                    TLUser user = null;
                    if (usersDict != null)
                    {
                        user = usersDict[uids[a]];
                    }
                    if (user == null)
                    {
                        user = InMemoryCacheService.Current.GetUser(uids[a]) as TLUser;
                    }
                    if (user != null)
                    {
                        var name = user.FullName;
                        start = names.Length;
                        if (names.Length != 0)
                        {
                            names.Append(", ");
                        }

                        names.Append(name);

                        if (entities != null)
                        {
                            entities.Add(new TLMessageEntityTextUrl { Offset = start, Length = name.Length, Url = "tg-user://" + user.Id });
                        }
                    }
                }

                source = source.Replace(param, names.ToString());
            }

            return source;
        }
    }
}
