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
using Unigram.Converters;
using Unigram.Strings;
using Unigram.Views;
using Unigram.Views.Users;
using Windows.ApplicationModel.Resources;
using Windows.Globalization.DateTimeFormatting;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class AdminLogEvent
    {
        public TLChannel Channel { get; set; }
        public TLChannelAdminLogEvent Event { get; set; }
    }

    public class AdminLogHelper
    {
        private static readonly Dictionary<Type, Func<TLChannelAdminLogEvent, TLChannelAdminLogEventActionBase, TLChannel, int, string, bool, Paragraph>> _actionsCache;

        public static bool IsBannedForever(int time)
        {
            return Math.Abs(((long)time) - (Utils.CurrentTimestamp / 1000)) > 157680000;
        }

        static AdminLogHelper()
        {
            _actionsCache = new Dictionary<Type, Func<TLChannelAdminLogEvent, TLChannelAdminLogEventActionBase, TLChannel, int, string, bool, Paragraph>>();
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionParticipantToggleAdmin), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase actionBase, TLChannel channel, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var action = actionBase as TLChannelAdminLogEventActionParticipantToggleAdmin;
                var whoUser = action.PrevParticipant.User;

                var replace1 = new string[2];
                var replace2 = new string[2];
                var text = string.Empty;

                if (whoUser != null)
                {
                    replace1[0] = whoUser.FullName;
                    replace2[0] = "tg-user://" + whoUser.Id;

                    if (whoUser.HasUsername)
                    {
                        replace1[1] = "@" + whoUser.Username;
                        replace2[1] = "tg-user://" + whoUser.Id;

                        text = "{0} ({1})";
                    }
                    else
                    {
                        text = "{0}";
                    }
                }

                var builder = new StringBuilder(string.Format(AppResources.EventLogPromoted, text));
                var added = false;

                TLChannelAdminRights o = null;
                TLChannelAdminRights n = null;

                if (action.PrevParticipant is TLChannelParticipantAdmin prevAdmin)
                {
                    o = prevAdmin.AdminRights;
                }
                if (action.NewParticipant is TLChannelParticipantAdmin newAdmin)
                {
                    n = newAdmin.AdminRights;
                }

                if (o == null)
                {
                    o = new TLChannelAdminRights();
                }
                if (n == null)
                {
                    n = new TLChannelAdminRights();
                }

                if (o.IsChangeInfo != n.IsChangeInfo)
                {
                    if (!added)
                    {
                        builder.Append('\n');
                        added = true;
                    }
                    builder.Append('\n').Append(n.IsChangeInfo ? '+' : '-').Append(' ');
                    builder.Append(channel.IsMegaGroup ? AppResources.EventLogPromotedChangeGroupInfo : AppResources.EventLogPromotedChangeChannelInfo);
                }
                if (!channel.IsMegaGroup)
                {
                    if (o.IsPostMessages != n.IsPostMessages)
                    {
                        if (!added)
                        {
                            builder.Append('\n');
                            added = true;
                        }
                        builder.Append('\n').Append(n.IsPostMessages ? '+' : '-').Append(' ');
                        builder.Append(AppResources.EventLogPromotedPostMessages);
                    }
                    if (o.IsEditMessages != n.IsEditMessages)
                    {
                        if (!added)
                        {
                            builder.Append('\n');
                            added = true;
                        }
                        builder.Append('\n').Append(n.IsEditMessages ? '+' : '-').Append(' ');
                        builder.Append(AppResources.EventLogPromotedEditMessages);
                    }
                }
                if (o.IsDeleteMessages != n.IsDeleteMessages)
                {
                    if (!added)
                    {
                        builder.Append('\n');
                        added = true;
                    }
                    builder.Append('\n').Append(n.IsDeleteMessages ? '+' : '-').Append(' ');
                    builder.Append(AppResources.EventLogPromotedDeleteMessages);
                }
                if (o.IsAddAdmins != n.IsAddAdmins)
                {
                    if (!added)
                    {
                        builder.Append('\n');
                        added = true;
                    }
                    builder.Append('\n').Append(n.IsAddAdmins ? '+' : '-').Append(' ');
                    builder.Append(AppResources.EventLogPromotedAddAdmins);
                }
                if (channel.IsMegaGroup)
                {
                    if (o.IsBanUsers != n.IsBanUsers)
                    {
                        if (!added)
                        {
                            builder.Append('\n');
                            added = true;
                        }
                        builder.Append('\n').Append(n.IsBanUsers ? '+' : '-').Append(' ');
                        builder.Append(AppResources.EventLogPromotedBanUsers);
                    }
                    if (o.IsInviteUsers != n.IsInviteUsers)
                    {
                        if (!added)
                        {
                            builder.Append('\n');
                            added = true;
                        }
                        builder.Append('\n').Append(n.IsInviteUsers ? '+' : '-').Append(' ');
                        builder.Append(AppResources.EventLogPromotedAddUsers);
                    }
                    if (o.IsPinMessages != n.IsPinMessages)
                    {
                        if (!added)
                        {
                            builder.Append('\n');
                            added = true;
                        }
                        builder.Append('\n').Append(n.IsPinMessages ? '+' : '-').Append(' ');
                        builder.Append(AppResources.EventLogPromotedPinMessages);
                    }
                }

                return ReplaceLinks(null, builder.ToString(), replace1, replace2, useActiveLinks);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionParticipantToggleBan), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase actionBase, TLChannel channel, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var action = actionBase as TLChannelAdminLogEventActionParticipantToggleBan;
                var whoUser = action.PrevParticipant.User;

                var replace1 = new string[2];
                var replace2 = new string[2];
                var text = string.Empty;

                if (whoUser != null)
                {
                    replace1[0] = whoUser.FullName;
                    replace2[0] = "tg-user://" + whoUser.Id;

                    if (whoUser.HasUsername)
                    {
                        replace1[1] = "@" + whoUser.Username;
                        replace2[1] = "tg-user://" + whoUser.Id;

                        text = "{0} ({1})";
                    }
                    else
                    {
                        text = "{0}";
                    }
                }

                TLChannelBannedRights o = null;
                TLChannelBannedRights n = null;

                if (action.PrevParticipant is TLChannelParticipantBanned prevBanned)
                {
                    o = prevBanned.BannedRights;
                }
                if (action.NewParticipant is TLChannelParticipantBanned newBanned)
                {
                    n = newBanned.BannedRights;
                }

                if (!channel.IsMegaGroup || (n != null && n.IsViewMessages && (n == null || o == null || n.UntilDate == o.UntilDate)))
                {
                    if (n == null || !(o == null || n.IsViewMessages))
                    {
                        return ReplaceLinks(null, string.Format(AppResources.EventLogChannelUnrestricted, text), replace1, replace2, useActiveLinks);
                    }
                    else
                    {
                        return ReplaceLinks(null, string.Format(AppResources.EventLogChannelRestricted, text), replace1, replace2, useActiveLinks);
                    }
                }

                String bannedDuration;
                if (n == null || IsBannedForever(n.UntilDate))
                {
                    bannedDuration = AppResources.UserRestrictionsUntilForever;
                }
                else
                {
                    bannedDuration = "";
                    int duration = n.UntilDate - adminLog.Date;
                    int days = ((duration / 60) / 60) / 24;
                    duration -= ((days * 60) * 60) * 24;
                    int hours = (duration / 60) / 60;
                    int minutes = (duration - ((hours * 60) * 60)) / 60;
                    int count = 0;
                    for (int a = 0; a < 3; a++)
                    {
                        String addStr = null;
                        if (a == 0)
                        {
                            if (days != 0)
                            {
                                //addStr = LocaleController.formatPluralString("Days", days);
                                addStr = $"{days} days";
                                count++;
                            }
                        }
                        else if (a == 1)
                        {
                            if (hours != 0)
                            {
                                //addStr = LocaleController.formatPluralString("Hours", hours);
                                addStr = $"{hours} hours";
                                count++;
                            }
                        }
                        else if (minutes != 0)
                        {
                            //addStr = LocaleController.formatPluralString("Minutes", minutes);
                            addStr = $"{minutes} minutes";
                            count++;
                        }
                        if (addStr != null)
                        {
                            if (bannedDuration.Length > 0)
                            {
                                bannedDuration = bannedDuration + ", ";
                            }

                            bannedDuration = bannedDuration + addStr;
                        }
                        if (count == 2)
                        {
                            break;
                        }
                    }
                }

                var builder = new StringBuilder(string.Format(AppResources.EventLogRestrictedUntil, text, bannedDuration));
                var added = false;
                if (o == null)
                {
                    o = new TLChannelBannedRights();
                }
                if (n == null)
                {
                    n = new TLChannelBannedRights();
                }
                if (o.IsViewMessages != n.IsViewMessages)
                {
                    if (!added)
                    {
                        builder.Append('\n');
                        added = true;
                    }

                    builder.Append('\n').Append(!n.IsViewMessages ? '+' : '-').Append(' ');
                    builder.Append(AppResources.EventLogRestrictedReadMessages);
                }
                if (o.IsSendMessages != n.IsSendMessages)
                {
                    if (!added)
                    {
                        builder.Append('\n');
                        added = true;
                    }
                    builder.Append('\n').Append(!n.IsSendMessages ? '+' : '-').Append(' ');
                    builder.Append(AppResources.EventLogRestrictedSendMessages);
                }
                if (!(o.IsSendStickers == n.IsSendStickers && o.IsSendInline == n.IsSendInline && o.IsSendGifs == n.IsSendGifs && o.IsSendGames == n.IsSendGames))
                {
                    if (!added)
                    {
                        builder.Append('\n');
                        added = true;
                    }
                    builder.Append('\n').Append(!n.IsSendStickers ? '+' : '-').Append(' ');
                    builder.Append(AppResources.EventLogRestrictedSendStickers);
                }
                if (o.IsSendMedia != n.IsSendMedia)
                {
                    if (!added)
                    {
                        builder.Append('\n');
                        added = true;
                    }
                    builder.Append('\n').Append(!n.IsSendMedia ? '+' : '-').Append(' ');
                    builder.Append(AppResources.EventLogRestrictedSendMedia);
                }
                if (o.IsEmbedLinks != n.IsEmbedLinks)
                {
                    if (!added)
                    {
                        builder.Append('\n');
                    }
                    builder.Append('\n').Append(!n.IsEmbedLinks ? '+' : '-').Append(' ');
                    builder.Append(AppResources.EventLogRestrictedSendEmbed);
                }

                return ReplaceLinks(null, builder.ToString(), replace1, replace2, useActiveLinks);
            });
            //_actionsCache.Add(typeof(TLMessageActionDate), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(adminLog, new DateTimeFormatter("day month").Format(Utils.UnixTimestampToDateTime(((TLMessageActionDate)action).Date))));
            //_actionsCache.Add(typeof(TLMessageActionEmpty), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(adminLog, AppResources.MessageActionEmpty));
            //_actionsCache.Add(typeof(TLMessageActionGameScore), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            //{
            //    var value = ((TLMessageActionGameScore)action).Score;
            //    var won = value == 0 || value > 0;
            //    var serviceMessage = message as TLMessageService;
            //    if (serviceMessage != null)
            //    {
            //        var game = GetGame(serviceMessage);
            //        if (game != null)
            //        {
            //            var text = game.Title.ToString();
            //            //TLMessageCommon tLMessageCommon = serviceMessage.Reply as TLMessageCommonBase;
            //            //if (tLMessageCommon != null)
            //            //{
            //            //    text = ServiceMessageToTextConverter.GetGameFullNameString(text, serviceMessage.Index, serviceMessage.ToId, useActiveLinks);
            //            //}

            //            if (fromUserId == SettingsHelper.UserId)
            //            {
            //                return ReplaceLinks(serviceMessage, won ? AppResources.YourScoredAtGamePlural : AppResources.YourScoredAtGame, new[] { value.ToString(), game.Title }, new[] { "tg-bold://", "tg-game://" }, useActiveLinks);
            //            }

            //            return ReplaceLinks(serviceMessage, won ? AppResources.UserScoredAtGamePlural : AppResources.UserScoredAtGame, new[] { fromUserFullName, value.ToString(), game.Title }, new[] { "tg-user://" + fromUserId, "tg-bold://", "tg-game://" }, useActiveLinks);
            //        }

            //        if (fromUserId == SettingsHelper.UserId)
            //        {
            //            return ReplaceLinks(serviceMessage, string.Format(won ? AppResources.YourScoredPlural : AppResources.YourScored, value));
            //        }

            //        return ReplaceLinks(serviceMessage, won ? AppResources.UserScoredPlural : AppResources.UserScored, new[] { fromUserFullName, value.ToString() }, new[] { "tg-user://" + fromUserId, "tg-bold://" }, useActiveLinks);
            //    }

            //    return ReplaceLinks(serviceMessage, won ? AppResources.UserScoredPlural : AppResources.UserScored, new[] { AppResources.UserNominativeSingular, value.ToString() }, new[] { "tg-bold://", "tg-bold://" }, useActiveLinks);
            //});
            //_actionsCache.Add(typeof(TLMessageActionChatCreate), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(adminLog, AppResources.MessageActionChatCreate, new[] { fromUserFullName, ((TLMessageActionChatCreate)action).Title }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            //_actionsCache.Add(typeof(TLMessageActionChatEditPhoto), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(adminLog, AppResources.MessageActionChatEditPhoto, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            //_actionsCache.Add(typeof(TLMessageActionChatEditTitle), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(adminLog, AppResources.MessageActionChatEditTitle, new[] { fromUserFullName, ((TLMessageActionChatEditTitle)action).Title }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            //_actionsCache.Add(typeof(TLMessageActionChatDeletePhoto), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(adminLog, AppResources.MessageActionChatDeletePhoto, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            //_actionsCache.Add(typeof(TLChannelAdminLogEventActionParticipantInvite), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            //{
            //    var users = ((TLMessageActionChatAddUser)action).Users;
            //    var names = new List<string>();
            //    var codes = new List<string>();

            //    for (int i = 0; i < users.Count; i++)
            //    {
            //        var item = users[i];
            //        var user = InMemoryCacheService.Current.GetUser(item);
            //        if (user != null)
            //        {
            //            names.Add(user.FullName);
            //            codes.Add($"{{{i + 1}}}");
            //        }
            //    }

            //    if (users.Count == 1 && users[0] == fromUserId)
            //    {
            //        return ReplaceLinks(message, AppResources.MessageActionChatAddSelf, new[] { names[0] }, new[] { "tg-user://" + users[0] }, useActiveLinks);
            //    }

            //    // TODO: replace last ", " with "and "

            //    var codesReplace = "{1}";
            //    if (codes.Count > 1)
            //    {
            //        codesReplace = string.Concat(string.Join(", ", codes.Take(codes.Count - 1)), " and ", codes.Last());
            //    }

            //    var tags = users.Select(x => "tg-user://" + x).ToList();
            //    names.Insert(0, fromUserFullName);
            //    tags.Insert(0, "tg-user://" + fromUserId);

            //    return ReplaceLinks(message, string.Format(AppResources.MessageActionChatAddUser, "{0}", codesReplace), names.ToArray(), tags.ToArray(), useActiveLinks);
            //});
            //_actionsCache.Add(typeof(TLMessageActionChatDeleteUser), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            //{
            //    var userId = ((TLMessageActionChatDeleteUser)action).UserId;
            //    var user = InMemoryCacheService.Current.GetUser(userId);
            //    var userFullName = user.FullName;
            //    if (userId != fromUserId)
            //    {
            //        return ReplaceLinks(message, AppResources.MessageActionChatDeleteUser, new[] { fromUserFullName, userFullName }, new[] { "tg-user://" + fromUserId, "tg-user://" + userId }, useActiveLinks);
            //    }

            //    if (fromUserId == SettingsHelper.UserId)
            //    {
            //        return ReplaceLinks(message, AppResources.MessageActionLeftGroupSelf);
            //    }

            //    return ReplaceLinks(message, AppResources.MessageActionUserLeftGroup, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
            //});
            ////_actionsCache.Add(typeof(TLMessageActionUnreadMessages), (TLMessageBase message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => AppResources.UnreadMessages.ToLowerInvariant());
            ////_actionsCache.Add(typeof(TLMessageActionContactRegistered), delegate (TLMessageBase message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks)
            ////{
            ////    TLInt userId = ((TLMessageActionContactRegistered)action).UserId;
            ////    TLUserBase user = IoC.Get<ICacheService>(null).GetUser(userId);
            ////    string text = (user != null) ? user.FirstName.ToString() : AppResources.User;
            ////    if (string.IsNullOrEmpty(text) && user != null)
            ////    {
            ////        text = user.FullName;
            ////    }
            ////    return string.Format(AppResources.ContactRegistered, text);
            ////});
            //_actionsCache.Add(typeof(TLMessageActionChatJoinedByLink), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(adminLog, AppResources.MessageActionChatJoinedByLink, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            ////_actionsCache.Add(typeof(TLMessageActionMessageGroup), delegate (TLMessageBase message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks)
            ////{
            ////    int value = ((TLMessageActionMessageGroup)action).Group.Count.Value;
            ////    return Language.Declension(value, AppResources.CommentNominativeSingular, AppResources.CommentNominativePlural, AppResources.CommentGenitiveSingular, AppResources.CommentGenitivePlural, null, null).ToLower(CultureInfo.get_CurrentUICulture());
            ////});
            //_actionsCache.Add(typeof(TLMessageActionChatMigrateTo), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            //{
            //    var channelId = ((TLMessageActionChatMigrateTo)action).ChannelId;
            //    var channel = InMemoryCacheService.Current.GetChat(channelId) as TLChannel;
            //    var fullName = channel != null ? channel.DisplayName : string.Empty;
            //    if (string.IsNullOrWhiteSpace(fullName))
            //    {
            //        return ReplaceLinks(message, AppResources.MessageActionChatMigrateToGeneric);
            //    }

            //    return ReplaceLinks(message, AppResources.MessageActionChatMigrateTo, new[] { fullName }, new[] { "tg-channel://" + channelId }, useActiveLinks);
            //});
            //_actionsCache.Add(typeof(TLMessageActionChannelMigrateFrom), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(adminLog, AppResources.MessageActionChannelMigrateFrom));
            //_actionsCache.Add(typeof(TLMessageActionHistoryClear), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(adminLog, "History was cleared"));
            //_actionsCache.Add(typeof(TLMessageActionUnreadMessages), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(adminLog, "Unread messages"));
            //_actionsCache.Add(typeof(TLMessageActionPhoneCall), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            //{
            //    if (message is TLMessageService serviceMessage && action is TLMessageActionPhoneCall phoneCallAction)
            //    {
            //        var loader = ResourceLoader.GetForCurrentView("Resources");
            //        var text = string.Empty;

            //        var outgoing = serviceMessage.IsOut;
            //        var missed = phoneCallAction.Reason is TLPhoneCallDiscardReasonMissed || phoneCallAction.Reason is TLPhoneCallDiscardReasonBusy;

            //        var type = loader.GetString(missed ? (outgoing ? "CallCanceled" : "CallMissed") : (outgoing ? "CallOutgoing" : "CallIncoming"));
            //        var duration = string.Empty;

            //        if (!missed && (phoneCallAction.Duration ?? 0) > 0)
            //        {
            //            duration = BindConvert.Current.CallDuration(phoneCallAction.Duration ?? 0);
            //        }

            //        return ReplaceLinks(serviceMessage, missed || (phoneCallAction.Duration ?? 0) < 1 ? type : string.Format(AppResources.CallTimeFormat, type, duration));
            //    }

            //    return null;
            //});
            //_actionsCache.Add(typeof(TLMessageActionPaymentSent), (TLChannelAdminLogEvent adminLog, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            //{
            //    if (message is TLMessageService serviceMessage && action is TLMessageActionPaymentSent paymentSentAction)
            //    {
            //        if (serviceMessage.Reply is TLMessage replied && replied.Media is TLMessageMediaInvoice invoiceMedia)
            //        {
            //            var amount = BindConvert.Current.FormatAmount(paymentSentAction.TotalAmount, paymentSentAction.Currency);
            //            var userName = replied.From?.FullName ?? "User";
            //            var userId = replied.FromId ?? fromUserId;

            //            return ReplaceLinks(serviceMessage, "You have just successfully transferred {0} to {1} for {2}", new[] { amount, userName, invoiceMedia.Title }, new[] { "tg-bold://", "tg-user://" + userId, "tg-message://" + replied.Id }, useActiveLinks);
            //        }
            //        else if (serviceMessage.Parent is TLUser user)
            //        {
            //            var amount = BindConvert.Current.FormatAmount(paymentSentAction.TotalAmount, paymentSentAction.Currency);
            //            var userName = user?.FullName ?? "User";
            //            var userId = user?.Id ?? fromUserId;

            //            return ReplaceLinks(serviceMessage, "You have just successfully transferred {0} to {1}", new[] { amount, userName }, new[] { "tg-bold://", "tg-user://" + userId }, useActiveLinks);
            //        }
            //    }

            //    return null;
            //});
        }

        #region Message
        public static AdminLogEvent GetMessage(DependencyObject obj)
        {
            return (AdminLogEvent)obj.GetValue(MessageProperty);
        }

        public static void SetMessage(DependencyObject obj, AdminLogEvent value)
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
            DependencyProperty.RegisterAttached("Message", typeof(AdminLogEvent), typeof(AdminLogHelper), new PropertyMetadata(null, OnMessageChanged));

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as RichTextBlock;
            var newValue = e.NewValue as AdminLogEvent;

            OnMessageChanged(sender, newValue);
        }

        private static void OnMessageChanged(RichTextBlock sender, AdminLogEvent newValue)
        {
            sender.IsTextSelectionEnabled = false;
            sender.Blocks.Clear();

            var serviceMessage = newValue.Event as TLChannelAdminLogEvent;
            if (serviceMessage != null)
            {
                sender.Blocks.Add(Convert(newValue.Channel, serviceMessage, true));
                return;
            }

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = AppResources.MessageActionEmpty });
            sender.Blocks.Add(paragraph);
        }

        //public static string Convert(TLMessageService serviceMessage)
        //{
        //    var paragraph = Convert(serviceMessage, false);
        //    if (paragraph != null && paragraph.Inlines.Count > 0)
        //    {
        //        var run = paragraph.Inlines[0] as Run;
        //        if (run != null)
        //        {
        //            return run.Text;
        //        }
        //    }

        //    return AppResources.MessageActionEmpty;
        //}

        public static Paragraph Convert(TLChannel channel, TLChannelAdminLogEvent adminLog, bool useActiveLinks)
        {
            var fromId = adminLog.UserId;
            var user = InMemoryCacheService.Current.GetUser(fromId);
            var userFullName = user != null ? user.FullName : AppResources.UserNominativeSingular;
            var action = adminLog.Action;

            //if (serviceMessage.ToId is TLPeerChannel)
            //{
            //    var channel = InMemoryCacheService.Current.GetChat(serviceMessage.ToId.Id) as TLChannel;
            //    var flag = channel != null && channel.IsMegaGroup;

            //    var pinMessageAction = action as TLMessageActionPinMessage;
            //    if (pinMessageAction != null)
            //    {
            //        var replyToMsgId = serviceMessage.ReplyToMsgId;
            //        if (replyToMsgId != null && channel != null)
            //        {
            //            var repliedMessage = InMemoryCacheService.Current.GetMessage(replyToMsgId, channel.Id) as TLMessage;
            //            if (repliedMessage != null)
            //            {
            //                var gameMedia = repliedMessage.Media as TLMessageMediaGame;
            //                if (gameMedia != null)
            //                {
            //                    return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinGame }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                }

            //                var photoMedia = repliedMessage.Media as TLMessageMediaPhoto;
            //                if (photoMedia != null)
            //                {
            //                    return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinPhoto }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                }

            //                var documentMedia = repliedMessage.Media as TLMessageMediaDocument;
            //                if (documentMedia != null)
            //                {
            //                    if (TLMessage.IsSticker(documentMedia.Document))
            //                    {
            //                        var emoji = string.Empty;

            //                        var documentSticker = documentMedia.Document as TLDocument;
            //                        if (documentSticker != null)
            //                        {
            //                            var attribute = documentSticker.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
            //                            if (attribute != null)
            //                            {
            //                                emoji = $"{attribute.Alt} ";
            //                            }
            //                        }

            //                        return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, string.Format(AppResources.MessageActionPinSticker, emoji) }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                    }
            //                    if (TLMessage.IsVoice(documentMedia.Document))
            //                    {
            //                        return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinVoiceMessage }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                    }
            //                    if (TLMessage.IsMusic(documentMedia.Document))
            //                    {
            //                        return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinTrack }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                    }
            //                    if (TLMessage.IsVideo(documentMedia.Document))
            //                    {
            //                        return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinVideo }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                    }
            //                    if (TLMessage.IsGif(documentMedia.Document))
            //                    {
            //                        return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinGif }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                    }

            //                    return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinFile }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                }

            //                var contactMedia = repliedMessage.Media as TLMessageMediaContact;
            //                if (contactMedia != null)
            //                {
            //                    return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinContact }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                }

            //                var geoMedia = repliedMessage.Media as TLMessageMediaGeo;
            //                if (geoMedia != null)
            //                {
            //                    return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinMap }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                }

            //                var venueMedia = repliedMessage.Media as TLMessageMediaVenue;
            //                if (venueMedia != null)
            //                {
            //                    return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinMap }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                }

            //                if (repliedMessage.Message.Length > 0)
            //                {
            //                    if (repliedMessage.Message.Length > 20)
            //                    {
            //                        return ReplaceLinks(serviceMessage, AppResources.MessageActionPinText, new[] { userFullName, repliedMessage.Message.Substring(0, 20).Replace("\r\n", "\n").Replace("\n", " ") + "…" }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                    }

            //                    return ReplaceLinks(serviceMessage, AppResources.MessageActionPinText, new[] { userFullName, repliedMessage.Message.Replace("\r\n", "\n").Replace("\n", " ") }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //                }

            //                return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinMessage }, new[] { "tg-user://" + fromId, "tg-message://" + repliedMessage.Id }, useActiveLinks);
            //            }

            //            return ReplaceLinks(serviceMessage, AppResources.MessageActionPin, new[] { userFullName, AppResources.MessageActionPinMessage }, new[] { "tg-user://" + fromId, "tg-message://" + serviceMessage.ReplyToMsgId }, useActiveLinks);
            //        }
            //    }

            //    var channelCreateAction = action as TLMessageActionChannelCreate;
            //    if (channelCreateAction != null)
            //    {
            //        if (!flag)
            //        {
            //            return ReplaceLinks(serviceMessage, AppResources.MessageActionChannelCreate);
            //        }

            //        return ReplaceLinks(serviceMessage, AppResources.MessageActionChatCreate, new[] { userFullName, channelCreateAction.Title }, new[] { "tg-user://" + fromId }, useActiveLinks);
            //    }

            //    var editPhotoAction = action as TLMessageActionChatEditPhoto;
            //    if (editPhotoAction != null)
            //    {
            //        if (!flag)
            //        {
            //            return ReplaceLinks(serviceMessage, AppResources.MessageActionChannelEditPhoto);
            //        }

            //        return ReplaceLinks(serviceMessage, AppResources.MessageActionChatEditPhoto, new[] { userFullName }, new[] { "tg-user://" + fromId }, useActiveLinks);
            //    }

            //    var deletePhotoAction = action as TLMessageActionChatDeletePhoto;
            //    if (deletePhotoAction != null)
            //    {
            //        if (!flag)
            //        {
            //            return ReplaceLinks(serviceMessage, AppResources.MessageActionChannelDeletePhoto);
            //        }

            //        return ReplaceLinks(serviceMessage, AppResources.MessageActionChatDeletePhoto, new[] { userFullName }, new[] { "tg-user://" + fromId }, useActiveLinks);
            //    }

            //    var editTitleAction = action as TLMessageActionChatEditTitle;
            //    if (editTitleAction != null)
            //    {
            //        if (!flag)
            //        {
            //            return ReplaceLinks(serviceMessage, string.Format(AppResources.MessageActionChannelEditTitle, editTitleAction.Title));
            //        }

            //        return ReplaceLinks(serviceMessage, AppResources.MessageActionChatEditTitle, new[] { userFullName, editTitleAction.Title }, new[] { "tg-user://" + fromId }, useActiveLinks);
            //    }
            //}

            if (action != null && _actionsCache.ContainsKey(action.GetType()))
            {
                return _actionsCache[action.GetType()].Invoke(adminLog, action, channel, fromId, userFullName, useActiveLinks);
            }

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = AppResources.MessageActionEmpty });
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

                    if (users?.Length > i && identifiers?.Length > i && identifiers[i] != null)
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
                            //hyperlink.Foreground = new SolidColorBrush(Colors.White);
                            hyperlink.Inlines.Add(new Run { Text = users[i] /*, FontWeight = FontWeights.SemiBold*/ });
                            paragraph.Inlines.Add(hyperlink);
                        }
                    }
                    else if (users?.Length > i && users[i] != null)
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
    }
}
