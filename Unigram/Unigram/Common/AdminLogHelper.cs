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
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Strings;
using Unigram.Views;
using Unigram.Views.Users;
using Windows.ApplicationModel.Resources;
using Windows.Globalization.DateTimeFormatting;
using Windows.System.UserProfile;
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
        public static bool IsBannedForever(int time)
        {
            return Math.Abs(((long)time) - (Utils.CurrentTimestamp / 1000)) > 157680000;
        }

        private static readonly Dictionary<Type, Func<TLMessageService, TLChannelAdminLogEventActionBase, int, string, bool, Paragraph>> _actionsCache;

        static AdminLogHelper()
        {
            _actionsCache = new Dictionary<Type, Func<TLMessageService, TLChannelAdminLogEventActionBase, int, string, bool, Paragraph>>();
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionChangeTitle), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var changeTitleAction = action as TLChannelAdminLogEventActionChangeTitle;

                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;
                if (channel.IsMegaGroup)
                {
                    return ReplaceLinks(message, string.Format(Strings.EventLogStrings.EventLogEditedGroupTitle, "{0}", changeTitleAction.NewValue), new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, string.Format(Strings.EventLogStrings.EventLogEditedChannelTitle, "{0}", changeTitleAction.NewValue), new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionChangeAbout), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var changeAboutAction = action as TLChannelAdminLogEventActionChangeAbout;

                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;
                if (channel.IsMegaGroup)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogEditedGroupDescription, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogEditedChannelDescription, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionChangeUsername), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var changeUsernameAction = action as TLChannelAdminLogEventActionChangeUsername;
                if (string.IsNullOrEmpty(changeUsernameAction.NewValue))
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogRemovedGroupLink, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogChangedGroupLink, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionChangePhoto), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var changePhotoAction = action as TLChannelAdminLogEventActionChangePhoto;
                var empty = changePhotoAction.NewPhoto is TLChatPhotoEmpty;

                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;
                if (channel.IsMegaGroup)
                {
                    return ReplaceLinks(message, empty ? Strings.EventLogStrings.EventLogRemovedGroupPhoto : Strings.EventLogStrings.EventLogEditedGroupPhoto, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, empty ? Strings.EventLogStrings.EventLogRemovedChannelPhoto : Strings.EventLogStrings.EventLogEditedChannelPhoto, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionChangeStickerSet), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var changeStickerSetAction = action as TLChannelAdminLogEventActionChangeStickerSet;

                var newStickerset = changeStickerSetAction.NewStickerSet;
                var oldStickerset = changeStickerSetAction.PrevStickerSet;
                if (newStickerset == null || newStickerset is TLInputStickerSetEmpty)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogRemovedStickersSet, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else if (newStickerset is TLInputStickerSetShortName shortName)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogChangedStickersSet, new[] { fromUserFullName, "sticker set" }, new[] { "tg-user://" + fromUserId, "tg-stickers://" + shortName.ShortName }, useActiveLinks);
                }
                else if (newStickerset is TLInputStickerSetID id)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogChangedStickersSet, new[] { fromUserFullName, "sticker set" }, new[] { "tg-user://" + fromUserId, "tg-stickers://" + id.Id + "?" + id.AccessHash }, useActiveLinks);
                }

                return ReplaceLinks(message, Strings.EventLogStrings.EventLogChangedStickersSet, new[] { fromUserFullName, "sticker set" }, new[] { "tg-user://" + fromUserId, "tg-bold://" }, useActiveLinks);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionToggleInvites), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var toggleInvitesAction = action as TLChannelAdminLogEventActionToggleInvites;
                if (toggleInvitesAction.NewValue)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogToggledInvitesOn, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogToggledInvitesOff, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionToggleSignatures), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var toggleSignaturesAction = action as TLChannelAdminLogEventActionToggleSignatures;
                if (toggleSignaturesAction.NewValue)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogToggledSignaturesOn, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogToggledSignaturesOff, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionTogglePreHistoryHidden), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var togglePreHistoryHiddenAction = action as TLChannelAdminLogEventActionTogglePreHistoryHidden;
                if (togglePreHistoryHiddenAction.NewValue)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogToggledInvitesHistoryOn, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogToggledInvitesHistoryOff, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionUpdatePinned), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var updatePinnedAction = action as TLChannelAdminLogEventActionUpdatePinned;
                if (updatePinnedAction.Message is TLMessageEmpty)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogUnpinnedMessages, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogPinnedMessages, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionEditMessage), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var editMessageAction = action as TLChannelAdminLogEventActionEditMessage;

                var newMessage = editMessageAction.NewMessage as TLMessage;
                if (newMessage.Media == null || (newMessage.Media is TLMessageMediaEmpty) || (newMessage.Media is TLMessageMediaWebPage) || !string.IsNullOrEmpty(newMessage.Message))
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogEditedMessages, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogEditedCaption, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionDeleteMessage), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) => ReplaceLinks(message, Strings.EventLogStrings.EventLogDeletedMessages, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks));
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionParticipantJoin), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;
                if (channel.IsMegaGroup)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogGroupJoined, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogChannelJoined, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionParticipantLeave), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;
                if (channel.IsMegaGroup)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogLeftGroup, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogLeftChannel, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionParticipantInvite), (TLMessageService message, TLChannelAdminLogEventActionBase action, int fromUserId, string fromUserFullName, bool useActiveLinks) =>
            {
                var participantInviteAction = action as TLChannelAdminLogEventActionParticipantInvite;

                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;

                var whoUser = participantInviteAction.Participant.User;
                if (whoUser.Id != fromUserId)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogAdded, new[] { fromUserFullName, whoUser.FullName }, new[] { "tg-user://" + fromUserId, "tg-user://" + whoUser.Id }, useActiveLinks);
                }
                else if (channel.IsMegaGroup)
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogGroupJoined, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
                else
                {
                    return ReplaceLinks(message, Strings.EventLogStrings.EventLogChannelJoined, new[] { fromUserFullName }, new[] { "tg-user://" + fromUserId }, useActiveLinks);
                }
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
            DependencyProperty.RegisterAttached("Message", typeof(TLMessageBase), typeof(AdminLogHelper), new PropertyMetadata(null, OnMessageChanged));

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
            paragraph.Inlines.Add(new Run { Text = Messages.MessageActionEmpty });
            sender.Blocks.Add(paragraph);
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

        public static Paragraph Convert(TLMessageService serviceMessage, bool useActiveLinks)
        {
            var fromId = serviceMessage.FromId;
            var user = InMemoryCacheService.Current.GetUser(fromId ?? 0);
            var userFullName = user != null ? user.FullName : Strings.Nouns.UserNominativeSingular;
            var action = serviceMessage.Action;

            if (serviceMessage.ToId is TLPeerChannel)
            {
                var channel = InMemoryCacheService.Current.GetChat(serviceMessage.ToId.Id) as TLChannel;

                if (action is TLMessageActionAdminLogEvent eventAction && _actionsCache.TryGetValue(eventAction.Event.Action.GetType(), out Func<TLMessageService, TLChannelAdminLogEventActionBase, int, string, bool, Paragraph> func))
                {
                    return func.Invoke(serviceMessage, eventAction.Event.Action, fromId.Value, userFullName, useActiveLinks);
                }
            }

            if (action is TLMessageActionDate dateAction)
            {
                return ReplaceLinks(serviceMessage, DateTimeToFormatConverter.ConvertDayGrouping(Utils.UnixTimestampToDateTime(dateAction.Date)));
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

        private static async void Hyperlink_Navigate(TLMessageCommonBase message, string currentId)
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
            else if (currentId.StartsWith("tg-stickers://"))
            {
                var split = currentId.Replace("tg-stickers://", string.Empty).Split('?');

                TLInputStickerSetBase input;
                if (split.Length > 1)
                {
                    input = new TLInputStickerSetID { Id = long.Parse(split[0]), AccessHash = long.Parse(split[1]) };
                }
                else
                {
                    input = new TLInputStickerSetShortName { ShortName = split[0] };
                }

                await StickerSetView.Current.ShowAsync(input);
            }
        }
    }
}
