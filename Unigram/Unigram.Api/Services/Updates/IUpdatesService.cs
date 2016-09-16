using System;
using System.Collections.Generic;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Updates
{
    public delegate void GetDifferenceAction(TLInt pts, TLInt date, TLInt qts, Action<TLDifferenceBase> callback, Action<TLRPCError> faultCallback);
    public delegate void GetDHConfigAction(TLInt version, TLInt randomLength, Action<TLDHConfigBase> callback, Action<TLRPCError> faultCallback);
    public delegate void AcceptEncryptionAction(TLInputEncryptedChat peer, TLString gb, TLLong keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback);
    public delegate void SendEncryptedServiceAction(TLInputEncryptedChat peer, TLLong randomkId, TLString data, Action<TLSentEncryptedMessage> callback, Action<TLRPCError> faultCallback);
    public delegate void UpdateChannelAction(TLInt channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback);
    public delegate void GetParticipantAction(TLInputChannelBase channelId, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback);
    public delegate void GetFullChatAction(TLInt chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback);
    public delegate void GetFullUserAction(TLInputUserBase userId, Action<TLUserFull> callback, Action<TLRPCError> faultCallback);
    public delegate void GetChannelMessagesAction(TLInputChannelBase channelId, TLVector<TLInt> id, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback);

    public delegate void SetMessageOnTimeAtion(double seconds, string message);

    public interface IUpdatesService
    {
        void CancelUpdating();

        IList<ExceptionInfo> SyncDifferenceExceptions { get; }
        //void IncrementClientSeq();

        Func<TLInt> GetCurrentUserId { get; set; }

        Action<Action<TLState>, Action<TLRPCError>> GetStateAsync { get; set; }
        GetDHConfigAction GetDHConfigAsync { get; set; }
        GetDifferenceAction GetDifferenceAsync { get; set; }
        AcceptEncryptionAction AcceptEncryptionAsync { get; set; }
        SendEncryptedServiceAction SendEncryptedServiceAsync { get; set; }
        SetMessageOnTimeAtion SetMessageOnTimeAsync { get; set; }
        Action<TLLong> RemoveFromQueue { get; set; }
        UpdateChannelAction UpdateChannelAsync { get; set; }
        GetParticipantAction GetParticipantAsync { get; set; }
        GetFullChatAction GetFullChatAsync { get; set; }
        GetFullUserAction GetFullUserAsync { get; set; }
        GetChannelMessagesAction GetChannelMessagesAsync { get; set; }

        void SetInitState();

        TLInt ClientSeq { get; }
        void SetState(TLInt seq, TLInt pts, TLInt qts, TLInt date, TLInt unreadCount, string caption, bool cleanupMissingCounts = false);
        void SetState(IMultiPts multiPts, string caption);
        void ProcessTransportMessage(TLTransportMessage transportMessage);
        void ProcessUpdates(TLUpdatesBase updates, bool notifyNewMessages = false);
        
        void LoadStateAndUpdate(Action callback);
        void SaveState();
        TLState GetState();
        void ClearState();

        void SaveStateSnapshot(string toDirectoryName);
        void LoadStateSnapshot(string fromDirectoryName);

        event EventHandler<DCOptionsUpdatedEventArgs> DCOptionsUpdated;
    }
}
