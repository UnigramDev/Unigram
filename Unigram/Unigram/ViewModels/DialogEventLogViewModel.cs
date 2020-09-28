using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Factories;
using Unigram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public class DialogEventLogViewModel : DialogViewModel
    {
        public DialogEventLogViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService, INotificationsService pushService, IPlaybackService playbackService, IVoipService voipService, INetworkService networkService, IMessageFactory messageFactory)
            : base(protoService, cacheService, settingsService, aggregator, locationService, pushService, playbackService, voipService, networkService, messageFactory)
        {
            HelpCommand = new RelayCommand(HelpExecute);
        }

        public override DialogType Type => DialogType.EventLog;

        private long _minEventId = long.MaxValue;

        private ChatEventLogFilters _filters = new ChatEventLogFilters(true, true, true, true, true, true, true, true, true, true);
        public ChatEventLogFilters Filters
        {
            get => _filters;
            set => Set(ref _filters, value);
        }

        private IList<int> _userIds = new int[0];
        public IList<int> UserIds
        {
            get => _userIds;
            set => Set(ref _userIds, value);
        }

        public override string Subtitle
        {
            get
            {
                if (_filters.InfoChanges &&
                    _filters.MemberInvites &&
                    _filters.MemberJoins &&
                    _filters.MemberLeaves &&
                    _filters.MemberPromotions &&
                    _filters.MemberRestrictions &&
                    _filters.MessageDeletions &&
                    _filters.MessageEdits &&
                    _filters.MessagePins &&
                    _filters.SettingChanges &&
                    _userIds.IsEmpty())
                {
                    return Strings.Resources.EventLogAllEvents;
                }

                return Strings.Resources.EventLogSelectedEvents;
            }
        }

        protected override async void FilterExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var supergroup = CacheService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            var dialog = new SupergroupEventLogFiltersPopup();

            var confirm = await dialog.ShowAsync(ProtoService, supergroup.Id, _filters, _userIds);
            if (confirm == ContentDialogResult.Primary)
            {
                Filters = dialog.Filters;
                UserIds = dialog.UserIds;

                RaisePropertyChanged(() => Subtitle);

                await LoadEventLogSliceAsync();
            }
        }

        public RelayCommand HelpCommand { get; }
        private async void HelpExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            await MessagePopup.ShowAsync(chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel ? Strings.Resources.EventLogInfoDetailChannel : Strings.Resources.EventLogInfoDetail, Strings.Resources.EventLogInfoTitle, Strings.Resources.OK);
        }

        public override async Task LoadEventLogSliceAsync(string query = "")
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var chat = _chat;
                if (chat == null)
                {
                    return;
                }

                if (_isLoadingNextSlice || _isLoadingPreviousSlice)
                {
                    return;
                }

                _isLoadingNextSlice = true;
                _isLoadingPreviousSlice = true;
                IsLastSliceLoaded = null;
                IsFirstSliceLoaded = null;
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadScheduledSliceAsync");

                var response = await ProtoService.SendAsync(new GetChatEventLog(chat.Id, query, 0, 50, _filters, _userIds));
                if (response is ChatEvents events)
                {
                    _groupedMessages.Clear();

                    if (events.Events.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logs.Logger.Debug(Logs.Target.Chat, "Setting scroll mode to KeepLastItemInView");
                    }

                    var replied = ProcessEvents(events);
                    await ProcessMessagesAsync(chat, replied);

                    var target = replied.FirstOrDefault();
                    if (target != null)
                    {
                        replied.Insert(0, _messageFactory.Create(this, new Message(0, target.SenderUserId, target.SenderChatId, target.ChatId, null, target.SchedulingState, target.IsOutgoing, false, false, true, false, false, false, target.IsChannelPost, false, target.Date, 0, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null)));
                    }

                    Items.ReplaceWith(replied);

                    IsLastSliceLoaded = false;
                    IsFirstSliceLoaded = true;
                }

                _isLoadingNextSlice = false;
                _isLoadingPreviousSlice = false;
                IsLoading = false;
            }

            var already = Items.LastOrDefault();
            if (already != null)
            {
                var field = ListField;
                if (field != null)
                {
                    await field.ScrollToItem(already, VerticalAlignment.Bottom, false, int.MaxValue, ScrollIntoViewAlignment.Leading, true);
                }
            }
        }

        public async override Task LoadNextSliceAsync(bool force = false, bool init = false)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                try
                {
                    // We don't want to flood with requests when the chat gets initialized
                    if (init && ListField?.ScrollingHost?.ScrollableHeight >= 200)
                    {
                        return;
                    }
                }
                catch { }

                var chat = _migratedChat ?? _chat;
                if (chat == null)
                {
                    return;
                }

                if (_isLoadingNextSlice || _isLoadingPreviousSlice || Items.Count < 1 || IsLastSliceLoaded == true)
                {
                    return;
                }

                _isLoadingNextSlice = true;
                IsLoading = true;

                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadNextSliceAsync");
                System.Diagnostics.Debug.WriteLine("DialogViewModel: LoadNextSliceAsync: Begin request");

                var response = await ProtoService.SendAsync(new GetChatEventLog(chat.Id, string.Empty, _minEventId, 50, _filters, _userIds));
                if (response is ChatEvents events)
                {
                    if (events.Events.Count > 0)
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, force);
                        Logs.Logger.Debug(Logs.Target.Chat, "Setting scroll mode to KeepLastItemInView");
                    }

                    var replied = ProcessEvents(events);
                    await ProcessMessagesAsync(chat, replied);

                    foreach (var message in replied)
                    {
                        Items.Insert(0, message);
                    }

                    IsLastSliceLoaded = replied.IsEmpty();

                    if (replied.IsEmpty())
                    {
                        await AddHeaderAsync();
                    }
                }

                _isLoadingNextSlice = false;
                IsLoading = false;
            }
        }

        private Message CreateMessage(long chatId, bool isChannel, ChatEvent chatEvent, bool child = false)
        {
            var userId = chatEvent.UserId;

            if (child)
            {
                if (chatEvent.Action is ChatEventMessageDeleted messageDeleted)
                {
                    userId = messageDeleted.Message.SenderUserId;
                }
                else if (chatEvent.Action is ChatEventMessageEdited messageEdited)
                {
                    userId = messageEdited.NewMessage.SenderUserId;
                }
                else if (chatEvent.Action is ChatEventMessagePinned messagePinned)
                {
                    userId = messagePinned.Message.SenderUserId;
                }
                else if (chatEvent.Action is ChatEventPollStopped pollStopped)
                {
                    userId = pollStopped.Message.SenderUserId;
                }
            }

            return new Message(chatEvent.Id, userId, 0, chatId, null, null, false, false, false, false, false, false, false, isChannel, false, chatEvent.Date, 0, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, null, null);
        }

        private MessageViewModel GetMessage(long chatId, bool isChannel, ChatEvent chatEvent, bool child = false)
        {
            var message = _messageFactory.Create(this, CreateMessage(chatId, isChannel, chatEvent, child));
            message.IsFirst = true;
            message.IsLast = true;

            return message;
        }

        private IList<MessageViewModel> ProcessEvents(ChatEvents events)
        {
            var result = new List<MessageViewModel>();
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
                    case ChatEventSlowModeDelayChanged slowModeDelayChanged:
                    case ChatEventPermissionsChanged permissionsChanged:
                    case ChatEventMemberRestricted memberRestricted:
                    case ChatEventMemberPromoted memberPromoted:
                        message = GetMessage(_chat.Id, channel, item);
                        //message.Content = new MessageChatEvent(item, true);
                        message.Content = GetMessageContent(item, channel);
                        break;
                    case ChatEventSignMessagesToggled signMessagesToggled:
                    case ChatEventStickerSetChanged stickerSetChanged:
                    case ChatEventInvitesToggled invitesToggled:
                    case ChatEventIsAllHistoryAvailableToggled isAllHistoryAvailableToggled:
                    case ChatEventMessageUnpinned messageUnpinned:
                    case ChatEventLinkedChatChanged linkedChatChanged:
                    case ChatEventLocationChanged locationChanged:
                        message = GetMessage(_chat.Id, channel, item);
                        message.Content = new MessageChatEvent(item);
                        break;
                    case ChatEventMemberLeft memberLeft:
                        message = GetMessage(_chat.Id, channel, item);
                        message.Content = new MessageChatDeleteMember(item.UserId);
                        break;
                    case ChatEventDescriptionChanged descriptionChanged:
                    case ChatEventUsernameChanged usernameChanged:
                    case ChatEventMessageDeleted messageDeleted:
                    case ChatEventMessageEdited messageEdited:
                    case ChatEventMessagePinned messagePinned:
                    case ChatEventPollStopped pollStopped:
                        message = GetMessage(_chat.Id, channel, item, true);
                        //message.Content = new MessageChatEvent(item, true);
                        message.Content = GetMessageContent(item, channel);
                        result.Add(message);
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
                    case ChatEventMemberJoined memberJoined:
                        message = GetMessage(_chat.Id, channel, item);
                        message.Content = new MessageChatAddMembers(new int[] { item.UserId });
                        break;
                    case ChatEventTitleChanged titleChanged:
                        message = GetMessage(_chat.Id, channel, item);
                        message.Content = new MessageChatChangeTitle(titleChanged.NewTitle);
                        break;
                }

                if (message != null)
                {
                    result.Add(message);
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
                var text = new FormattedText(descriptionChanged.NewDescription, new TextEntity[0]);
                var webPage = string.IsNullOrEmpty(descriptionChanged.OldDescription) ? null : new WebPage { SiteName = Strings.Resources.EventLogPreviousGroupDescription, Description = new FormattedText { Text = descriptionChanged.OldDescription } };

                return new MessageText(text, webPage);
            }
            else if (item.Action is ChatEventUsernameChanged usernameChanged)
            {
                var link = string.IsNullOrEmpty(usernameChanged.NewUsername) ? string.Empty : MeUrlPrefixConverter.Convert(CacheService, usernameChanged.NewUsername);

                var text = new FormattedText(link, new[] { new TextEntity(0, link.Length, new TextEntityTypeUrl()) });
                var webPage = string.IsNullOrEmpty(usernameChanged.OldUsername) ? null : new WebPage { SiteName = Strings.Resources.EventLogPreviousLink, Description = new FormattedText { Text = MeUrlPrefixConverter.Convert(CacheService, usernameChanged.OldUsername) } };

                return new MessageText(text, webPage);
            }
            else if (item.Action is ChatEventPermissionsChanged permissionChanged)
            {
                var text = string.Empty;
                var entities = new List<TextEntity>();

                ChatPermissions o = permissionChanged.OldPermissions;
                ChatPermissions n = permissionChanged.NewPermissions;

                if (o == null)
                {
                    o = new ChatPermissions();
                }
                if (n == null)
                {
                    n = new ChatPermissions();
                }

                var rights = new StringBuilder(Strings.Resources.EventLogDefaultPermissions);
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
                //    AppendChange(n.IsViewMessages, Strings.Resources.EventLogRestrictedReadMessages);
                //}
                if (o.CanSendMessages != n.CanSendMessages)
                {
                    AppendChange(n.CanSendMessages, Strings.Resources.EventLogRestrictedSendMessages);
                }
                if (o.CanSendOtherMessages != n.CanSendOtherMessages)
                {
                    AppendChange(n.CanSendOtherMessages, Strings.Resources.EventLogRestrictedSendStickers);
                }
                if (o.CanSendMediaMessages != n.CanSendMediaMessages)
                {
                    AppendChange(n.CanSendMediaMessages, Strings.Resources.EventLogRestrictedSendMedia);
                }
                if (o.CanSendPolls != n.CanSendPolls)
                {
                    AppendChange(n.CanSendPolls, Strings.Resources.EventLogRestrictedSendPolls);
                }
                if (o.CanAddWebPagePreviews != n.CanAddWebPagePreviews)
                {
                    AppendChange(n.CanAddWebPagePreviews, Strings.Resources.EventLogRestrictedSendEmbed);
                }
                if (o.CanChangeInfo != n.CanChangeInfo)
                {
                    AppendChange(n.CanChangeInfo, Strings.Resources.EventLogRestrictedChangeInfo);
                }
                if (o.CanInviteUsers != n.CanInviteUsers)
                {
                    AppendChange(n.CanInviteUsers, Strings.Resources.EventLogRestrictedSendEmbed);
                }
                if (o.CanPinMessages != n.CanPinMessages)
                {
                    AppendChange(n.CanPinMessages, Strings.Resources.EventLogRestrictedPinMessages);
                }

                text = rights.ToString();

                return new MessageText(new FormattedText(text, entities), null);
            }
            else if (item.Action is ChatEventMemberRestricted memberRestricted)
            {
                var text = string.Empty;
                var entities = new List<TextEntity>();

                var whoUser = CacheService.GetUser(memberRestricted.UserId);
                ChatMemberStatusRestricted o = null;
                ChatMemberStatusRestricted n = null;

                if (memberRestricted.OldStatus is ChatMemberStatusRestricted oldRestricted)
                {
                    o = oldRestricted;
                }
                else if (memberRestricted.OldStatus is ChatMemberStatusBanned oldBanned)
                {
                    o = new ChatMemberStatusRestricted(false, oldBanned.BannedUntilDate, new ChatPermissions(false, false, false, false, false, false, false, false));
                }
                else if (memberRestricted.OldStatus is ChatMemberStatusMember)
                {
                    o = new ChatMemberStatusRestricted(true, 0, new ChatPermissions(true, true, true, true, true, true, true, true));
                }

                if (memberRestricted.NewStatus is ChatMemberStatusRestricted newRestricted)
                {
                    n = newRestricted;
                }
                else if (memberRestricted.NewStatus is ChatMemberStatusBanned newBanned)
                {
                    n = new ChatMemberStatusRestricted(false, newBanned.BannedUntilDate, new ChatPermissions(false, false, false, false, false, false, false, false));
                }
                else if (memberRestricted.NewStatus is ChatMemberStatusMember)
                {
                    n = new ChatMemberStatusRestricted(true, 0, new ChatPermissions(true, true, true, true, true, true, true, true));
                }

                if (!channel && (n == null || n != null && o != null /*&& n.RestrictedUntilDate != o.RestrictedUntilDate*/))
                {
                    StringBuilder rights;
                    String bannedDuration;
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
                            String addStr = null;
                            if (a == 0)
                            {
                                if (days != 0)
                                {
                                    addStr = Locale.Declension("Days", days);
                                    count++;
                                }
                            }
                            else if (a == 1)
                            {
                                if (hours != 0)
                                {
                                    addStr = Locale.Declension("Hours", hours);
                                    count++;
                                }
                            }
                            else
                            {
                                if (minutes != 0)
                                {
                                    addStr = Locale.Declension("Minutes", minutes);
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
                        bannedDuration = Strings.Resources.UserRestrictionsUntilForever;
                    }

                    var str = Strings.Resources.EventLogRestrictedUntil;
                    rights = new StringBuilder(String.Format(str, GetUserName(whoUser, entities, str.IndexOf("{0}")), bannedDuration));
                    var added = false;
                    if (o == null)
                    {
                        o = new ChatMemberStatusRestricted(true, 0, new ChatPermissions(true, true, true, true, true, true, true, true));
                    }
                    if (n == null)
                    {
                        n = new ChatMemberStatusRestricted(true, 0, new ChatPermissions(true, true, true, true, true, true, true, true));
                    }

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
                    //    AppendChange(n.IsViewMessages, Strings.Resources.EventLogRestrictedReadMessages);
                    //}
                    if (o.Permissions.CanSendMessages != n.Permissions.CanSendMessages)
                    {
                        AppendChange(n.Permissions.CanSendMessages, Strings.Resources.EventLogRestrictedSendMessages);
                    }
                    if (o.Permissions.CanSendOtherMessages != n.Permissions.CanSendOtherMessages)
                    {
                        AppendChange(n.Permissions.CanSendOtherMessages, Strings.Resources.EventLogRestrictedSendStickers);
                    }
                    if (o.Permissions.CanSendMediaMessages != n.Permissions.CanSendMediaMessages)
                    {
                        AppendChange(n.Permissions.CanSendMediaMessages, Strings.Resources.EventLogRestrictedSendMedia);
                    }
                    if (o.Permissions.CanSendPolls != n.Permissions.CanSendPolls)
                    {
                        AppendChange(n.Permissions.CanSendPolls, Strings.Resources.EventLogRestrictedSendPolls);
                    }
                    if (o.Permissions.CanAddWebPagePreviews != n.Permissions.CanAddWebPagePreviews)
                    {
                        AppendChange(n.Permissions.CanAddWebPagePreviews, Strings.Resources.EventLogRestrictedSendEmbed);
                    }
                    if (o.Permissions.CanChangeInfo != n.Permissions.CanChangeInfo)
                    {
                        AppendChange(n.Permissions.CanChangeInfo, Strings.Resources.EventLogRestrictedChangeInfo);
                    }
                    if (o.Permissions.CanInviteUsers != n.Permissions.CanInviteUsers)
                    {
                        AppendChange(n.Permissions.CanInviteUsers, Strings.Resources.EventLogRestrictedSendEmbed);
                    }
                    if (o.Permissions.CanPinMessages != n.Permissions.CanPinMessages)
                    {
                        AppendChange(n.Permissions.CanPinMessages, Strings.Resources.EventLogRestrictedPinMessages);
                    }

                    text = rights.ToString();
                }
                else
                {
                    String str;
                    if (o == null || memberRestricted.NewStatus is ChatMemberStatusBanned)
                    {
                        str = Strings.Resources.EventLogChannelRestricted;
                    }
                    else
                    {
                        str = Strings.Resources.EventLogChannelUnrestricted;
                    }

                    text = String.Format(str, GetUserName(whoUser, entities, str.IndexOf("{0}")));
                }

                return new MessageText(new FormattedText(text, entities), null);
            }
            else if (item.Action is ChatEventMemberPromoted memberPromoted)
            {
                var entities = new List<TextEntity>();

                var whoUser = CacheService.GetUser(memberPromoted.UserId);
                var str = memberPromoted.NewStatus is ChatMemberStatusCreator
                    ? Strings.Resources.EventLogChangedOwnership
                    : Strings.Resources.EventLogPromoted;
                var userName = GetUserName(whoUser, entities, str.IndexOf("{0}"));
                var builder = new StringBuilder(string.Format(str, userName));
                var added = false;

                if (memberPromoted.NewStatus is ChatMemberStatusCreator)
                {
                    return new MessageText(new FormattedText(builder.ToString(), entities), null);
                }

                ChatMemberStatusAdministrator o = null;
                ChatMemberStatusAdministrator n = null;

                if (memberPromoted.OldStatus is ChatMemberStatusAdministrator oldAdmin)
                {
                    o = oldAdmin;
                }
                if (memberPromoted.NewStatus is ChatMemberStatusAdministrator newAdmin)
                {
                    n = newAdmin;
                }

                if (o == null)
                {
                    o = new ChatMemberStatusAdministrator();
                }
                if (n == null)
                {
                    n = new ChatMemberStatusAdministrator();
                }

                if (!string.Equals(o.CustomTitle, n.CustomTitle))
                {
                    if (!added)
                    {
                        builder.Append('\n');
                        added = true;
                    }

                    if (string.IsNullOrEmpty(n.CustomTitle))
                    {
                        builder.Append('\n').Append('-').Append(' ');
                        builder.Append(Strings.Resources.EventLogPromotedRemovedTitle);
                    }
                    else
                    {
                        builder.Append('\n').Append('+').Append(' ');
                        builder.AppendFormat(Strings.Resources.EventLogPromotedTitle, n.CustomTitle);
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
                    AppendChange(n.CanChangeInfo, channel ? Strings.Resources.EventLogPromotedChangeChannelInfo : Strings.Resources.EventLogPromotedChangeGroupInfo);
                }

                if (channel)
                {
                    if (o.CanPostMessages != n.CanPostMessages)
                    {
                        AppendChange(n.CanPostMessages, Strings.Resources.EventLogPromotedPostMessages);
                    }
                    if (o.CanEditMessages != n.CanEditMessages)
                    {
                        AppendChange(n.CanEditMessages, Strings.Resources.EventLogPromotedEditMessages);
                    }
                }
                if (o.CanDeleteMessages != n.CanDeleteMessages)
                {
                    AppendChange(n.CanDeleteMessages, Strings.Resources.EventLogPromotedDeleteMessages);
                }
                if (o.CanPromoteMembers != n.CanPromoteMembers)
                {
                    AppendChange(n.CanPromoteMembers, Strings.Resources.EventLogPromotedAddAdmins);
                }
                if (!channel)
                {
                    if (o.IsAnonymous != n.IsAnonymous)
                    {
                        AppendChange(n.IsAnonymous, Strings.Resources.EventLogPromotedSendAnonymously);
                    }
                    if (o.CanRestrictMembers != n.CanRestrictMembers)
                    {
                        AppendChange(n.CanRestrictMembers, Strings.Resources.EventLogPromotedBanUsers);
                    }
                }
                if (o.CanInviteUsers != n.CanInviteUsers)
                {
                    AppendChange(n.CanInviteUsers, Strings.Resources.EventLogPromotedAddUsers);
                }
                if (!channel)
                {
                    if (o.CanPinMessages != n.CanPinMessages)
                    {
                        AppendChange(n.CanPinMessages, Strings.Resources.EventLogPromotedPinMessages);
                    }
                }

                return new MessageText(new FormattedText(builder.ToString(), entities), null);
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
                        SiteName = Strings.Resources.EventLogOriginalMessages,
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

        private string GetUserName(User user, List<TextEntity> entities, int offset)
        {
            string name;
            if (user == null)
            {
                name = string.Empty;
            }
            else
            {
                name = user.GetFullName();
            }

            if (offset >= 0)
            {
                entities.Add(new TextEntity(offset, name.Length, new TextEntityTypeMentionName(user.Id)));
            }

            if (string.IsNullOrEmpty(user.Username))
            {
                return name;
            }

            if (offset >= 0)
            {
                entities.Add(new TextEntity((name.Length + offset) + 2, user.Username.Length + 1, new TextEntityTypeMentionName(user.Id)));
            }

            return string.Format("{0} (@{1})", name, user.Username);
        }
    }
}
