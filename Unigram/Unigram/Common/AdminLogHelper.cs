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
    public class AdminLogHelper
    {
        public static bool IsBannedForever(int time)
        {
            return Math.Abs(((long)time) - (Utils.CurrentTimestamp / 1000)) > 157680000;
        }

        private static readonly Dictionary<Type, Func<TLMessageService, TLChannelAdminLogEventActionBase, TLUser, bool, (string content, IList<TLMessageEntityBase> entities)>> _actionsCache;

        static AdminLogHelper()
        {
            _actionsCache = new Dictionary<Type, Func<TLMessageService, TLChannelAdminLogEventActionBase, TLUser, bool, (string content, IList<TLMessageEntityBase> entities)>>();
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionChangeTitle), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var changeTitleAction = action as TLChannelAdminLogEventActionChangeTitle;

                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;
                if (channel.IsMegaGroup)
                {
                    content = ReplaceWithLink(string.Format(Strings.Android.EventLogEditedGroupTitle, changeTitleAction.NewValue), "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(string.Format(Strings.Android.EventLogEditedChannelTitle, changeTitleAction.NewValue), "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionChangeAbout), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var changeAboutAction = action as TLChannelAdminLogEventActionChangeAbout;

                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;
                if (channel.IsMegaGroup)
                {
                    content = ReplaceWithLink(Strings.Android.EventLogEditedGroupDescription, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogEditedChannelDescription, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionChangeUsername), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var changeUsernameAction = action as TLChannelAdminLogEventActionChangeUsername;
                if (string.IsNullOrEmpty(changeUsernameAction.NewValue))
                {
                    content = ReplaceWithLink(Strings.Android.EventLogRemovedGroupLink, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogChangedGroupLink, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionChangePhoto), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var changePhotoAction = action as TLChannelAdminLogEventActionChangePhoto;
                var empty = changePhotoAction.NewPhoto is TLChatPhotoEmpty;

                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;
                if (channel.IsMegaGroup)
                {
                    content = ReplaceWithLink(empty ? Strings.Android.EventLogRemovedWGroupPhoto : Strings.Android.EventLogEditedGroupPhoto, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(empty ? Strings.Android.EventLogRemovedChannelPhoto : Strings.Android.EventLogEditedChannelPhoto, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionChangeStickerSet), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var changeStickerSetAction = action as TLChannelAdminLogEventActionChangeStickerSet;

                var newStickerset = changeStickerSetAction.NewStickerSet;
                var oldStickerset = changeStickerSetAction.PrevStickerSet;
                if (newStickerset == null || newStickerset is TLInputStickerSetEmpty)
                {
                    content = ReplaceWithLink(Strings.Android.EventLogRemovedStickersSet, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogChangedStickersSet, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionToggleInvites), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var toggleInvitesAction = action as TLChannelAdminLogEventActionToggleInvites;
                if (toggleInvitesAction.NewValue)
                {
                    content = ReplaceWithLink(Strings.Android.EventLogToggledInvitesOn, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogToggledInvitesOff, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionToggleSignatures), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var toggleSignaturesAction = action as TLChannelAdminLogEventActionToggleSignatures;
                if (toggleSignaturesAction.NewValue)
                {
                    content = ReplaceWithLink(Strings.Android.EventLogToggledSignaturesOn, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogToggledSignaturesOff, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionTogglePreHistoryHidden), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var togglePreHistoryHiddenAction = action as TLChannelAdminLogEventActionTogglePreHistoryHidden;
                if (togglePreHistoryHiddenAction.NewValue)
                {
                    content = ReplaceWithLink(Strings.Android.EventLogToggledInvitesHistoryOn, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogToggledInvitesHistoryOff, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionUpdatePinned), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var updatePinnedAction = action as TLChannelAdminLogEventActionUpdatePinned;
                if (updatePinnedAction.Message is TLMessageEmpty)
                {
                    content = ReplaceWithLink(Strings.Android.EventLogUnpinnedMessages, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogPinnedMessages, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionEditMessage), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var editMessageAction = action as TLChannelAdminLogEventActionEditMessage;

                var newMessage = editMessageAction.NewMessage as TLMessage;
                if (newMessage.Media == null || (newMessage.Media is TLMessageMediaEmpty) || (newMessage.Media is TLMessageMediaWebPage) || !string.IsNullOrEmpty(newMessage.Message))
                {
                    content = ReplaceWithLink(Strings.Android.EventLogEditedMessages, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogEditedCaption, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionDeleteMessage), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                content = ReplaceWithLink(Strings.Android.EventLogDeletedMessages, "un1", fromUser, ref entities);

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionParticipantJoin), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;
                if (channel.IsMegaGroup)
                {
                    content = ReplaceWithLink(Strings.Android.EventLogGroupJoined, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogChannelJoined, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionParticipantLeave), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;
                if (channel.IsMegaGroup)
                {
                    content = ReplaceWithLink(Strings.Android.EventLogLeftGroup, "un1", fromUser, ref entities);
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogLeftChannel, "un1", fromUser, ref entities);
                }

                return (content, entities);
            });
            _actionsCache.Add(typeof(TLChannelAdminLogEventActionParticipantInvite), (TLMessageService message, TLChannelAdminLogEventActionBase action, TLUser fromUser, bool useActiveLinks) =>
            {
                var content = string.Empty;
                var entities = useActiveLinks ? new List<TLMessageEntityBase>() : null;

                var participantInviteAction = action as TLChannelAdminLogEventActionParticipantInvite;

                var channel = InMemoryCacheService.Current.GetChat(message.ToId.Id) as TLChannel;

                var whoUser = participantInviteAction.Participant.User;
                if (participantInviteAction.Participant.UserId == message.FromId)
                {
                    if (channel.IsMegaGroup)
                    {
                        content = ReplaceWithLink(Strings.Android.EventLogGroupJoined, "un1", fromUser, ref entities);
                    }
                    else
                    {
                        content = ReplaceWithLink(Strings.Android.EventLogChannelJoined, "un1", fromUser, ref entities);
                    }
                }
                else
                {
                    content = ReplaceWithLink(Strings.Android.EventLogAdded, "un1", fromUser, ref entities);
                    content = ReplaceWithLink(content, "un2", whoUser, ref entities);
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
            paragraph.Inlines.Add(new Run { Text = "Empty" });
            sender.Blocks.Add(paragraph);
        }

        public static string Convert(TLMessageService serviceMessage)
        {
            var action = serviceMessage.Action;

            if (serviceMessage.ToId is TLPeerChannel)
            {
                var channel = InMemoryCacheService.Current.GetChat(serviceMessage.ToId.Id) as TLChannel;

                if (action is TLMessageActionAdminLogEvent eventAction && _actionsCache.TryGetValue(eventAction.Event.Action.GetType(), out Func<TLMessageService, TLChannelAdminLogEventActionBase, TLUser, bool, (string content, IList<TLMessageEntityBase> entities)> func))
                {
                    return func.Invoke(serviceMessage, eventAction.Event.Action, serviceMessage.From, false).content;
                }
            }

            if (action is TLMessageActionDate dateAction)
            {
                return DateTimeToFormatConverter.ConvertDayGrouping(Utils.UnixTimestampToDateTime(dateAction.Date));
            }

            return "Empty";
        }

        public static Paragraph Convert(TLMessageService serviceMessage, bool useActiveLinks)
        {
            var action = serviceMessage.Action;

            if (serviceMessage.ToId is TLPeerChannel)
            {
                var channel = InMemoryCacheService.Current.GetChat(serviceMessage.ToId.Id) as TLChannel;

                if (action is TLMessageActionAdminLogEvent eventAction && _actionsCache.TryGetValue(eventAction.Event.Action.GetType(), out Func<TLMessageService, TLChannelAdminLogEventActionBase, TLUser, bool, (string content, IList<TLMessageEntityBase> entities)> func))
                {
                    var result = func.Invoke(serviceMessage, eventAction.Event.Action, serviceMessage.From, true);
                    if (useActiveLinks && result.entities != null)
                    {
                        return ServiceHelper.ReplaceEntities(serviceMessage, result.content, result.entities);
                    }
                    else
                    {
                        var paragraphAction = new Paragraph();
                        paragraphAction.Inlines.Add(new Run { Text = result.content });
                        return paragraphAction;
                    }
                }
            }

            if (action is TLMessageActionDate dateAction)
            {
                var paragraphDate = new Paragraph();
                paragraphDate.Inlines.Add(new Run { Text = DateTimeToFormatConverter.ConvertDayGrouping(Utils.UnixTimestampToDateTime(dateAction.Date)) });
                return paragraphDate;
            }

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = "Empty" });
            return paragraph;
        }
        #endregion

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

        private static string ReplaceWithLink(string source, string param, TLObject obj, ref List<TLMessageEntityBase> entities)
        {
            return ServiceHelper.ReplaceWithLink(source, param, obj, ref entities);
        }
    }
}
