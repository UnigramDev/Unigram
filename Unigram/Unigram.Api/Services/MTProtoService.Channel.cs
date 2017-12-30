using System;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Native;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Channels;
using Telegram.Api.TL.Channels.Methods;
using Telegram.Api.TL.Messages;
using Telegram.Api.TL.Messages.Methods;
using Telegram.Api.TL.Updates;
using Telegram.Api.TL.Updates.Methods;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void DeleteHistoryAsync(TLInputChannelBase inputChannel, int maxId, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsDeleteHistory { Channel = inputChannel, MaxId = maxId };

            const string caption = "cannels.deleteHistory";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void SetStickersAsync(TLInputChannelBase inputChannel, TLInputStickerSetBase stickerset, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsSetStickers { Channel = inputChannel, StickerSet = stickerset };

            const string caption = "channels.setStickers";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ReadMessageContentsAsync(TLInputChannelBase inputChannel, TLVector<int> id, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsReadMessageContents { Channel = inputChannel, Id = id };

            const string caption = "channels.readMessageContents";
            SendInformativeMessage(caption, obj, callback, /*() => { },*/ faultCallback);
        }

        public void GetAdminLogAsync(TLInputChannelBase inputChannel, string query, TLChannelAdminLogEventsFilter filter, TLVector<TLInputUserBase> admins, long maxId, long minId, int limit, Action<TLChannelsAdminLogResults> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsGetAdminLog { Channel = inputChannel, Q = query, EventsFilter = filter, Admins = admins, MaxId = maxId, MinId = minId, Limit = limit };

            const string caption = "channels.getAdminLog";
            SendInformativeMessage<TLChannelsAdminLogResults>(caption, obj, result =>
            {
                var chats = result as TLChannelsAdminLogResults;
                if (chats != null)
                {
                    _cacheService.SyncUsersAndChats(chats.Users, chats.Chats, tuple => callback?.Invoke(result));
                }
            }, faultCallback);
        }

        public void GetChannelDifferenceAsync(TLInputChannelBase inputChannel, TLChannelMessagesFilterBase filter, int pts, int limit, Action<TLUpdatesChannelDifferenceBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUpdatesGetChannelDifference { Channel = inputChannel, Filter = filter, Pts = pts, Limit = limit };

            const string caption = "updates.getChannelDifference";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetMessagesAsync(TLInputChannelBase inputChannel, TLVector<int> id, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsGetMessages { Channel = inputChannel, Id = id };

            const string caption = "channels.getMessages";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetAdminedPublicChannelsAsync(Action<TLMessagesChatsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsGetAdminedPublicChannels();

            const string caption = "channels.getAdminedPublicChannels";
            SendInformativeMessage<TLMessagesChatsBase>(caption, obj, result =>
            {
                var chats = result as TLMessagesChats;
                if (chats != null)
                {
                    _cacheService.SyncUsersAndChats(new TLVector<TLUserBase>(), chats.Chats, tuple => callback?.Invoke(result));
                }
            },
            faultCallback);
        }

        public void EditAdminAsync(TLChannel channel, TLInputUserBase userId, TLChannelAdminRights rights, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsEditAdmin { Channel = channel.ToInputChannel(), UserId = userId, AdminRights = rights };

            const string caption = "channels.editAdmin";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    GetFullChannelAsync(channel.ToInputChannel(),
                        messagesChatFull => callback?.Invoke(result),
                        faultCallback);
                },
                faultCallback);
        }

        public void EditBannedAsync(TLChannel channel, TLInputUserBase userId, TLChannelBannedRights rights, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsEditBanned { Channel = channel.ToInputChannel(), UserId = userId, BannedRights = rights };

            const string caption = "channels.editBanned";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    GetFullChannelAsync(channel.ToInputChannel(),
                        messagesChatFull => callback?.Invoke(result),
                        faultCallback);
                },
                faultCallback);
        }

        public void GetParticipantAsync(TLInputChannelBase inputChannel, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsGetParticipant { Channel = inputChannel, UserId = userId };

            const string caption = "channels.getParticipant";
            SendInformativeMessage<TLChannelsChannelParticipant>(caption, obj, result =>
            {
                _cacheService.SyncUsers(result.Users, r => { });

                callback?.Invoke(result);
            },
            faultCallback);
        }

        public void GetParticipantsAsync(TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, int offset, int limit, int hash, Action<TLChannelsChannelParticipantsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsGetParticipants { Channel = inputChannel, Filter = filter, Offset = offset, Limit = limit, Hash = hash };

            const string caption = "channels.getParticipants";
            SendInformativeMessage<TLChannelsChannelParticipantsBase>(caption, obj,
                result =>
                {
                    //for (var i = 0; i < result.Users.Count; i++)
                    //{
                    //    var cachedUser = _cacheService.GetUser(result.Users[i].Id) as TLUser;
                    //    if (cachedUser != null)
                    //    {
                    //        // TODO: cachedUser._status = ((TLUser)result.Users[i]).Status;
                    //        cachedUser.Status = ((TLUser)result.Users[i]).Status;
                    //        result.Users[i] = cachedUser;
                    //    }
                    //}

                    if (result is TLChannelsChannelParticipants participants)
                    {
                        _cacheService.SyncUsers(participants.Users, r => { });
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void EditTitleAsync(TLChannel channel, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsEditTitle { Channel = channel.ToInputChannel(), Title = title };

            const string caption = "channels.editTitle";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void EditAboutAsync(TLChannel channel, string about, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsEditAbout { Channel = channel.ToInputChannel(), About = about };

            const string caption = "channels.editAbout";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void JoinChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsJoinChannel { Channel = channel.ToInputChannel() };

            const string caption = "channels.joinChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    channel.IsLeft = false;
                    if (channel.ParticipantsCount != null)
                    {
                        channel.ParticipantsCount = channel.ParticipantsCount + 1;
                    }
                    _cacheService.Commit();

                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void LeaveChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsLeaveChannel { Channel = channel.ToInputChannel() };

            const string caption = "channels.leaveChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    channel.IsLeft = true;
                    if (channel.ParticipantsCount != null)
                    {
                        channel.ParticipantsCount = channel.ParticipantsCount.Value - 1;
                    }
                    _cacheService.Commit();

                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void DeleteChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsDeleteChannel { Channel = channel.ToInputChannel() };

            const string caption = "channels.deleteChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void InviteToChannelAsync(TLInputChannelBase channel, TLVector<TLInputUserBase> users, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsInviteToChannel { Channel = channel, Users = users };

            const string caption = "channels.inviteToChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void DeleteMessagesAsync(TLInputChannelBase channel, TLVector<int> id, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsDeleteMessages { Channel = channel, Id = id };

            const string caption = "channels.deleteMessages";
            SendInformativeMessage<TLMessagesAffectedMessages>(caption, obj,
                result =>
                {
                    //var multiPts = result as ITLMultiPts;
                    //if (multiPts != null)
                    //{
                    //    _updatesService.SetState(multiPts, caption);
                    //}
                    //else
                    //{
                    //    _updatesService.SetState(null, result.Pts, null, null, null, caption);
                    //}

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void UpdateChannelAsync(int? channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null)
        {
            var channel = _cacheService.GetChat(channelId) as TLChannel;
            if (channel != null)
            {
                GetFullChannelAsync(channel.ToInputChannel(), callback, faultCallback);
                return;
            }

            var channelForbidden = _cacheService.GetChat(channelId) as TLChannelForbidden;
            if (channelForbidden != null)
            {
                GetFullChannelAsync(channelForbidden.ToInputChannel(), callback, faultCallback);
                return;
            }
        }

        public void GetFullChannelAsync(TLInputChannelBase channel, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsGetFullChannel { Channel = channel };

            SendInformativeMessage<TLMessagesChatFull>("channels.getFullChannel", obj,
                messagesChatFull =>
                {
                    _cacheService.SyncChat(messagesChatFull, result => callback?.Invoke(messagesChatFull));
                },
                faultCallback);
        }

        public void ReadHistoryAsync(TLChannel channel, int maxId, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsReadHistory { Channel = channel.ToInputChannel(), MaxId = maxId };

            const string caption = "channels.readHistory";
            SendInformativeMessage<bool>(caption, obj,
                result =>
                {
                    channel.ReadInboxMaxId = maxId;

                    _cacheService.Commit();

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void CreateChannelAsync(TLChannelsCreateChannel.Flag flags, string title, string about, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsCreateChannel { Flags = flags, Title = title, About = about };

            var caption = "channels.createChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void ExportInviteAsync(TLInputChannelBase channel, Action<TLExportedChatInviteBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsExportInvite { Channel = channel };

            const string caption = "channels.exportInvite";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void CheckUsernameAsync(TLInputChannelBase channel, string username, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsCheckUsername { Channel = channel, Username = username };

            const string caption = "channels.checkUsername";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void UpdateUsernameAsync(TLInputChannelBase channel, string username, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsUpdateUsername { Channel = channel, Username = username };

            const string caption = "channels.updateUsername";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void EditPhotoAsync(TLChannel channel, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsEditPhoto { Channel = channel.ToInputChannel(), Photo = photo };

            const string caption = "channels.editPhoto";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null, true);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void DeleteChannelMessagesAsync(TLInputChannelBase channel, TLVector<int> id, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsDeleteMessages { Channel = channel, Id = id };

            const string caption = "channels.deleteMessages";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ToggleInvitesAsync(TLInputChannelBase channel, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsToggleInvites { Channel = channel, Enabled = enabled };

            const string caption = "channels.toggleInvites";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void ExportMessageLinkAsync(TLInputChannelBase channel, int id, Action<TLExportedMessageLink> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsExportMessageLink { Channel = channel, Id = id };

            const string caption = "channels.exportMessageLink";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void UpdatePinnedMessageAsync(bool silent, TLInputChannelBase channel, int id, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsUpdatePinnedMessage { Flags = 0, Channel = channel, Id = id };
            if (silent)
            {
                obj.IsSilent = true;
            }

            const string caption = "channels.updatePinnedMessage";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void ToggleSignaturesAsync(TLInputChannelBase channel, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsToggleSignatures { Channel = channel, Enabled = enabled };

            const string caption = "channels.toggleSignatures";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void TogglePreHistoryHiddenAsync(TLInputChannelBase channel, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsTogglePreHistoryHidden { Channel = channel, Enabled = enabled };

            const string caption = "channels.togglePreHistoryHidden";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void GetMessageEditDataAsync(TLInputPeerBase peer, int id, Action<TLMessagesMessageEditData> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetMessageEditData { Peer = peer, Id = id };

            const string caption = "messages.getMessageEditData";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void EditMessageAsync(TLInputPeerBase peer, int id, string message, TLVector<TLMessageEntityBase> entities, TLReplyMarkupBase replyMarkup, TLInputGeoPointBase geoPoint, bool noWebPage, bool stop, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesEditMessage { Flags = 0, Peer = peer, Id = id, Message = message, IsNoWebPage = noWebPage, Entities = entities, ReplyMarkup = replyMarkup, GeoPoint = geoPoint, IsStopGeoLive = stop };

            const string caption = "messages.editMessage";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null, true);
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void ReportSpamAsync(TLInputChannelBase channel, TLInputUserBase userId, TLVector<int> id, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsReportSpam { Channel = channel, UserId = userId, Id = id };

            const string caption = "channels.reportSpam";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void DeleteUserHistoryAsync(TLChannel channel, TLInputUserBase userId, Action<TLMessagesAffectedHistory> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLChannelsDeleteUserHistory { Channel = channel.ToInputChannel(), UserId = userId };

            const string caption = "channels.deleteUserHistory";
            SendInformativeMessage<TLMessagesAffectedHistory>(caption, obj,
                result =>
                {
                    var multiChannelPts = result as ITLMultiChannelPts;
                    if (multiChannelPts != null)
                    {
                        if (channel.Pts == null || channel.Pts.Value + multiChannelPts.PtsCount != multiChannelPts.Pts)
                        {
                            Execute.ShowDebugMessage(string.Format("channel_id={0} channel_pts={1} affectedHistory24[channel_pts={2} channel_pts_count={3}]", channel.Id, channel.Pts, multiChannelPts.Pts, multiChannelPts.PtsCount));
                        }
                        channel.Pts = multiChannelPts.Pts;
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }
    }
}
