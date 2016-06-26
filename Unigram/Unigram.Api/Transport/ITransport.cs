using System;
using System.Collections.Generic;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace Telegram.Api.Transport
{
    public interface ITransport
    {
        event EventHandler Connecting;

        event EventHandler Connected;

        event EventHandler ConnectionLost;

        event EventHandler<DataEventArgs> PacketReceived;

        DateTime? FirstSendTime
        {
            get;
        }

        DateTime? LastReceiveTime
        {
            get;
        }

        object SyncRoot
        {
            get;
        }

        int Id
        {
            get;
        }

        int DCId
        {
            get;
            set;
        }

        byte[] AuthKey
        {
            get;
            set;
        }

        long? SessionId
        {
            get;
            set;
        }

        long? Salt
        {
            get;
            set;
        }

        int SequenceNumber
        {
            get;
            set;
        }

        long ClientTicksDelta
        {
            get;
            set;
        }

        string Host
        {
            get;
        }

        int Port
        {
            get;
        }

        TransportType Type
        {
            get;
        }

        bool Initiated
        {
            get;
            set;
        }

        bool Initialized
        {
            get;
            set;
        }

        bool IsInitializing
        {
            get;
            set;
        }

        bool IsAuthorized
        {
            get;
            set;
        }

        bool IsAuthorizing
        {
            get;
            set;
        }

        bool Closed
        {
            get;
        }

        void SendPacketAsync(string caption, byte[] data, Action<bool> callback, Action<TcpTransportResult> faultCallback = null);

        void Close();

        Tuple<int, int, int> GetCurrentPacketInfo();

        string GetTransportInfo();

        string PrintNonEncryptedHistory();

        long GenerateMessageId(bool checkPreviousMessageId = false);

        void EnqueueNonEncryptedItem(HistoryItem item);

        HistoryItem DequeueFirstNonEncryptedItem();

        bool RemoveNonEncryptedItem(HistoryItem item);

        void ClearNonEncryptedHistory(Exception e = null);

        IList<HistoryItem> RemoveTimeOutRequests(double timeout = 25.0);

        void UpdateTicksDelta(long? msgId);
    }
}
