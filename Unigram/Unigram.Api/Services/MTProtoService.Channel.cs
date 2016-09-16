using System;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Channels;
using Telegram.Api.TL.Functions.Updates;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void GetAdminedPublicChannelsAsync(Action<TLChatsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetAdminedPublicChannels();

            SendInformativeMessage<TLChatsBase>("updates.getAdminedPublicChannels", obj,
                result =>
                {
                    var chats = result as TLChats24;
                    if (chats != null)
                    {
                        _cacheService.SyncUsersAndChats(new TLVector<TLUserBase>(), chats.Chats, tuple => callback.SafeInvoke(result));
                    }
                },
                faultCallback);
        }

        public void GetChannelDifferenceAsync(TLInputChannelBase inputChannel, TLChannelMessagesFilerBase filter, TLInt pts, TLInt limit, Action<TLChannelDifferenceBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetChannelDifference { Channel = inputChannel, Filter = filter, Pts = pts, Limit = limit };

            SendInformativeMessage("updates.getChannelDifference", obj, callback, faultCallback);
        }

        public void GetMessagesAsync(TLInputChannelBase inputChannel, TLVector<TLInt> id, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetMessages { Channel = inputChannel, Id = id };

            SendInformativeMessage("channels.getMessages", obj, callback, faultCallback);
        }

        public void EditAdminAsync(TLChannel channel, TLInputUserBase userId, TLChannelParticipantRoleBase role, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLEditAdmin { Channel = channel.ToInputChannel(), UserId = userId, Role = role };

            const string caption = "channels.editAdmin";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    GetFullChannelAsync(channel.ToInputChannel(),
                        messagesChatFull => callback.SafeInvoke(result),
                        faultCallback.SafeInvoke);
                },
                faultCallback);
        }


        public void GetParticipantAsync(TLInputChannelBase inputChannel, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetParticipant { Channel = inputChannel, UserId = userId };

            const string caption = "channels.getParticipant";
            SendInformativeMessage<TLChannelsChannelParticipant>(caption, obj, result =>
            {
                _cacheService.SyncUsers(result.Users, r => { });

                callback.SafeInvoke(result);
            }, 
            faultCallback);
        }

        public void GetParticipantsAsync(TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, TLInt offset, TLInt limit, Action<TLChannelsChannelParticipants> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetParticipants { Channel = inputChannel, Filter = filter, Offset = offset, Limit = limit };

            const string caption = "channels.getParticipants";
            SendInformativeMessage<TLChannelsChannelParticipants>(caption, obj,
                result =>
                {
                    for (var i = 0; i < result.Users.Count; i++)
                    {
                        var cachedUser = _cacheService.GetUser(result.Users[i].Id);
                        if (cachedUser != null)
                        {
                            cachedUser._status = result.Users[i].Status;
                            result.Users[i] = cachedUser;
                        }
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void EditTitleAsync(TLChannel channel, TLString title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLEditTitle { Channel = channel.ToInputChannel(), Title = title };

            const string caption = "channels.editTitle";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void EditAboutAsync(TLChannel channel, TLString about, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLEditAbout { Channel = channel.ToInputChannel(), About = about };

            const string caption = "channels.editAbout";
            SendInformativeMessage<TLBool>(caption, obj, callback.SafeInvoke, faultCallback);
        }

        public void JoinChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLJoinChannel { Channel = channel.ToInputChannel() };

            const string caption = "channels.joinChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    channel.Left = TLBool.False;
                    if (channel.ParticipantsCount != null)
                    {
                        channel.ParticipantsCount = new TLInt(channel.ParticipantsCount.Value + 1);
                    }
                    _cacheService.Commit();

                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void LeaveChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLLeaveChannel { Channel = channel.ToInputChannel() };

            const string caption = "channels.leaveChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    channel.Left = TLBool.True;
                    if (channel.ParticipantsCount != null)
                    {
                        channel.ParticipantsCount = new TLInt(channel.ParticipantsCount.Value - 1);
                    }
                    _cacheService.Commit();

                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void KickFromChannelAsync(TLChannel channel, TLInputUserBase userId, TLBool kicked, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLKickFromChannel { Channel = channel.ToInputChannel(), UserId = userId, Kicked = kicked };

            const string caption = "channels.kickFromChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    GetFullChannelAsync(channel.ToInputChannel(),
                        messagesChatFull => callback.SafeInvoke(result),
                        faultCallback.SafeInvoke);
                },
                faultCallback);
        }

        public void DeleteChannelAsync(TLChannel channel, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLDeleteChannel { Channel = channel.ToInputChannel() };

            const string caption = "channels.deleteChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void InviteToChannelAsync(TLInputChannelBase channel, TLVector<TLInputUserBase> users, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLInviteToChannel { Channel = channel, Users = users };

            const string caption = "channels.inviteToChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {


                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void DeleteMessagesAsync(TLInputChannelBase channel, TLVector<TLInt> id, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLDeleteChannelMessages { Channel = channel, Id = id };

            const string caption = "channels.deleteMessages";
            SendInformativeMessage<TLAffectedMessages>(caption, obj,
                result =>
                {
                    //var multiPts = result as IMultiPts;
                    //if (multiPts != null)
                    //{
                    //    _updatesService.SetState(multiPts, caption);
                    //}
                    //else
                    //{
                    //    _updatesService.SetState(null, result.Pts, null, null, null, caption);
                    //}

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void UpdateChannelAsync(TLInt channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null)
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
            var obj = new TLGetFullChannel { Channel = channel };

            SendInformativeMessage<TLMessagesChatFull>(
                "cnannels.getFullChannel", obj,
                messagesChatFull =>
                {
                    _cacheService.SyncChat(messagesChatFull, result => callback.SafeInvoke(messagesChatFull));
                },
                faultCallback);
        }

        public void GetImportantHistoryAsync(TLInputChannelBase channel, TLPeerBase peer, bool sync, TLInt offsetId, TLInt addOffset, TLInt limit, TLInt maxId, TLInt minId, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetImportantHistory { Channel = channel, OffsetId = offsetId, OffsetDate = new TLInt(0), AddOffset = addOffset, Limit = limit, MaxId = maxId, MinId = minId };

            SendInformativeMessage("channels.getImportantHistory", obj, callback, faultCallback);
        }

        public void ReadHistoryAsync(TLChannel channel, TLInt maxId, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLReadChannelHistory { Channel = channel.ToInputChannel(), MaxId = maxId };

            SendInformativeMessage<TLBool>("channels.readHistory", obj,
                result =>
                {
                    channel.ReadInboxMaxId = maxId;

                    _cacheService.Commit();

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void CreateChannelAsync(TLInt flags, TLString title, TLString about, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLCreateChannel { Flags = flags, Title = title, About = about };

            var caption = "channels.createChannel";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                }, 
                faultCallback);
        }

        public void ExportInviteAsync(TLInputChannelBase channel, Action<TLExportedChatInvite> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLExportInvite { Channel = channel };

            SendInformativeMessage("channels.exportInvite", obj, callback, faultCallback);
        }

        public void CheckUsernameAsync(TLInputChannelBase channel, TLString username, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLCheckUsername { Channel = channel, Username = username };

            SendInformativeMessage("channels.checkUsername", obj, callback, faultCallback);
        }

        public void UpdateUsernameAsync(TLInputChannelBase channel, TLString username, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUpdateUsername { Channel = channel, Username = username };

            SendInformativeMessage("channels.updateUsername", obj, callback, faultCallback);
        }

        public void EditPhotoAsync(TLChannel channel, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLEditPhoto { Channel = channel.ToInputChannel(), Photo = photo };

            const string caption = "channels.editPhoto";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null, true);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void DeleteChannelMessagesAsync(TLInputChannelBase channel, TLVector<TLInt> id, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLDeleteChannelMessages { Channel = channel, Id = id };

            SendInformativeMessage("channels.deleteMessages", obj, callback, faultCallback);
        }

        public void EditChatAdminAsync(TLInputChannelBase channel, TLInputUserBase userId, TLChannelParticipantRoleBase role, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLEditAdmin { Channel = channel, UserId = userId, Role = role };

            SendInformativeMessage("channels.editAdmin", obj, callback, faultCallback);
        }

        public void ToggleInvitesAsync(TLInputChannelBase channel, TLBool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLToggleInvites { Channel = channel, Enabled = enabled };

            const string caption = "channels.toggleInvites";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void ExportMessageLinkAsync(TLInputChannelBase channel, TLInt id, Action<TLExportedMessageLink> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLExportMessageLink { Channel = channel, Id = id };

            SendInformativeMessage("channels.exportMessageLink", obj, callback, faultCallback);
        }

        public void UpdatePinnedMessageAsync(bool silent, TLInputChannelBase channel, TLInt id, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUpdatePinnedMessage { Flags = new TLInt(0), Channel = channel, Id = id };
            if (silent)
            {
                obj.SetSilent();
            }

            const string caption = "channels.updatePinnedMessage";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void ToggleSignaturesAsync(TLInputChannelBase channel, TLBool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLToggleSignatures { Channel = channel, Enabled = enabled };

            const string caption = "channels.toggleSignatures";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void GetMessageEditDataAsync(TLInputPeerBase peer, TLInt id, Action<TLMessageEditData> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetMessageEditData { Peer = peer, Id = id };

            SendInformativeMessage("messages.getMessageEditData", obj, callback, faultCallback);
        }


        public void EditMessageAsync(TLInputPeerBase peer, TLInt id, TLString message, TLVector<TLMessageEntityBase> entities, TLReplyKeyboardBase replyMarkup, TLBool noWebPage, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLEditMessage { Flags=new TLInt(0), Peer = peer, Id = id, Message = message, NoWebPage = noWebPage, Entities = entities, ReplyMarkup = replyMarkup };

            const string caption = "messages.editMessage";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null, true);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void ReportSpamAsync(TLInputChannelBase channel, TLInt userId, TLVector<TLInt> id, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLReportSpam { Channel = channel, UserId = userId, Id = id };

            const string caption = "channels.reportSpam";
            SendInformativeMessage<TLBool>(caption, obj, callback.SafeInvoke, faultCallback);
        }

        public void DeleteUserHistoryAsync(TLChannel channel, TLInputUserBase userId, Action<TLAffectedHistory> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLDeleteUserHistory { Channel = channel.ToInputChannel(), UserId = userId };

            const string caption = "channels.deleteUserHistory";
            SendInformativeMessage<TLAffectedHistory>(caption, obj,
                result =>
                {
                    var multiChannelPts = result as IMultiChannelPts;
                    if (multiChannelPts != null)
                    {
                        if (channel.Pts == null
                            || channel.Pts.Value + multiChannelPts.ChannelPtsCount.Value != multiChannelPts.ChannelPts.Value)
                        {
                            Execute.ShowDebugMessage(string.Format("channel_id={0} channel_pts={1} affectedHistory24[channel_pts={2} channel_pts_count={3}]", channel.Id, channel.Pts, multiChannelPts.ChannelPts, multiChannelPts.ChannelPtsCount));
                        }
                        channel.Pts = new TLInt(multiChannelPts.ChannelPts.Value);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }
    }
}
