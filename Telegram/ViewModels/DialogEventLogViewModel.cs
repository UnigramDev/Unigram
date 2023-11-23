//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Services.Factories;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public class DialogEventLogViewModel : DialogViewModel
    {
        public DialogEventLogViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IPlaybackService playbackService, IVoipService voipService, IVoipGroupService voipGroupService, INetworkService networkService, IStorageService storageService, ITranslateService translateService, IMessageFactory messageFactory)
            : base(clientService, settingsService, aggregator, locationService, pushService, playbackService, voipService, voipGroupService, networkService, storageService, translateService, messageFactory)
        {
        }

        public override DialogType Type => DialogType.EventLog;

        private long _minEventId = long.MaxValue;

        private ChatEventLogFilters _filters = new ChatEventLogFilters(true, true, true, true, true, true, true, true, true, true, true, true, true);
        public ChatEventLogFilters Filters
        {
            get => _filters;
            set => Set(ref _filters, value);
        }

        private IList<long> _userIds = Array.Empty<long>();
        public IList<long> UserIds
        {
            get => _userIds;
            set => Set(ref _userIds, value);
        }

        public override string Subtitle
        {
            get
            {
                if (_filters.ForumChanges
                    && _filters.VideoChatChanges
                    && _filters.InviteLinkChanges
                    && _filters.SettingChanges
                    && _filters.InfoChanges
                    && _filters.MemberRestrictions
                    && _filters.MemberPromotions
                    && _filters.MemberInvites
                    && _filters.MemberLeaves
                    && _filters.MemberJoins
                    && _filters.MessagePins
                    && _filters.MessageDeletions
                    && _filters.MessageEdits
                    && _userIds.Empty())
                {
                    return Strings.EventLogAllEvents;
                }

                return Strings.EventLogSelectedEvents;
            }
        }

        protected override async void FilterExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var supergroup = ClientService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            var dialog = new SupergroupEventLogFiltersPopup();

            var confirm = await dialog.ShowAsync(ClientService, supergroup.Id, _filters, _userIds);
            if (confirm == ContentDialogResult.Primary)
            {
                Filters = dialog.Filters;
                UserIds = dialog.UserIds;

                RaisePropertyChanged(nameof(Subtitle));

                await LoadEventLogSliceAsync();
            }
        }

        public async void Help()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            await ShowPopupAsync(chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel ? Strings.EventLogInfoDetailChannel : Strings.EventLogInfoDetail, Strings.EventLogInfoTitle, Strings.OK);
        }

        public override async Task LoadEventLogSliceAsync(string query = "")
        {
            NotifyMessageSliceLoaded();

            using (await _loadMoreLock.WaitAsync())
            {
                var chat = _chat;
                if (chat == null)
                {
                    return;
                }

                if (_loadingSlice)
                {
                    return;
                }

                _loadingSlice = true;
                IsLastSliceLoaded = null;
                IsFirstSliceLoaded = null;
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadScheduledSliceAsync");

                var response = await ClientService.SendAsync(new GetChatEventLog(chat.Id, query, 0, 50, _filters, _userIds));
                if (response is ChatEvents events)
                {
                    _groupedMessages.Clear();

                    if (events.Events.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logger.Debug("Setting scroll mode to KeepLastItemInView");
                    }

                    var replied = ProcessEvents(events);
                    ProcessMessages(chat, replied);

                    Items.RawReplaceWith(replied);

                    IsLastSliceLoaded = false;
                    IsFirstSliceLoaded = true;
                }

                _loadingSlice = false;
                IsLoading = false;
            }

            var already = Items.LastOrDefault();
            if (already != null)
            {
                var field = HistoryField;
                if (field != null)
                {
                    await field.ScrollToItem(already, VerticalAlignment.Bottom, null, int.MaxValue, ScrollIntoViewAlignment.Leading, true);
                }
            }
        }

        public override async Task LoadNextSliceAsync()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var chat = _chat;
                if (chat == null)
                {
                    return;
                }

                if (_loadingSlice || Items.Count < 1 || IsLastSliceLoaded == true)
                {
                    return;
                }

                _loadingSlice = true;
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadNextSliceAsync");
                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadNextSliceAsync: Begin request");

                var response = await ClientService.SendAsync(new GetChatEventLog(chat.Id, string.Empty, _minEventId, 50, _filters, _userIds));
                if (response is ChatEvents events)
                {
                    if (events.Events.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logger.Debug("Setting scroll mode to KeepLastItemInView");
                    }

                    var replied = ProcessEvents(events);
                    ProcessMessages(chat, replied);

                    Items.RawInsertRange(0, replied, false, out bool empty);
                    IsLastSliceLoaded = empty;

                    if (empty)
                    {
                        await AddHeaderAsync();
                    }
                }

                _loadingSlice = false;
                IsLoading = false;
            }
        }

        private Message CreateMessage(long chatId, bool isChannel, ChatEvent chatEvent, bool child = false)
        {
            MessageSender sender = chatEvent.MemberId;

            if (child)
            {
                if (chatEvent.Action is ChatEventMessageDeleted messageDeleted)
                {
                    sender = messageDeleted.Message.SenderId;
                }
                else if (chatEvent.Action is ChatEventMessageEdited messageEdited)
                {
                    sender = messageEdited.NewMessage.SenderId;
                }
                else if (chatEvent.Action is ChatEventMessagePinned messagePinned)
                {
                    sender = messagePinned.Message.SenderId;
                }
                else if (chatEvent.Action is ChatEventPollStopped pollStopped)
                {
                    sender = pollStopped.Message.SenderId;
                }
            }

            return new Message(chatEvent.Id, sender, chatId, null, null, false, false, false, false, false, true, false, false, false, false, false, false, false, false, false, isChannel, false, false, chatEvent.Date, 0, null, null, null, null, null, 0, null, 0, 0, 0, string.Empty, 0, string.Empty, null, null);
        }

        private MessageViewModel GetMessage(long chatId, bool isChannel, ChatEvent chatEvent, bool child = false)
        {
            var message = CreateMessage(CreateMessage(chatId, isChannel, chatEvent, child));
            message.Event = chatEvent;
            message.IsFirst = true;
            message.IsLast = true;

            return message;
        }

        private IList<MessageViewModel> ProcessEvents(ChatEvents events)
        {
            var result = new MessageCollection(Items.Ids, Array.Empty<MessageViewModel>());
            var channel = _chat.Type is ChatTypeSupergroup super && super.IsChannel;

            foreach (var item in events.Events)
            {
                _minEventId = Math.Min(_minEventId, item.Id);

                MessageViewModel message = null;
                switch (item.Action)
                {
                    case ChatEventMemberInvited memberInvited:
                        message = GetMessage(_chat.Id, channel, item);
                        message.Content = new MessageChatAddMembers(new[] { memberInvited.UserId });
                        break;
                    case ChatEventSlowModeDelayChanged:
                    case ChatEventPermissionsChanged:
                    case ChatEventMemberRestricted:
                    case ChatEventMemberPromoted:
                        message = GetMessage(_chat.Id, channel, item);
                        //message.Content = new MessageChatEvent(item, true);
                        message.Content = GetMessageContent(item, channel);
                        break;
                    case ChatEventAvailableReactionsChanged:
                    case ChatEventHasProtectedContentToggled:
                    case ChatEventSignMessagesToggled:
                    case ChatEventStickerSetChanged:
                    case ChatEventInvitesToggled:
                    case ChatEventIsAllHistoryAvailableToggled:
                    case ChatEventMemberJoinedByInviteLink:
                    case ChatEventMessageUnpinned:
                    case ChatEventMessageAutoDeleteTimeChanged:
                    case ChatEventLinkedChatChanged:
                    case ChatEventLocationChanged:
                    case ChatEventVideoChatCreated:
                    case ChatEventVideoChatEnded:
                    case ChatEventVideoChatMuteNewParticipantsToggled:
                    case ChatEventVideoChatParticipantIsMutedToggled:
                    case ChatEventVideoChatParticipantVolumeLevelChanged:
                    case ChatEventInviteLinkDeleted:
                    case ChatEventInviteLinkEdited:
                    case ChatEventInviteLinkRevoked:
                    case ChatEventForumTopicCreated:
                    case ChatEventForumTopicDeleted:
                    case ChatEventForumTopicEdited:
                    case ChatEventForumTopicPinned:
                    case ChatEventForumTopicToggleIsClosed:
                    case ChatEventAccentColorChanged:
                    case ChatEventBackgroundCustomEmojiChanged:
                        message = GetMessage(_chat.Id, channel, item);
                        message.Content = new MessageChatEvent(item);
                        break;
                    case ChatEventMemberLeft:
                        if (item.MemberId is MessageSenderUser leftUser)
                        {
                            message = GetMessage(_chat.Id, channel, item);
                            message.Content = new MessageChatDeleteMember(leftUser.UserId);
                        }
                        break;
                    case ChatEventDescriptionChanged:
                    case ChatEventUsernameChanged:
                    case ChatEventMessageDeleted:
                    case ChatEventMessageEdited:
                    case ChatEventMessagePinned:
                    case ChatEventPollStopped:
                        message = GetMessage(_chat.Id, channel, item, true);
                        //message.Content = new MessageChatEvent(item, true);
                        message.Content = GetMessageContent(item, channel);
                        result.Insert(0, message);
                        message = GetMessage(_chat.Id, channel, item);
                        message.Content = new MessageChatEvent(item);
                        break;
                    case ChatEventPhotoChanged photoChanged:
                        message = GetMessage(_chat.Id, channel, item);
                        if (photoChanged.NewPhoto == null)
                        {
                            message.Content = new MessageChatDeletePhoto();
                            break;
                        }

                        message.Content = new MessageChatChangePhoto(photoChanged.NewPhoto);
                        break;
                    case ChatEventMemberJoined:
                        if (item.MemberId is MessageSenderUser joinedUser)
                        {
                            message = GetMessage(_chat.Id, channel, item);
                            message.Content = new MessageChatAddMembers(new long[] { joinedUser.UserId });
                        }
                        break;
                    case ChatEventTitleChanged titleChanged:
                        message = GetMessage(_chat.Id, channel, item);
                        message.Content = new MessageChatChangeTitle(titleChanged.NewTitle);
                        break;
                }

                if (message != null)
                {
                    result.Insert(0, message);
                }
            }

            //if (response.Result.Events.Count < 50)
            //{
            //    _hasMore = false;
            //}

            result.Reverse();
            return result;
        }

        private MessageContent GetMessageContent(ChatEvent item, bool channel)
        {
            if (item.Action is ChatEventDescriptionChanged descriptionChanged)
            {
                var text = new FormattedText(descriptionChanged.NewDescription, Array.Empty<TextEntity>());
                var webPage = string.IsNullOrEmpty(descriptionChanged.OldDescription) ? null : new WebPage { SiteName = Strings.EventLogPreviousGroupDescription, Description = new FormattedText { Text = descriptionChanged.OldDescription } };

                return new MessageText(text, webPage, null);
            }
            else if (item.Action is ChatEventUsernameChanged usernameChanged)
            {
                var link = string.IsNullOrEmpty(usernameChanged.NewUsername) ? string.Empty : MeUrlPrefixConverter.Convert(ClientService, usernameChanged.NewUsername);

                var text = new FormattedText(link, new[] { new TextEntity(0, link.Length, new TextEntityTypeUrl()) });
                var webPage = string.IsNullOrEmpty(usernameChanged.OldUsername) ? null : new WebPage { SiteName = Strings.EventLogPreviousLink, Description = new FormattedText { Text = MeUrlPrefixConverter.Convert(ClientService, usernameChanged.OldUsername) } };

                return new MessageText(text, webPage, null);
            }
            else if (item.Action is ChatEventPermissionsChanged permissionChanged)
            {
                var entities = new List<TextEntity>();

                ChatPermissions o = permissionChanged.OldPermissions;
                ChatPermissions n = permissionChanged.NewPermissions;

                o ??= new ChatPermissions();
                n ??= new ChatPermissions();

                var rights = new StringBuilder(Strings.EventLogDefaultPermissions);
                var added = false;

                void AppendChange(bool value, string label)
                {
                    if (!added)
                    {
                        rights.Append('\n');
                        added = true;
                    }

                    rights.Append('\n').Append(value ? '+' : '-').Append(' ');
                    rights.Append(label);
                }

                //if (o.IsViewMessages != n.IsViewMessages)
                //{
                //    AppendChange(n.IsViewMessages, Strings.EventLogRestrictedReadMessages);
                //}
                if (o.CanSendBasicMessages != n.CanSendBasicMessages)
                {
                    AppendChange(n.CanSendBasicMessages, Strings.EventLogRestrictedSendMessages);
                }
                if (o.CanSendOtherMessages != n.CanSendOtherMessages)
                {
                    AppendChange(n.CanSendOtherMessages, Strings.EventLogRestrictedSendStickers);
                }
                if (o.CanSendPhotos != n.CanSendPhotos)
                {
                    AppendChange(n.CanSendPhotos, Strings.UserRestrictionsSendPhotos);
                }
                if (o.CanSendVideos != n.CanSendVideos)
                {
                    AppendChange(n.CanSendVideos, Strings.UserRestrictionsSendVideos);
                }
                if (o.CanSendOtherMessages != n.CanSendOtherMessages)
                {
                    AppendChange(n.CanSendOtherMessages, Strings.UserRestrictionsSendStickers);
                }
                if (o.CanSendAudios != n.CanSendAudios)
                {
                    AppendChange(n.CanSendAudios, Strings.UserRestrictionsSendMusic);
                }
                if (o.CanSendDocuments != n.CanSendDocuments)
                {
                    AppendChange(n.CanSendDocuments, Strings.UserRestrictionsSendFiles);
                }
                if (o.CanSendVoiceNotes != n.CanSendVoiceNotes)
                {
                    AppendChange(n.CanSendVoiceNotes, Strings.UserRestrictionsSendVoices);
                }
                if (o.CanSendVideoNotes != n.CanSendVideoNotes)
                {
                    AppendChange(n.CanSendVideoNotes, Strings.UserRestrictionsSendRound);
                }
                if (o.CanSendPolls != n.CanSendPolls)
                {
                    AppendChange(n.CanSendPolls, Strings.EventLogRestrictedSendPolls);
                }
                if (o.CanAddWebPagePreviews != n.CanAddWebPagePreviews)
                {
                    AppendChange(n.CanAddWebPagePreviews, Strings.EventLogRestrictedSendEmbed);
                }
                if (o.CanChangeInfo != n.CanChangeInfo)
                {
                    AppendChange(n.CanChangeInfo, Strings.EventLogRestrictedChangeInfo);
                }
                if (o.CanInviteUsers != n.CanInviteUsers)
                {
                    AppendChange(n.CanInviteUsers, Strings.EventLogRestrictedSendEmbed);
                }
                if (o.CanPinMessages != n.CanPinMessages)
                {
                    AppendChange(n.CanPinMessages, Strings.EventLogRestrictedPinMessages);
                }

                string text = rights.ToString();

                return new MessageText(new FormattedText(text, entities), null, null);
            }
            else if (item.Action is ChatEventMemberRestricted memberRestricted)
            {
                string text;

                var whoUser = ClientService.GetMessageSender(memberRestricted.MemberId);
                var entities = new List<TextEntity>();

                if (memberRestricted.NewStatus is ChatMemberStatusBanned)
                {
                    text = string.Format(Strings.EventLogChannelRestricted, GetUserName(whoUser, entities, Strings.EventLogChannelRestricted.IndexOf("{0}")));
                }
                else if (memberRestricted.NewStatus is ChatMemberStatusMember && memberRestricted.OldStatus is ChatMemberStatusBanned)
                {
                    text = string.Format(Strings.EventLogChannelUnrestricted, GetUserName(whoUser, entities, Strings.EventLogChannelUnrestricted.IndexOf("{0}")));
                }
                else
                {
                    ChatMemberStatusRestricted o = null;
                    ChatMemberStatusRestricted n = null;

                    if (memberRestricted.OldStatus is ChatMemberStatusRestricted oldRestricted)
                    {
                        o = oldRestricted;
                    }
                    else if (memberRestricted.OldStatus is ChatMemberStatusBanned oldBanned)
                    {
                        o = new ChatMemberStatusRestricted(false, oldBanned.BannedUntilDate, new ChatPermissions(false, false, false, false, false, false, false, false, false, false, false, false, false, false));
                    }
                    else if (memberRestricted.OldStatus is ChatMemberStatusMember)
                    {
                        o = new ChatMemberStatusRestricted(true, 0, new ChatPermissions(true, true, true, true, true, true, true, true, true, true, true, true, true, true));
                    }

                    if (memberRestricted.NewStatus is ChatMemberStatusRestricted newRestricted)
                    {
                        n = newRestricted;
                    }
                    else if (memberRestricted.NewStatus is ChatMemberStatusBanned newBanned)
                    {
                        n = new ChatMemberStatusRestricted(false, newBanned.BannedUntilDate, new ChatPermissions(false, false, false, false, false, false, false, false, false, false, false, false, false, false));
                    }
                    else if (memberRestricted.NewStatus is ChatMemberStatusMember)
                    {
                        n = new ChatMemberStatusRestricted(true, 0, new ChatPermissions(true, true, true, true, true, true, true, true, true, true, true, true, true, true));
                    }

                    if (!channel && (n != null && o != null /*&& n.RestrictedUntilDate != o.RestrictedUntilDate*/))
                    {
                        StringBuilder rights;
                        string bannedDuration;
                        if (n != null && !n.IsForever())
                        {
                            bannedDuration = "";
                            int duration = n.RestrictedUntilDate - item.Date;
                            int days = duration / 60 / 60 / 24;
                            duration -= days * 60 * 60 * 24;
                            int hours = duration / 60 / 60;
                            duration -= hours * 60 * 60;
                            int minutes = duration / 60;
                            int count = 0;
                            for (int a = 0; a < 3; a++)
                            {
                                string addStr = null;
                                if (a == 0)
                                {
                                    if (days != 0)
                                    {
                                        addStr = Locale.Declension(Strings.R.Days, days);
                                        count++;
                                    }
                                }
                                else if (a == 1)
                                {
                                    if (hours != 0)
                                    {
                                        addStr = Locale.Declension(Strings.R.Hours, hours);
                                        count++;
                                    }
                                }
                                else
                                {
                                    if (minutes != 0)
                                    {
                                        addStr = Locale.Declension(Strings.R.Minutes, minutes);
                                        count++;
                                    }
                                }
                                if (addStr != null)
                                {
                                    if (bannedDuration.Length > 0)
                                    {
                                        bannedDuration += ", ";
                                    }
                                    bannedDuration += addStr;
                                }
                                if (count == 2)
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            bannedDuration = Strings.UserRestrictionsUntilForever;
                        }

                        var str = Strings.EventLogRestrictedUntil;
                        rights = new StringBuilder(string.Format(str, GetUserName(whoUser, entities, str.IndexOf("{0}")), bannedDuration));
                        var added = false;
                        o ??= new ChatMemberStatusRestricted(true, 0, new ChatPermissions(true, true, true, true, true, true, true, true, true, true, true, true, true, true));
                        n ??= new ChatMemberStatusRestricted(true, 0, new ChatPermissions(true, true, true, true, true, true, true, true, true, true, true, true, true, true));

                        void AppendChange(bool value, string label)
                        {
                            if (!added)
                            {
                                rights.Append('\n');
                                added = true;
                            }

                            rights.Append('\n').Append(value ? '+' : '-').Append(' ');
                            rights.Append(label);
                        }

                        //if (o.IsViewMessages != n.IsViewMessages)
                        //{
                        //    AppendChange(n.IsViewMessages, Strings.EventLogRestrictedReadMessages);
                        //}
                        if (o.Permissions.CanSendBasicMessages != n.Permissions.CanSendBasicMessages)
                        {
                            AppendChange(n.Permissions.CanSendBasicMessages, Strings.EventLogRestrictedSendMessages);
                        }
                        if (o.Permissions.CanSendOtherMessages != n.Permissions.CanSendOtherMessages)
                        {
                            AppendChange(n.Permissions.CanSendOtherMessages, Strings.EventLogRestrictedSendStickers);
                        }
                        if (o.Permissions.CanSendPhotos != n.Permissions.CanSendPhotos)
                        {
                            AppendChange(n.Permissions.CanSendPhotos, Strings.UserRestrictionsSendPhotos);
                        }
                        if (o.Permissions.CanSendVideos != n.Permissions.CanSendVideos)
                        {
                            AppendChange(n.Permissions.CanSendVideos, Strings.UserRestrictionsSendVideos);
                        }
                        if (o.Permissions.CanSendOtherMessages != n.Permissions.CanSendOtherMessages)
                        {
                            AppendChange(n.Permissions.CanSendOtherMessages, Strings.UserRestrictionsSendStickers);
                        }
                        if (o.Permissions.CanSendAudios != n.Permissions.CanSendAudios)
                        {
                            AppendChange(n.Permissions.CanSendAudios, Strings.UserRestrictionsSendMusic);
                        }
                        if (o.Permissions.CanSendDocuments != n.Permissions.CanSendDocuments)
                        {
                            AppendChange(n.Permissions.CanSendDocuments, Strings.UserRestrictionsSendFiles);
                        }
                        if (o.Permissions.CanSendVoiceNotes != n.Permissions.CanSendVoiceNotes)
                        {
                            AppendChange(n.Permissions.CanSendVoiceNotes, Strings.UserRestrictionsSendVoices);
                        }
                        if (o.Permissions.CanSendVideoNotes != n.Permissions.CanSendVideoNotes)
                        {
                            AppendChange(n.Permissions.CanSendVideoNotes, Strings.UserRestrictionsSendRound);
                        }
                        if (o.Permissions.CanSendPolls != n.Permissions.CanSendPolls)
                        {
                            AppendChange(n.Permissions.CanSendPolls, Strings.EventLogRestrictedSendPolls);
                        }
                        if (o.Permissions.CanAddWebPagePreviews != n.Permissions.CanAddWebPagePreviews)
                        {
                            AppendChange(n.Permissions.CanAddWebPagePreviews, Strings.EventLogRestrictedSendEmbed);
                        }
                        if (o.Permissions.CanChangeInfo != n.Permissions.CanChangeInfo)
                        {
                            AppendChange(n.Permissions.CanChangeInfo, Strings.EventLogRestrictedChangeInfo);
                        }
                        if (o.Permissions.CanInviteUsers != n.Permissions.CanInviteUsers)
                        {
                            AppendChange(n.Permissions.CanInviteUsers, Strings.EventLogRestrictedSendEmbed);
                        }
                        if (o.Permissions.CanPinMessages != n.Permissions.CanPinMessages)
                        {
                            AppendChange(n.Permissions.CanPinMessages, Strings.EventLogRestrictedPinMessages);
                        }

                        text = rights.ToString();
                    }
                    else
                    {
                        string str;
                        if (o == null || memberRestricted.NewStatus is ChatMemberStatusBanned)
                        {
                            str = Strings.EventLogChannelRestricted;
                        }
                        else
                        {
                            str = Strings.EventLogChannelUnrestricted;
                        }

                        text = string.Format(str, GetUserName(whoUser, entities, str.IndexOf("{0}")));
                    }
                }

                return new MessageText(new FormattedText(text, entities), null, null);
            }
            else if (item.Action is ChatEventMemberPromoted memberPromoted)
            {
                var entities = new List<TextEntity>();

                var whoUser = ClientService.GetUser(memberPromoted.UserId);
                var str = memberPromoted.NewStatus is ChatMemberStatusCreator
                    ? Strings.EventLogChangedOwnership
                    : Strings.EventLogPromoted;
                var userName = GetUserName(whoUser, entities, str.IndexOf("{0}"));
                var builder = new StringBuilder(string.Format(str, userName));
                var added = false;

                if (memberPromoted.NewStatus is ChatMemberStatusCreator)
                {
                    return new MessageText(new FormattedText(builder.ToString(), entities), null, null);
                }

                ChatAdministratorRights o = null;
                ChatAdministratorRights n = null;
                string oldTitle = null;
                string newTitle = null;

                if (memberPromoted.OldStatus is ChatMemberStatusAdministrator oldAdmin)
                {
                    o = oldAdmin.Rights;
                    oldTitle = oldAdmin.CustomTitle;
                }
                if (memberPromoted.NewStatus is ChatMemberStatusAdministrator newAdmin)
                {
                    n = newAdmin.Rights;
                    newTitle = newAdmin.CustomTitle;
                }

                o ??= new ChatAdministratorRights();
                n ??= new ChatAdministratorRights();

                if (!string.Equals(oldTitle, newTitle))
                {
                    if (!added)
                    {
                        builder.Append('\n');
                        added = true;
                    }

                    if (string.IsNullOrEmpty(newTitle))
                    {
                        builder.Append('\n').Append('-').Append(' ');
                        builder.Append(Strings.EventLogPromotedRemovedTitle);
                    }
                    else
                    {
                        builder.Append('\n').Append('+').Append(' ');
                        builder.AppendFormat(Strings.EventLogPromotedTitle, newTitle);
                    }
                }

                void AppendChange(bool value, string label)
                {
                    if (!added)
                    {
                        builder.Append('\n');
                        added = true;
                    }

                    builder.Append('\n').Append(value ? '+' : '-').Append(' ');
                    builder.Append(label);
                }

                if (o.CanChangeInfo != n.CanChangeInfo)
                {
                    AppendChange(n.CanChangeInfo, channel ? Strings.EventLogPromotedChangeChannelInfo : Strings.EventLogPromotedChangeGroupInfo);
                }

                if (channel)
                {
                    if (o.CanPostMessages != n.CanPostMessages)
                    {
                        AppendChange(n.CanPostMessages, Strings.EventLogPromotedPostMessages);
                    }
                    if (o.CanEditMessages != n.CanEditMessages)
                    {
                        AppendChange(n.CanEditMessages, Strings.EventLogPromotedEditMessages);
                    }
                }
                if (o.CanDeleteMessages != n.CanDeleteMessages)
                {
                    AppendChange(n.CanDeleteMessages, Strings.EventLogPromotedDeleteMessages);
                }
                if (channel)
                {
                    if (o.CanPostStories != n.CanPostStories)
                    {
                        AppendChange(n.CanPostStories, Strings.EventLogPromotedPostStories);
                    }
                    if (o.CanEditStories != n.CanEditStories)
                    {
                        AppendChange(n.CanEditStories, Strings.EventLogPromotedEditStories);
                    }
                    if (o.CanDeleteMessages != n.CanDeleteMessages)
                    {
                        AppendChange(n.CanDeleteStories, Strings.EventLogPromotedDeleteStories);
                    }
                }
                if (o.CanPromoteMembers != n.CanPromoteMembers)
                {
                    AppendChange(n.CanPromoteMembers, Strings.EventLogPromotedAddAdmins);
                }
                if (!channel)
                {
                    if (o.IsAnonymous != n.IsAnonymous)
                    {
                        AppendChange(n.IsAnonymous, Strings.EventLogPromotedSendAnonymously);
                    }
                    if (o.CanRestrictMembers != n.CanRestrictMembers)
                    {
                        AppendChange(n.CanRestrictMembers, Strings.EventLogPromotedBanUsers);
                    }
                }
                if (o.CanInviteUsers != n.CanInviteUsers)
                {
                    AppendChange(n.CanInviteUsers, Strings.EventLogPromotedAddUsers);
                }
                if (!channel)
                {
                    if (o.CanPinMessages != n.CanPinMessages)
                    {
                        AppendChange(n.CanPinMessages, Strings.EventLogPromotedPinMessages);
                    }
                    if (o.CanManageVideoChats != n.CanManageVideoChats)
                    {
                        AppendChange(n.CanManageVideoChats, Strings.EventLogPromotedManageCall);
                    }
                }

                return new MessageText(new FormattedText(builder.ToString(), entities), null, null);
            }
            else if (item.Action is ChatEventMessageDeleted messageDeleted)
            {
                return messageDeleted.Message.Content;
            }
            else if (item.Action is ChatEventMessageEdited messageEdited)
            {
                if (messageEdited.NewMessage.Content is MessageText editedText && messageEdited.OldMessage.Content is MessageText oldText)
                {
                    editedText.WebPage = new WebPage
                    {
                        SiteName = Strings.EventLogOriginalMessages,
                        Description = oldText.Text
                    };
                }

                return messageEdited.NewMessage.Content;
            }
            else if (item.Action is ChatEventMessagePinned messagePinned)
            {
                return messagePinned.Message.Content;
            }
            else if (item.Action is ChatEventPollStopped pollStopped)
            {
                return pollStopped.Message.Content;
            }

            return new MessageChatEvent(item);
        }

        private string GetUserName(BaseObject sender, List<TextEntity> entities, int offset)
        {
            if (sender is User user)
            {
                var name = user.FullName();

                if (offset >= 0)
                {
                    entities.Add(new TextEntity(offset, name.Length, new TextEntityTypeMentionName(user.Id)));
                }

                var username = user.Usernames?.ActiveUsernames[0];
                if (string.IsNullOrEmpty(username))
                {
                    return name;
                }

                if (offset >= 0)
                {
                    entities.Add(new TextEntity(name.Length + offset + 2, username.Length + 1, new TextEntityTypeMentionName(user.Id)));
                }

                return string.Format("{0} (@{1})", name, username);
            }
            else if (sender is Chat chat)
            {
                var name = ClientService.GetTitle(chat);

                // Make it clickable

                return name;
            }

            return string.Empty;
        }
    }
}
