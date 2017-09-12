using System;
using System.Collections.Generic;
using Telegram.Api.Native.TL;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Channels;
using Telegram.Api.TL.Messages;
using Telegram.Api.TL.Updates;

namespace Telegram.Api.Services.Updates
{
    public delegate void GetDifferenceAction(int pts, int date, int qts, Action<TLUpdatesDifferenceBase> callback, Action<TLRPCError> faultCallback = null);
    public delegate void GetDHConfigAction(int version, int randomLength, Action<TLMessagesDHConfig> callback, Action<TLRPCError> faultCallback = null);
    public delegate void AcceptEncryptionAction(TLInputEncryptedChat peer, byte[] gb, long keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null);
    public delegate void SendEncryptedServiceAction(TLInputEncryptedChat peer, long randomkId, byte[] data, Action<TLMessagesSentEncryptedMessage> callback, Action<TLRPCError> faultCallback = null);
    public delegate void UpdateChannelAction(int? channelId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
    public delegate void GetParticipantAction(TLInputChannelBase channelId, TLInputUserBase userId, Action<TLChannelsChannelParticipant> callback, Action<TLRPCError> faultCallback = null);
    public delegate void GetFullChatAction(int chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null);
    public delegate void GetFullUserAction(TLInputUserBase userId, Action<TLUserFull> callback, Action<TLRPCError> faultCallback = null);
    public delegate void GetChannelMessagesAction(TLInputChannelBase channelId, TLVector<int> id, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);
    public delegate void GetMessagesAction(TLVector<int> id, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null);

    public delegate void SetMessageOnTimeAtion(double seconds, string message);

    public interface IUpdatesService
    {
        void CancelUpdating();

        IList<ExceptionInfo> SyncDifferenceExceptions { get; }
        //void IncrementClientSeq();

        Func<int> GetCurrentUserId { get; set; }

        Action<Action<TLUpdatesState>, Action<TLRPCError>> GetStateAsync { get; set; }
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
        GetMessagesAction GetMessagesAsync { get; set; }

        void SetInitState();

        int? ClientSeq { get; }
        void SetState(int? seq, int? pts, int? qts, int? date, int? unreadCount, string caption, bool cleanupMissingCounts = false);
        void SetState(ITLMultiPts multiPts, string caption);
        //void ProcessTransportMessage(TLTransportMessage transportMessage);
        void ProcessUpdates(TLUpdatesBase updates, bool notifyNewMessages = false);
        
        void LoadStateAndUpdate(Action callback);
        void SaveState();
        TLUpdatesState GetState();
        void ClearState();

        void SaveStateSnapshot(string toDirectoryName);
        void LoadStateSnapshot(string fromDirectoryName);

        event EventHandler<DCOptionsUpdatedEventArgs> DCOptionsUpdated;
    }
}
