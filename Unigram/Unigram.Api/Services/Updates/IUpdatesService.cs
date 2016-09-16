using System;
using System.Collections.Generic;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Updates
{
    public delegate void GetDifferenceAction(int? pts, int? date, int? qts, Action<TLDifferenceBase> callback, Action<TLRPCError> faultCallback);
    public delegate void GetDHConfigAction(int? version, int? randomLength, Action<TLDHConfigBase> callback, Action<TLRPCError> faultCallback);
    public delegate void AcceptEncryptionAction(TLInputEncryptedChat peer, string gb, long? keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback);
    public delegate void SendEncryptedServiceAction(TLInputEncryptedChat peer, long? randomkId, string data, Action<TLSentEncryptedMessage> callback, Action<TLRPCError> faultCallback);
    public delegate void UpdateChannelAction(int? channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback);
    public delegate void GetParticipantAction(TLInputChannelBase channelId, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback);
    public delegate void GetFullChatAction(int? chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback);
    public delegate void GetFullUserAction(TLInputUserBase userId, Action<TLUserFull> callback, Action<TLRPCError> faultCallback);
    public delegate void GetChannelMessagesAction(TLInputChannelBase channelId, TLVector<int> id, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback);

    public delegate void SetMessageOnTimeAtion(double seconds, string message);

    public interface IUpdatesService
    {
        void CancelUpdating();

        IList<ExceptionInfo> SyncDifferenceExceptions { get; }
        //void IncrementClientSeq();

        Func<int> GetCurrentUserId { get; set; }

        Action<Action<TLState>, Action<TLRPCError>> GetStateAsync { get; set; }
        GetDHConfigAction GetDHConfigAsync { get; set; }
        GetDifferenceAction GetDifferenceAsync { get; set; }
        AcceptEncryptionAction AcceptEncryptionAsync { get; set; }
        SendEncryptedServiceAction SendEncryptedServiceAsync { get; set; }
        SetMessageOnTimeAtion SetMessageOnTimeAsync { get; set; }
        Action<long> RemoveFromQueue { get; set; }
        UpdateChannelAction UpdateChannelAsync { get; set; }
        GetParticipantAction GetParticipantAsync { get; set; }
        GetFullChatAction GetFullChatAsync { get; set; }
        GetFullUserAction GetFullUserAsync { get; set; }
        GetChannelMessagesAction GetChannelMessagesAsync { get; set; }

        void SetInitState();

        int? ClientSeq { get; }
        void SetState(int? seq, int? pts, int? qts, int? date, int? unreadCount, string caption, bool cleanupMissingCounts = false);
        void SetState(ITLMultiPts multiPts, string caption);
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
