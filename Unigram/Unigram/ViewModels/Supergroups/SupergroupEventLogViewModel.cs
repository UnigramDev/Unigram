using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Factories;
using Unigram.Strings;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEventLogViewModel : TLViewModelBase, IDelegable<IChatDelegate>, IMessageDelegate
    {
        private readonly IMessageFactory _messageFactory;

        public SupergroupEventLogViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IMessageFactory messageFactory, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _messageFactory = messageFactory;

            FiltersCommand = new RelayCommand(FiltersExecute);
            HelpCommand = new RelayCommand(HelpExecute);
        }

        public IChatDelegate Delegate { get; set; }

        protected Chat _chat;
        public Chat Chat
        {
            get
            {
                return _chat;
            }
            set
            {
                Set(ref _chat, value);
            }
        }

        private ChatEventLogFilters _filters = new ChatEventLogFilters(true, true, true, true, true, true, true, true, true, true);
        public ChatEventLogFilters Filters
        {
            get
            {
                return _filters;
            }
            set
            {
                Set(ref _filters, value);
            }
        }

        private IList<int> _userIds = new int[0];
        public IList<int> UserIds
        {
            get
            {
                return _userIds;
            }
            set
            {
                Set(ref _userIds, value);
            }
        }

        private bool _isEmpty = true;
        public bool IsEmpty
        {
            get
            {
                return _isEmpty && !IsLoading;
            }
            set
            {
                Set(ref _isEmpty, value);
            }
        }

        public override bool IsLoading
        {
            get
            {
                return base.IsLoading;
            }
            set
            {
                base.IsLoading = value;
                RaisePropertyChanged(() => IsEmpty);
            }
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var chatId = (long)parameter;

            Chat = ProtoService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            Delegate?.UpdateChat(chat);

            Items = new ItemsCollection(ProtoService, _messageFactory, this, chat.Id, chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel, _filters, _userIds);
            Items.CollectionChanged += (s, args) => IsEmpty = Items.Count == 0;

            RaisePropertyChanged(() => Items);

            return Task.CompletedTask;
        }

        public RelayCommand FiltersCommand { get; }
        private async void FiltersExecute()
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

            var dialog = new SupergroupEventLogFiltersView();

            var confirm = await dialog.ShowAsync(ProtoService, supergroup.Id, _filters, _userIds);
            if (confirm == ContentDialogResult.Primary)
            {
                Filters = dialog.Filters;
                UserIds = dialog.UserIds;
                Items = new ItemsCollection(ProtoService, _messageFactory, this, chat.Id, supergroup.IsChannel, dialog.Filters, dialog.AreAllAdministratorsSelected ? new int[0] : dialog.UserIds);

                RaisePropertyChanged(() => Items);
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

            await TLMessageDialog.ShowAsync(chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel ? Strings.Resources.EventLogInfoDetailChannel : Strings.Resources.EventLogInfoDetail, Strings.Resources.EventLogInfoTitle, Strings.Resources.OK);
        }

        public ItemsCollection Items { get; protected set; }

        public class ItemsCollection : IncrementalCollection<MessageViewModel>
        {
            private readonly IProtoService _protoService;
            private readonly IMessageFactory _messageFactory;
            private readonly IMessageDelegate _delegate;
            private readonly long _chatId;
            private readonly bool _channel;
            private readonly ChatEventLogFilters _filters;
            private readonly IList<int> _userIds;

            private long _minEventId = long.MaxValue;
            private bool _hasMore;

            public ItemsCollection(IProtoService protoService, IMessageFactory messageFactory, IMessageDelegate delegato, long chatId, bool channel, ChatEventLogFilters filters, IList<int> userIds)
            {
                _protoService = protoService;
                _messageFactory = messageFactory;
                _delegate = delegato;
                _chatId = chatId;
                _channel = channel;
                _filters = filters;
                _userIds = userIds;

                _hasMore = true;
            }

            private Message newMessage(long chatId, bool isChannel, ChatEvent chatEvent)
            {
                return new Message(chatEvent.Id, chatEvent.UserId, chatId, null, null, false, false, false, false, false, isChannel, false, chatEvent.Date, 0, null, 0, 0, 0.0d, 0, string.Empty, 0, 0, string.Empty, null, null);
            }

            private MessageViewModel GetMessage(long chatId, bool isChannel, ChatEvent chatEvent)
            {
                var message = _messageFactory.Create(_delegate, newMessage(chatId, isChannel, chatEvent));
                message.IsFirst = true;
                message.IsLast = true;

                return message;
            }

            public override async Task<IList<MessageViewModel>> LoadDataAsync()
            {
                _hasMore = false;

                var maxId = Count > 0 ? _minEventId : 0;

                var response = await _protoService.SendAsync(new GetChatEventLog(_chatId, string.Empty, 0, 50, _filters, _userIds));
                if (response is ChatEvents events)
                {
                    var result = new List<MessageViewModel>();

                    foreach (var item in events.Events)
                    {
                        _minEventId = Math.Min(_minEventId, item.Id);

                        MessageViewModel message = null;
                        switch (item.Action)
                        {
                            case ChatEventMemberInvited memberInvited:
                                message = GetMessage(_chatId, _channel, item);
                                message.Content = new MessageChatAddMembers(new[] { memberInvited.UserId });
                                break;
                            case ChatEventSlowModeDelayChanged slowModeDelayChanged:
                            case ChatEventPermissionsChanged permissionsChanged:
                            case ChatEventMemberRestricted memberRestricted:
                            case ChatEventMemberPromoted memberPromoted:
                                message = GetMessage(_chatId, _channel, item);
                                //message.Content = new MessageChatEvent(item, true);
                                message.Content = GetMessageContent(item);
                                break;
                            case ChatEventSignMessagesToggled signMessagesToggled:
                            case ChatEventStickerSetChanged stickerSetChanged:
                            case ChatEventInvitesToggled invitesToggled:
                            case ChatEventIsAllHistoryAvailableToggled isAllHistoryAvailableToggled:
                            case ChatEventMessageUnpinned messageUnpinned:
                            case ChatEventLinkedChatChanged linkedChatChanged:
                            case ChatEventLocationChanged locationChanged:
                                message = GetMessage(_chatId, _channel, item);
                                message.Content = new MessageChatEvent(item, false);
                                break;
                            case ChatEventMemberLeft memberLeft:
                                message = GetMessage(_chatId, _channel, item);
                                message.Content = new MessageChatDeleteMember(item.UserId);
                                break;
                            case ChatEventMessageDeleted messageDeleted:
                            case ChatEventMessageEdited messageEdited:
                            case ChatEventDescriptionChanged descriptionChanged:
                            case ChatEventMessagePinned messagePinned:
                            case ChatEventUsernameChanged usernameChanged:
                            case ChatEventPollStopped pollStopped:
                                message = GetMessage(_chatId, _channel, item);
                                //message.Content = new MessageChatEvent(item, true);
                                message.Content = GetMessageContent(item);
                                result.Add(message);
                                message = GetMessage(_chatId, _channel, item);
                                message.Content = new MessageChatEvent(item, false);
                                break;
                            case ChatEventPhotoChanged photoChanged:
                                message = GetMessage(_chatId, _channel, item);
                                if (photoChanged.NewPhoto == null)
                                {
                                    message.Content = new MessageChatDeletePhoto();
                                    break;
                                }

                                message.Content = new MessageChatChangePhoto(photoChanged.NewPhoto);
                                break;
                            case ChatEventMemberJoined memberJoined:
                                message = GetMessage(_chatId, _channel, item);
                                message.Content = new MessageChatAddMembers(new int[] { item.UserId });
                                break;
                            case ChatEventTitleChanged titleChanged:
                                message = GetMessage(_chatId, _channel, item);
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

                return new MessageViewModel[0];
            }

            private MessageContent GetMessageContent(ChatEvent item)
            {
                if (item.Action is ChatEventDescriptionChanged descriptionChanged)
                {
                    var text = new FormattedText(descriptionChanged.NewDescription, new TextEntity[0]);
                    var webPage = string.IsNullOrEmpty(descriptionChanged.OldDescription) ? null : new WebPage { SiteName = Strings.Resources.EventLogPreviousGroupDescription, Description = descriptionChanged.OldDescription };

                    return new MessageText(text, webPage);
                }
                else if (item.Action is ChatEventUsernameChanged usernameChanged)
                {
                    var link = string.IsNullOrEmpty(usernameChanged.NewUsername) ? string.Empty : MeUrlPrefixConverter.Convert(_protoService, usernameChanged.NewUsername);

                    var text = new FormattedText(link, new[] { new TextEntity(0, link.Length, new TextEntityTypeUrl()) });
                    var webPage = string.IsNullOrEmpty(usernameChanged.OldUsername) ? null : new WebPage { SiteName = Strings.Resources.EventLogPreviousLink, Description = MeUrlPrefixConverter.Convert(_protoService, usernameChanged.OldUsername) };

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

                    var whoUser = _protoService.GetUser(memberRestricted.UserId);
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

                    if (!_channel && (n == null || n != null && o != null /*&& n.RestrictedUntilDate != o.RestrictedUntilDate*/))
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

                    var whoUser = _protoService.GetUser(memberPromoted.UserId);
                    var str = Strings.Resources.EventLogPromoted;
                    var userName = GetUserName(whoUser, entities, str.IndexOf("{0}"));
                    var builder = new StringBuilder(string.Format(str, userName));
                    var added = false;

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
                        AppendChange(n.CanChangeInfo, _channel ? Strings.Resources.EventLogPromotedChangeChannelInfo : Strings.Resources.EventLogPromotedChangeGroupInfo);
                    }

                    if (_channel)
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
                    if (!_channel)
                    {
                        if (o.CanRestrictMembers != n.CanRestrictMembers)
                        {
                            AppendChange(n.CanRestrictMembers, Strings.Resources.EventLogPromotedBanUsers);
                        }
                    }
                    if (o.CanInviteUsers != n.CanInviteUsers)
                    {
                        AppendChange(n.CanInviteUsers, Strings.Resources.EventLogPromotedAddUsers);
                    }
                    if (!_channel)
                    {
                        if (o.CanPinMessages != n.CanPinMessages)
                        {
                            AppendChange(n.CanPinMessages, Strings.Resources.EventLogPromotedPinMessages);
                        }
                    }

                    return new MessageText(new FormattedText(builder.ToString(), entities), null);
                }

                return new MessageChatEvent(item, true);
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

            protected override bool GetHasMoreItems()
            {
                return _hasMore;
            }

            #region Insert

            //protected override void InsertItem(int index, MessageViewModel item)
            //{
            //    base.InsertItem(index, item);

            //    var previous = index > 0 ? this[index - 1] : null;
            //    var next = index < Count - 1 ? this[index + 1] : null;

            //    //if (next is TLMessageEmpty)
            //    //{
            //    //    next = index > 1 ? this[index - 2] : null;
            //    //}
            //    //if (previous is TLMessageEmpty)
            //    //{
            //    //    previous = index < Count - 2 ? this[index + 2] : null;
            //    //}

            //    UpdateSeparatorOnInsert(item, next, index);
            //    UpdateSeparatorOnInsert(previous, item, index - 1);

            //    UpdateAttach(next, item, index + 1);
            //    UpdateAttach(item, previous, index);
            //}

            //protected override void RemoveItem(int index)
            //{
            //    var next = index > 0 ? this[index - 1] : null;
            //    var previous = index < Count - 1 ? this[index + 1] : null;

            //    UpdateAttach(previous, next, index + 1);

            //    base.RemoveItem(index);

            //    UpdateSeparatorOnRemove(next, previous, index);
            //}

            //private void UpdateSeparatorOnInsert(MessageViewModel item, MessageViewModel previous, int index)
            //{
            //    if (item != null && previous != null)
            //    {
            //        if ((item is TLMessageService itemService && itemService.Action is TLMessageActionAdminLogEvent) || (previous is TLMessageService previousService && previousService.Action is TLMessageActionAdminLogEvent))
            //        {
            //            return;
            //        }

            //        if (item.Id == previous.Id)
            //        {
            //            return;
            //        }

            //        var itemDate = Utils.UnixTimestampToDateTime(item.Date);
            //        var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
            //        if (previousDate.Date != itemDate.Date)
            //        {
            //            var timestamp = (int)Utils.DateTimeToUnixTimestamp(previousDate.Date);
            //            var service = new TLMessageService
            //            {
            //                Date = timestamp,
            //                FromId = SettingsHelper.UserId,
            //                HasFromId = true,
            //                Action = new TLMessageActionDate
            //                {
            //                    Date = timestamp
            //                }
            //            };

            //            base.InsertItem(index + 1, service);
            //        }
            //    }
            //}

            //private void UpdateSeparatorOnRemove(MessageViewModel next, MessageViewModel previous, int index)
            //{
            //    if (next is TLMessageService && previous != null)
            //    {
            //        var action = ((TLMessageService)next).Action as TLMessageActionDate;
            //        if (action != null)
            //        {
            //            var itemDate = Utils.UnixTimestampToDateTime(action.Date);
            //            var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
            //            if (previousDate.Date != itemDate.Date)
            //            {
            //                base.RemoveItem(index - 1);
            //            }
            //        }
            //    }
            //    else if (next is TLMessageService && previous == null)
            //    {
            //        var action = ((TLMessageService)next).Action as TLMessageActionDate;
            //        if (action != null)
            //        {
            //            base.RemoveItem(index - 1);
            //        }
            //    }
            //}

            //private void UpdateAttach(MessageViewModel item, MessageViewModel previous, int index)
            //{
            //    if (item == null)
            //    {
            //        if (previous != null)
            //        {
            //            previous.IsLast = true;
            //        }

            //        return;
            //    }

            //    var oldFirst = item.IsFirst;
            //    var isItemPost = false;
            //    if (item is TLMessage) isItemPost = ((TLMessage)item).IsPost;

            //    if (!isItemPost)
            //    {
            //        var attach = false;
            //        if (previous != null)
            //        {
            //            var isPreviousPost = false;
            //            if (previous is TLMessage) isPreviousPost = ((TLMessage)previous).IsPost;

            //            attach = !isPreviousPost &&
            //                     !(previous is TLMessageService && !(((TLMessageService)previous).Action is TLMessageActionPhoneCall)) &&
            //                     !(previous.IsService()) &&
            //                     !(previous is TLMessageEmpty) &&
            //                     previous.FromId == item.FromId &&
            //                     item.Date - previous.Date < 900;
            //        }

            //        item.IsFirst = !attach;

            //        if (previous != null)
            //        {
            //            previous.IsLast = item.IsFirst || item.IsService();
            //        }
            //    }
            //    else
            //    {
            //        item.IsFirst = true;

            //        if (previous != null)
            //        {
            //            previous.IsLast = false;
            //        }
            //    }
            //}

            #endregion
        }

        #region Delegate

        public bool CanBeDownloaded(MessageViewModel message)
        {
            return false;
        }

        public void DownloadFile(MessageViewModel message, File file)
        {
        }

        public void ReplyToMessage(MessageViewModel message)
        {
        }

        public void OpenReply(MessageViewModel message)
        {
        }

        public void OpenFile(File file)
        {
        }

        public void OpenWebPage(WebPage webPage)
        {
        }

        public void OpenSticker(Sticker sticker)
        {
        }

        public void OpenLocation(Location location, string title)
        {
        }

        public void OpenLiveLocation(MessageViewModel message)
        {

        }

        public void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button)
        {
        }

        public void OpenMedia(MessageViewModel message, FrameworkElement target)
        {
        }

        public void PlayMessage(MessageViewModel message)
        {
        }

        public void OpenUsername(string username)
        {
        }

        public void OpenHashtag(string hashtag)
        {
        }

        public void OpenUser(int userId)
        {
        }

        public void OpenChat(long chatId)
        {
        }

        public void OpenChat(long chatId, long messageId)
        {
        }

        public void OpenViaBot(int viaBotUserId)
        {
        }

        public void OpenUrl(string url, bool untrust)
        {
        }

        public void SendBotCommand(string command)
        {
        }

        public bool IsAdmin(int userId)
        {
            return false;
        }

        public void Call(MessageViewModel message)
        {
            throw new NotImplementedException();
        }

        public void VotePoll(MessageViewModel message, PollOption option)
        {
            throw new NotImplementedException();
        }

        public string GetAdminTitle(int userId)
        {
            return null;
        }

        #endregion
    }
}
