using System;
using System.Collections.Generic;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace Telegram.Api.Transport
{
    public interface ITransport
    {
        MTProtoTransportType MTProtoType { get; }

        event EventHandler Connecting;
        event EventHandler Connected;
        event EventHandler ConnectionLost;
        event EventHandler<DataEventArgs> PacketReceived;

        void SendPacketAsync(string caption, byte[] data, Action<bool> callback, Action<TcpTransportResult> faultCallback = null);
        void Close();

        // check hang connection
        DateTime? FirstSendTime { get; }
        DateTime? LastReceiveTime { get; }
        int PacketLength { get; }
        int LastPacketLength { get; }

        // debug
        Tuple<int, int, int> GetCurrentPacketInfo();
        string GetTransportInfo();
        string PrintNonEncryptedHistory();

        long GenerateMessageId(bool checkPreviousMessageId = false);
        void EnqueueNonEncryptedItem(HistoryItem item);
        HistoryItem DequeueFirstNonEncryptedItem();
        bool RemoveNonEncryptedItem(HistoryItem item);
        void ClearNonEncryptedHistory(Exception e = null);
        IList<HistoryItem> RemoveTimeOutRequests(double timeout = Constants.TimeoutInterval);
        void UpdateTicksDelta(long? msgId);

        object SyncRoot { get; }
        int Id { get; }
        int DCId { get; set; }
        byte[] AuthKey { get; set; }

        long? SessionId { get; set; }
        long MinMessageId { get; set; }
        Dictionary<long, long> MessageIdDict { get; set; }

        long? Salt { get; set; }
        int SequenceNumber { get; set; }
        long ClientTicksDelta { get; set; }

        string Host { get; }
        int Port { get; }
        TLProxyConfig ProxyConfig { get; }

        TransportType Type { get; }

        //сделан initConnection
        bool Initiated { get; set; }
        //создан ключ
        bool Initialized { get; set; }
        bool IsInitializing { get; set; }
        //перенесена авторизация из активного dc (import/export authorization)
        bool IsAuthorized { get; set; }
        bool IsAuthorizing { get; set; }
        //вызван метод Close
        bool Closed { get; }
    }

    public enum MTProtoTransportType
    {
        Main,
        File,
        Special,
    }
}
