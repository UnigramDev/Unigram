using System;
using Org.BouncyCastle.Bcpg;
using Telegram.Api.Extensions;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using System.Threading.Tasks;
using Telegram.Api.TL.Methods.Channels;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public async Task<MTProtoResponse<TLMessagesMessagesBase>> GetMessagesAsync(TLInputChannelBase inputChannel, TLVector<int> id)
        {
            var obj = new TLChannelsGetMessages { Channel = inputChannel, Id = id };

            return await SendInformativeMessage<TLMessagesMessagesBase>("channels.getMessages", obj);
        }

        public async Task<MTProtoResponse<bool>> EditAdminAsync(TLChannel channel, TLInputUserBase userId, TLChannelParticipantRoleBase role)
        {
            var obj = new TLChannelsEditAdmin { Channel = channel.ToInputChannel(), UserId = userId, Role = role };

            var result = await SendInformativeMessage<bool>("channels.editAdmin", obj);

            if (result.Error == null)
            {
                if (channel.AdminsCount != null)
                {
                    if (role is TLChannelRoleEmpty)
                    {
                        channel.AdminsCount = channel.AdminsCount - 1;
                    }
                    else
                    {
                        channel.AdminsCount = channel.AdminsCount + 1;
                    }

                    _cacheService.Commit();
                }
            }

            return result;
        }

        //TLInputChannelBase channelId, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback
        public async Task<MTProtoResponse<TLChannelsChannelParticipant>> GetParticipantAsync(TLInputChannelBase inputChannel, TLInputUserBase userId)
        {
            var obj = new TLChannelsGetParticipant { Channel = inputChannel, UserId = userId };

            var result = await SendInformativeMessage<TLChannelsChannelParticipant>("channels.getParticipant", obj);
            if (result.Error == null)
            {
                _cacheService.SyncUsers(result.Value.Users, r => { });
            }

            return result;
        }
        public async void GetParticipantCallbackAsync(TLInputChannelBase inputChannel, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback)
        {
            var obj = new TLChannelsGetParticipant { Channel = inputChannel, UserId = userId };

            var result = await SendInformativeMessage<TLChannelsChannelParticipant>("channels.getParticipant", obj);
            if (result.IsSucceeded)
            {
                _cacheService.SyncUsers(result.Value.Users, r => { });
                callback.SafeInvoke(result.Value);
            }
            else
            {
                faultCallback.SafeInvoke(result.Error);
            }
        }

        public async Task<MTProtoResponse<TLChannelsChannelParticipants>> GetParticipantsAsync(TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, int offset, int limit)
        {
            var obj = new TLChannelsGetParticipants { Channel = inputChannel, Filter = filter, Offset = offset, Limit = limit };

            const string caption = "channels.getParticipants";
            return await SendInformativeMessage<TLChannelsChannelParticipants>(caption, obj);
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> EditTitleAsync(TLChannel channel, string title)
        {
            var obj = new TLChannelsEditTitle { Channel = channel.ToInputChannel(), Title = title };

            var result = await SendInformativeMessage<TLUpdatesBase>("channels.editTitle", obj);
            if (result.Error == null)
            {

                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, "channels.editTitle");
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<bool>> EditAboutAsync(TLChannel channel, string about)
        {
            var obj = new TLChannelsEditAbout { Channel = channel.ToInputChannel(), About = about };

            return await SendInformativeMessage<bool>("channels.editAbout", obj);
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> JoinChannelAsync(TLChannel channel)
        {
            var obj = new TLChannelsJoinChannel { Channel = channel.ToInputChannel() };

            var result = await SendInformativeMessage<TLUpdatesBase>("channels.joinChannel", obj);
            if (result.Error == null)
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
                    _updatesService.SetState(multiPts, "channels.joinChannel");
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }
            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> LeaveChannelAsync(TLChannel channel)
        {
            var obj = new TLChannelsLeaveChannel { Channel = channel.ToInputChannel() };

            var result = await SendInformativeMessage<TLUpdatesBase>("channels.leaveChannel", obj);
            if (result.Error == null)
            {
                channel.IsLeft = true;
                if (channel.ParticipantsCount != null)
                {
                    channel.ParticipantsCount = channel.ParticipantsCount - 1;
                }
                _cacheService.Commit();

                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, "channels.leaveChannel");
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> KickFromChannelAsync(TLChannel channel, TLInputUserBase userId, bool kicked)
        {
            var obj = new TLChannelsKickFromChannel { Channel = channel.ToInputChannel(), UserId = userId, Kicked = kicked };

            var result = await SendInformativeMessage<TLUpdatesBase>("channels.kickFromChannel", obj);
            if (result.Error == null)
            {
                if (channel.ParticipantsCount != null)
                {
                    channel.ParticipantsCount = channel.ParticipantsCount - 1;
                }
                _cacheService.Commit();

                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, "channels.kickFromChannel");
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> DeleteChannelAsync(TLChannel channel)
        {
            var obj = new TLChannelsDeleteChannel { Channel = channel.ToInputChannel() };

            var result = await SendInformativeMessage<TLUpdatesBase>("channels.deleteChannel", obj);
            if (result.Error == null)
            {

                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, "channels.deleteChannel");
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> InviteToChannelAsync(TLInputChannelBase channel, TLVector<TLInputUserBase> users)
        {
            var obj = new TLChannelsInviteToChannel { Channel = channel, Users = users };

            var result = await SendInformativeMessage<TLUpdatesBase>("channels.inviteToChannel", obj);
            if (result.Error == null)
            {
                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, "channels.inviteToChannel");
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public Task<MTProtoResponse<TLMessagesAffectedMessages>> DeleteMessagesAsync(TLInputChannelBase channel, TLVector<int> id)
        {
            var obj = new TLChannelsDeleteMessages { Channel = channel, Id = id };

            return SendInformativeMessage<TLMessagesAffectedMessages>("channels.deleteMessages", obj);
        }

        public Task<MTProtoResponse<TLMessagesChatFull>> UpdateChannelAsync(int channelId)
        {
            var channel = _cacheService.GetChat(channelId) as TLChannel;
            if (channel != null)
            {
                return GetFullChannelAsync(channel.ToInputChannel());
            }

            var channelForbidden = _cacheService.GetChat(channelId) as TLChannelForbidden;
            if (channelForbidden != null)
            {
                return GetFullChannelAsync(channelForbidden.ToInputChannel());
            }

            return null;
        }
        public async void UpdateChannelCallbackAsync(int channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback)
        {
            var channel = _cacheService.GetChat(channelId) as TLChannel;
            if (channel != null)
            {
                var result = await GetFullChannelAsync(channel.ToInputChannel());
                if (result.IsSucceeded)
                {
                    callback(result.Value);
                }
                else
                {
                    faultCallback(result.Error);
                }
            }

            var channelForbidden = _cacheService.GetChat(channelId) as TLChannelForbidden;
            if (channelForbidden != null)
            {
                var result = await GetFullChannelAsync(channelForbidden.ToInputChannel());
                if (result.IsSucceeded)
                {
                    callback(result.Value);
                }
                else
                {
                    faultCallback(result.Error);
                }
            }
        }

        public async Task<MTProtoResponse<TLMessagesChatFull>> GetFullChannelAsync(TLInputChannelBase channel)
        {
            var obj = new TLChannelsGetFullChannel { Channel = channel };

            var result = await SendInformativeMessage<TLMessagesChatFull>("cnannels.getFullChannel", obj);

            if (result.Error == null)
            {
                _cacheService.SyncChat(result.Value, null);
            }

            return result;
        }

        // TODO:
        //public Task<MTProtoResponse<TLMessagesMessagesBase>> GetImportantHistoryAsync(TLInputChannelBase channel, TLPeerBase peer, bool sync, int offsetId, int addOffset, int limit, int maxId, int minId)
        //{
        //    var obj = new TLChannelsGetImportantHistory { Channel = channel, OffsetId = offsetId, AddOffset = addOffset, Limit = limit, MaxId = maxId, MinId = minId };

        //    return SendInformativeMessage<TLMessagesMessagesBase>("channels.getImportantHistory", obj);
        //}

        public async Task<MTProtoResponse<bool>> ReadHistoryAsync(TLChannel channel, int maxId)
        {
            var obj = new TLChannelsReadHistory { Channel = channel.ToInputChannel(), MaxId = maxId };

            var result = await SendInformativeMessage<bool>("channels.readHistory", obj);
            if (result.Error == null)
            {
                channel.ReadInboxMaxId = maxId;
                _cacheService.Commit();
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> CreateChannelAsync(int flags, string title, string about)
        {
            var obj = new TLChannelsCreateChannel { Flags = (TLChannelsCreateChannel.Flag)flags, Title = title, About = about };

            var result = await SendInformativeMessage<TLUpdatesBase>("channels.createChannel", obj);
            if (result.Error == null)
            {
                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, "channels.createChannel");
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public Task<MTProtoResponse<TLExportedChatInviteBase>> ExportInviteAsync(TLInputChannelBase channel)
        {
            var obj = new TLChannelsExportInvite { Channel = channel };

            return SendInformativeMessage<TLExportedChatInviteBase>("channels.exportInvite", obj);
        }

        public Task<MTProtoResponse<bool>> CheckUsernameAsync(TLInputChannelBase channel, string username)
        {
            var obj = new TLChannelsCheckUsername { Channel = channel, Username = username };

            return SendInformativeMessage<bool>("channels.checkUsername", obj);
        }

        public Task<MTProtoResponse<bool>> UpdateUsernameAsync(TLInputChannelBase channel, string username)
        {
            var obj = new TLChannelsUpdateUsername { Channel = channel, Username = username };

            return SendInformativeMessage<bool>("channels.updateUsername", obj);
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> EditPhotoAsync(TLChannel channel, TLInputChatPhotoBase photo)
        {
            var obj = new TLChannelsEditPhoto { Channel = channel.ToInputChannel(), Photo = photo };

            var result = await SendInformativeMessage<TLUpdatesBase>("channels.editPhoto", obj);
            if (result.Error == null)
            {
                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, "channels.editPhoto");
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public Task<MTProtoResponse<TLMessagesAffectedMessages>> DeleteChannelMessagesAsync(TLInputChannelBase channel, TLVector<int> id)
        {
            var obj = new TLChannelsDeleteMessages { Channel = channel, Id = id };

            return SendInformativeMessage<TLMessagesAffectedMessages>("channels.deleteMessages", obj);
        }

        public Task<MTProtoResponse<bool>> EditChatAdminAsync(TLInputChannelBase channel, TLInputUserBase userId, TLChannelParticipantRoleBase role)
        {
            var obj = new TLChannelsEditAdmin { Channel = channel, UserId = userId, Role = role };

            return SendInformativeMessage<bool>("channels.editAdmin", obj);
        }
    }
}
