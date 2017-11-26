using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Strings;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelAdminLogViewModel : UnigramViewModelBase
    {
        public ChannelAdminLogViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        protected TLChannel _item;
        public TLChannel Item
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
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
            Item = null;
            IsLoading = true;

            var channel = parameter as TLChannel;
            var peer = parameter as TLPeerChannel;
            if (peer != null)
            {
                channel = CacheService.GetChat(peer.ChannelId) as TLChannel;
            }

            if (channel != null)
            {
                Item = channel;

                Items = new ItemsCollection(ProtoService, this, channel);
                Items.CollectionChanged += (s, args) => IsEmpty = Items.Count == 0;

                RaisePropertyChanged(() => Items);
            }

            return Task.CompletedTask;
        }

        public ItemsCollection Items { get; protected set; }

        public class ItemsCollection : IncrementalCollection<TLMessageBase>
        {
            private readonly IMTProtoService _protoService;
            private readonly ChannelAdminLogViewModel _viewModel;
            private readonly TLInputChannelBase _inputChannel;
            private readonly TLChannel _channel;
            //private readonly TLChannelParticipantsFilterBase _filter;

            private long _minEventId = long.MaxValue;
            private bool _hasMore;

            public ItemsCollection(IMTProtoService protoService, ChannelAdminLogViewModel viewModel, TLChannel channel)
            {
                _protoService = protoService;
                _viewModel = viewModel;
                _inputChannel = channel.ToInputChannel();
                _channel = channel;
                //_filter = filter;
                _hasMore = true;
            }

            public override async Task<IList<TLMessageBase>> LoadDataAsync()
            {
                _viewModel.IsLoading = true;
                _hasMore = false;

                var maxId = Count > 0 ? _minEventId : 0;

                var response = await _protoService.GetAdminLogAsync(_inputChannel, null, null, null, maxId, 0, 50);
                if (response.IsSucceeded)
                {
                    _viewModel.IsLoading = false;

                    var result = new List<TLMessageBase>();

                    foreach (var item in response.Result.Events)
                    {
                        _minEventId = Math.Min(_minEventId, item.Id);

                        if (item.Action is TLChannelAdminLogEventActionChangeTitle changeTitle)
                        {
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionChangeAbout changeAbout)
                        {
                            var message = new TLMessage();
                            //message.Id = item.Id;
                            message.FromId = item.UserId;
                            message.ToId = _channel.ToPeer();
                            message.Date = item.Date;
                            message.Message = changeAbout.NewValue;
                            message.HasMedia = true;

                            if (string.IsNullOrEmpty(changeAbout.PrevValue))
                            {
                                message.Media = new TLMessageMediaEmpty();
                            }
                            else
                            {
                                message.Media = new TLMessageMediaWebPage
                                {
                                    WebPage = new TLWebPage
                                    {
                                        SiteName = Strings.Android.EventLogPreviousGroupDescription,
                                        Description = changeAbout.PrevValue,
                                        HasSiteName = true,
                                        HasDescription = true
                                    }
                                };
                            }

                            result.Insert(0, message);
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionChangeUsername changeUsername)
                        {
                            var message = new TLMessage();
                            //message.Id = item.Id;
                            message.FromId = item.UserId;
                            message.ToId = _channel.ToPeer();
                            message.Date = item.Date;
                            message.Message = string.IsNullOrEmpty(changeUsername.NewValue) ? string.Empty : MeUrlPrefixConverter.Convert(changeUsername.NewValue);
                            message.Entities = new TLVector<TLMessageEntityBase>();
                            message.HasMedia = true;
                            message.HasEntities = true;

                            message.Entities.Add(new TLMessageEntityUrl { Offset = 0, Length = message.Message.Length });

                            if (string.IsNullOrEmpty(changeUsername.PrevValue))
                            {
                                message.Media = new TLMessageMediaEmpty();
                            }
                            else
                            {
                                message.Media = new TLMessageMediaWebPage
                                {
                                    WebPage = new TLWebPage
                                    {
                                        SiteName = Strings.Android.EventLogPreviousLink,
                                        Description = MeUrlPrefixConverter.Convert(changeUsername.PrevValue),
                                        HasSiteName = true,
                                        HasDescription = true
                                    }
                                };
                            }

                            result.Insert(0, message);
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionChangePhoto changePhoto)
                        {
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionToggleInvites toggleInvites)
                        {
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionToggleSignatures toggleSignatures)
                        {
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionTogglePreHistoryHidden togglePreHistoryHidden)
                        {
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionUpdatePinned updatePinned)
                        {
                            // Patch for view
                            if (updatePinned.Message is TLMessageCommonBase messageCommon)
                            {
                                messageCommon.ReplyToMsgId = null;
                                messageCommon.IsOut = false;
                                messageCommon.IsPost = false;
                            }

                            if (!(updatePinned.Message is TLMessageEmpty))
                            {
                                result.Insert(0, updatePinned.Message);
                            }

                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionEditMessage editMessage)
                        {
                            // TODO: the actual message
                            if (editMessage.NewMessage is TLMessageCommonBase messageCommon)
                            {
                                messageCommon.ReplyToMsgId = editMessage.PrevMessage.Id;
                                messageCommon.IsOut = false;
                                messageCommon.IsPost = false;
                            }
                            if (editMessage.NewMessage is TLMessage message)
                            {
                                message.Reply = editMessage.PrevMessage;
                                message.EditDate = null;
                                message.HasEditDate = false;
                            }

                            result.Insert(0, editMessage.NewMessage);
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionDeleteMessage deleteMessage)
                        {
                            // Patch for view
                            if (deleteMessage.Message is TLMessageCommonBase messageCommon)
                            {
                                messageCommon.ReplyToMsgId = null;
                                messageCommon.IsOut = false;
                                messageCommon.IsPost = false;
                            }

                            result.Insert(0, deleteMessage.Message);
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionParticipantJoin participantJoin)
                        {
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionParticipantLeave participantLeave)
                        {
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionParticipantInvite participantInvite)
                        {
                            result.Insert(0, GetServiceMessage(item));
                        }
                        else if (item.Action is TLChannelAdminLogEventActionParticipantToggleBan participantToggleBan)
                        {
                            var message = new TLMessage();
                            //message.Id = item.Id;
                            message.FromId = item.UserId;
                            message.ToId = _channel.ToPeer();
                            message.Date = item.Date;
                            //message.Message = from.ReadString();
                            message.Entities = new TLVector<TLMessageEntityBase>();

                            message.HasFromId = true;
                            message.HasEntities = true;

                            var whoUser = participantToggleBan.PrevParticipant.User;
                            TLChannelBannedRights o = null;
                            TLChannelBannedRights n = null;

                            if (participantToggleBan.PrevParticipant is TLChannelParticipantBanned prevBanned)
                            {
                                o = prevBanned.BannedRights;
                            }
                            if (participantToggleBan.NewParticipant is TLChannelParticipantBanned newBanned)
                            {
                                n = newBanned.BannedRights;
                            }

                            if (_channel.IsMegaGroup && (n == null || !n.IsViewMessages || n != null && o != null && n.UntilDate != o.UntilDate))
                            {
                                StringBuilder rights;
                                String bannedDuration;
                                if (n != null && !AdminLogHelper.IsBannedForever(n.UntilDate))
                                {
                                    bannedDuration = "";
                                    int duration = n.UntilDate - item.Date;
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
                                                addStr = LocaleHelper.Declension("Days", days);
                                                count++;
                                            }
                                        }
                                        else if (a == 1)
                                        {
                                            if (hours != 0)
                                            {
                                                addStr = LocaleHelper.Declension("Hours", hours);
                                                count++;
                                            }
                                        }
                                        else
                                        {
                                            if (minutes != 0)
                                            {
                                                addStr = LocaleHelper.Declension("Minutes", minutes);
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
                                    bannedDuration = Strings.Android.UserRestrictionsUntilForever;
                                }

                                var str = Strings.Android.EventLogRestrictedUntil;
                                rights = new StringBuilder(String.Format(str, GetUserName(whoUser, message.Entities, str.IndexOf("{0}")), bannedDuration));
                                var added = false;
                                if (o == null)
                                {
                                    o = new TLChannelBannedRights();
                                }
                                if (n == null)
                                {
                                    n = new TLChannelBannedRights();
                                }

                                void AppendChange(bool value, string label)
                                {
                                    if (!added)
                                    {
                                        rights.Append('\n');
                                        added = true;
                                    }

                                    rights.Append('\n').Append(!value ? '+' : '-').Append(' ');
                                    rights.Append(label);
                                }

                                if (o.IsViewMessages != n.IsViewMessages)
                                {
                                    AppendChange(n.IsViewMessages, Strings.Android.EventLogRestrictedReadMessages);
                                }
                                if (o.IsSendMessages != n.IsSendMessages)
                                {
                                    AppendChange(n.IsSendMessages, Strings.Android.EventLogRestrictedSendMessages);
                                }
                                if (o.IsSendStickers != n.IsSendStickers || o.IsSendInline != n.IsSendInline || o.IsSendGifs != n.IsSendGifs || o.IsSendGames != n.IsSendGames)
                                {
                                    AppendChange(n.IsSendStickers, Strings.Android.EventLogRestrictedSendStickers);
                                }
                                if (o.IsSendMedia != n.IsSendMedia)
                                {
                                    AppendChange(n.IsSendMedia, Strings.Android.EventLogRestrictedSendMedia);
                                }
                                if (o.IsEmbedLinks != n.IsEmbedLinks)
                                {
                                    AppendChange(n.IsEmbedLinks, Strings.Android.EventLogRestrictedSendEmbed);
                                }

                                message.Message = rights.ToString();
                            }
                            else
                            {
                                String str;
                                if (n != null && (o == null || n.IsViewMessages))
                                {
                                    str = Strings.Android.EventLogChannelRestricted;
                                }
                                else
                                {
                                    str = Strings.Android.EventLogChannelUnrestricted;
                                }

                                message.Message = String.Format(str, GetUserName(whoUser, message.Entities, str.IndexOf("{0}")));
                            }

                            result.Insert(0, message);
                        }
                        else if (item.Action is TLChannelAdminLogEventActionParticipantToggleAdmin participantToggleAdmin)
                        {
                            var message = new TLMessage();
                            //message.Id = item.Id;
                            message.FromId = item.UserId;
                            message.ToId = _channel.ToPeer();
                            message.Date = item.Date;
                            //message.Message = from.ReadString();
                            message.Entities = new TLVector<TLMessageEntityBase>();

                            message.HasFromId = true;
                            message.HasEntities = true;

                            var whoUser = participantToggleAdmin.PrevParticipant.User;
                            var str = Strings.Android.EventLogPromoted;
                            var userName = GetUserName(whoUser, message.Entities, str.IndexOf("{0}"));
                            var builder = new StringBuilder(string.Format(str, userName));
                            var added = false;

                            TLChannelAdminRights o = null;
                            TLChannelAdminRights n = null;

                            if (participantToggleAdmin.PrevParticipant is TLChannelParticipantAdmin prevAdmin)
                            {
                                o = prevAdmin.AdminRights;
                            }
                            if (participantToggleAdmin.NewParticipant is TLChannelParticipantAdmin newAdmin)
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

                            if (o.IsChangeInfo != n.IsChangeInfo)
                            {
                                AppendChange(n.IsChangeInfo, _channel.IsMegaGroup ? Strings.Android.EventLogPromotedChangeGroupInfo : Strings.Android.EventLogPromotedChangeChannelInfo);
                            }

                            if (!_channel.IsMegaGroup)
                            {
                                if (o.IsPostMessages != n.IsPostMessages)
                                {
                                    AppendChange(n.IsPostMessages, Strings.Android.EventLogPromotedPostMessages);
                                }
                                if (o.IsEditMessages != n.IsEditMessages)
                                {
                                    AppendChange(n.IsEditMessages, Strings.Android.EventLogPromotedEditMessages);
                                }
                            }
                            if (o.IsDeleteMessages != n.IsDeleteMessages)
                            {
                                AppendChange(n.IsDeleteMessages, Strings.Android.EventLogPromotedDeleteMessages);
                            }
                            if (o.IsAddAdmins != n.IsAddAdmins)
                            {
                                AppendChange(n.IsAddAdmins, Strings.Android.EventLogPromotedAddAdmins);
                            }
                            if (_channel.IsMegaGroup)
                            {
                                if (o.IsBanUsers != n.IsBanUsers)
                                {
                                    AppendChange(n.IsBanUsers, Strings.Android.EventLogPromotedBanUsers);
                                }
                            }
                            if (o.IsInviteUsers != n.IsInviteUsers)
                            {
                                AppendChange(n.IsInviteUsers, Strings.Android.EventLogPromotedAddUsers);
                            }
                            if (_channel.IsMegaGroup)
                            {
                                if (o.IsPinMessages != n.IsPinMessages)
                                {
                                    AppendChange(n.IsPinMessages, Strings.Android.EventLogPromotedPinMessages);
                                }
                            }

                            message.Message = builder.ToString();

                            result.Insert(0, message);
                        }
                        else if (item.Action is TLChannelAdminLogEventActionChangeStickerSet changeStickerSet)
                        {
                            result.Insert(0, GetServiceMessage(item));
                        }
                    }

                    if (response.Result.Events.Count < 50)
                    {
                        _hasMore = false;
                    }

                    return result;
                }

                _viewModel.IsLoading = false;

                return new TLMessageBase[0];
            }

            private TLMessageService GetServiceMessage(TLChannelAdminLogEvent item)
            {
                var message = new TLMessageService();
                //message.Id = item.Id;
                message.FromId = item.UserId;
                message.ToId = _channel.ToPeer();
                message.Date = item.Date;
                message.Action = new TLMessageActionAdminLogEvent { Event = item };

                return message;
            }

            private string GetUserName(TLUser user, TLVector<TLMessageEntityBase> entities, int offset)
            {
                string name;
                if (user == null)
                {
                    name = string.Empty;
                }
                else
                {
                    name = user.FullName;
                }

                if (offset >= 0)
                {
                    var entity = new TLMessageEntityMentionName();
                    entity.UserId = user.Id;
                    entity.Offset = offset;
                    entity.Length = name.Length;
                    entities.Add(entity);
                }

                if (string.IsNullOrEmpty(user.Username))
                {
                    return name;
                }

                if (offset >= 0)
                {
                    var entity = new TLMessageEntityMentionName();
                    entity.UserId = user.Id;
                    entity.Offset = (name.Length + offset) + 2;
                    entity.Length = user.Username.Length + 1;
                    entities.Add(entity);
                }

                return String.Format("{0} (@{1})", name, user.Username);
            }

            protected override bool GetHasMoreItems()
            {
                return _hasMore;
            }

            #region Insert

            protected override void InsertItem(int index, TLMessageBase item)
            {
                base.InsertItem(index, item);

                var previous = index > 0 ? this[index - 1] : null;
                var next = index < Count - 1 ? this[index + 1] : null;

                //if (next is TLMessageEmpty)
                //{
                //    next = index > 1 ? this[index - 2] : null;
                //}
                //if (previous is TLMessageEmpty)
                //{
                //    previous = index < Count - 2 ? this[index + 2] : null;
                //}

                UpdateSeparatorOnInsert(item, next, index);
                UpdateSeparatorOnInsert(previous, item, index - 1);

                UpdateAttach(next, item, index + 1);
                UpdateAttach(item, previous, index);
            }

            protected override void RemoveItem(int index)
            {
                var next = index > 0 ? this[index - 1] : null;
                var previous = index < Count - 1 ? this[index + 1] : null;

                UpdateAttach(previous, next, index + 1);

                base.RemoveItem(index);

                UpdateSeparatorOnRemove(next, previous, index);
            }

            private void UpdateSeparatorOnInsert(TLMessageBase item, TLMessageBase previous, int index)
            {
                if (item != null && previous != null)
                {
                    if ((item is TLMessageService itemService && itemService.Action is TLMessageActionAdminLogEvent) || (previous is TLMessageService previousService && previousService.Action is TLMessageActionAdminLogEvent))
                    {
                        return;
                    }

                    if (item.Id == previous.Id)
                    {
                        return;
                    }

                    var itemDate = Utils.UnixTimestampToDateTime(item.Date);
                    var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
                    if (previousDate.Date != itemDate.Date)
                    {
                        var timestamp = (int)Utils.DateTimeToUnixTimestamp(previousDate.Date);
                        var service = new TLMessageService
                        {
                            Date = timestamp,
                            FromId = SettingsHelper.UserId,
                            HasFromId = true,
                            Action = new TLMessageActionDate
                            {
                                Date = timestamp
                            }
                        };

                        base.InsertItem(index + 1, service);
                    }
                }
            }

            private void UpdateSeparatorOnRemove(TLMessageBase next, TLMessageBase previous, int index)
            {
                if (next is TLMessageService && previous != null)
                {
                    var action = ((TLMessageService)next).Action as TLMessageActionDate;
                    if (action != null)
                    {
                        var itemDate = Utils.UnixTimestampToDateTime(action.Date);
                        var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
                        if (previousDate.Date != itemDate.Date)
                        {
                            base.RemoveItem(index - 1);
                        }
                    }
                }
                else if (next is TLMessageService && previous == null)
                {
                    var action = ((TLMessageService)next).Action as TLMessageActionDate;
                    if (action != null)
                    {
                        base.RemoveItem(index - 1);
                    }
                }
            }

            private void UpdateAttach(TLMessageBase item, TLMessageBase previous, int index)
            {
                if (item == null)
                {
                    if (previous != null)
                    {
                        previous.IsLast = true;
                    }

                    return;
                }

                var oldFirst = item.IsFirst;
                var isItemPost = false;
                if (item is TLMessage) isItemPost = ((TLMessage)item).IsPost;

                if (!isItemPost)
                {
                    var attach = false;
                    if (previous != null)
                    {
                        var isPreviousPost = false;
                        if (previous is TLMessage) isPreviousPost = ((TLMessage)previous).IsPost;

                        attach = !isPreviousPost &&
                                 !(previous is TLMessageService && !(((TLMessageService)previous).Action is TLMessageActionPhoneCall)) &&
                                 !(previous.IsService()) &&
                                 !(previous is TLMessageEmpty) &&
                                 previous.FromId == item.FromId &&
                                 item.Date - previous.Date < 900;
                    }

                    item.IsFirst = !attach;

                    if (previous != null)
                    {
                        previous.IsLast = item.IsFirst || item.IsService();
                    }
                }
                else
                {
                    item.IsFirst = true;

                    if (previous != null)
                    {
                        previous.IsLast = false;
                    }
                }

                //if (item.IsFirst && item is TLMessage)
                //{
                //    var message = item as TLMessage;
                //    if (message != null && !message.IsPost && !message.IsOut)
                //    {
                //        base.InsertItem(index, new TLMessageEmpty { Date = item.Date, FromId = item.FromId, Id = item.Id, ToId = item.ToId });
                //    }
                //}

                //if (!item.IsFirst && oldFirst)
                //{
                //    var next = index > 0 ? this[index - 1] : null;
                //    if (next is TLMessageEmpty)
                //    {
                //        Remove(item);
                //    }
                //}
            }

            #endregion
        }
    }
}
