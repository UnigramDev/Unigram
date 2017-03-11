using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using TransportType = Telegram.Api.Services.TransportType;

namespace Telegram.Api.Transport
{
    public abstract class TcpTransportBase : ITransport
    {
        public bool Additional { get; set; }

        public string Host { get; protected set; }
        public int Port { get; protected set; }
        public virtual TransportType Type { get { return TransportType.Tcp; } }

        protected TcpTransportBase(string host, int port)
        {
            Host = host;
            Port = port;

            var random = new Random();
            Id = random.Next(0, 255);
        }

        public bool Initiated { get; set; }
        public bool Initialized { get; set; }
        public bool IsInitializing { get; set; }
        public bool IsAuthorized { get; set; }
        public bool IsAuthorizing { get; set; }
        public bool Closed { get; protected set; }

        private readonly object _syncRoot = new object();
        public object SyncRoot { get { return _syncRoot; } }

        public int Id { get; protected set; }
        public int DCId { get; set; }
        public byte[] AuthKey { get; set; }
        public long? SessionId { get; set; }
        public long? Salt { get; set; }
        public int SequenceNumber { get; set; }

        private long _clientTicksDelta;
        public long ClientTicksDelta 
        { 
            get 
            { 
                return _clientTicksDelta; 
            } 
            set 
            { 
                _clientTicksDelta = value; 
            }
        }

        private bool _once;

        public void UpdateTicksDelta(long? msgId)
        {
            if (_once) return;  // to avoid lock

            lock (SyncRoot)
            {
                if (_once) return;
                _once = true;

                var clientTime = GenerateMessageId();
                var serverTime = msgId.Value;
                ClientTicksDelta += serverTime - clientTime;
            }
            //Execute.ShowDebugMessage("ITransport.UpdateTicksDelta dc_id=" + DCId);
        }

        public abstract void SendPacketAsync(string caption, byte[] data, Action<bool> callback, Action<TcpTransportResult> faultCallback = null);
        public abstract void Close();

        public int PacketLength { get { return _packetLength; } }

        private int _lastPacketLength;

        public int LastPacketLength { get { return _lastPacketLength; } }


        public Tuple<int, int, int> GetCurrentPacketInfo()
        {
            return new Tuple<int, int, int>(_packetLengthBytesRead, _packetLength, _bytesReceived);
        }

        public abstract string GetTransportInfo();

        public DateTime? FirstSendTime { get; protected set; }
        public DateTime? FirstReceiveTime { get; protected set; }
        public DateTime? LastReceiveTime { get; protected set; }

        protected static byte[] CreatePacket(byte[] buffer)
        {
            const int maxShortLength = 0x7E;
            var shortLength = buffer.Length / 4;
            var length = (shortLength > maxShortLength) ? 4 + buffer.Length : 1 + buffer.Length;
            var bytes = new byte[length];

            if (shortLength > maxShortLength)
            {
                bytes[0] = 0x7F;
                var shortLengthBytes = BitConverter.GetBytes(shortLength);
                Array.Copy(shortLengthBytes, 0, bytes, 1, 3);
                Array.Copy(buffer, 0, bytes, 4, buffer.Length);
            }
            else
            {
                bytes[0] = (byte)shortLength;
                Array.Copy(buffer, 0, bytes, 1, buffer.Length);
            }

            return bytes;
        }

        protected static int GetPacketLength(byte[] bytes, int position, out int bytesRead)
        {
            if (bytes.Length <= position)
            {
                if (bytes.Length != 0 && position != 0)
                {
                    Execute.ShowDebugMessage("TCPTransport.0x7F l<=p p=" + position + " l=" + bytes.Length);
                }
                bytesRead = 0;
                return 0;
            }

            int shortLength;
            if (bytes[position] == 0x7F)
            {
                if (bytes.Length < (position + 1 + 3))
                {
                    Execute.ShowDebugMessage("TCPTransport.0x7F error p=" + position + " l=" + bytes.Length);
                }

                var lengthBytes = bytes.SubArray(1 + position, 3);

                shortLength = BitConverter.ToInt32(lengthBytes.Concat(new byte[] { 0x00 }).ToArray(), 0);
                bytesRead = 4;
            }
            else
            {
                //Execute.ShowDebugMessage("TCPTransport.!=0x7F " + position);

                shortLength = bytes[position];
                bytesRead = 1;
            }

            return shortLength * 4;
        }

        protected static byte[] GetInitBuffer()
        {
            var buffer = new byte[64];
            var random = new Random();
            random.NextBytes(buffer);
            while (buffer[0] == 0x44414548
                   || buffer[0] == 0x54534f50
                   || buffer[0] == 0x20544547
                   || buffer[0] == 0x4954504f
                   || buffer[0] == 0xeeeeeeee
                   || buffer[0] == 0xef)
            {
                buffer[0] = (byte)random.Next();
            }
            while (buffer[1] == 0x00000000)
            {
                buffer[1] = (byte)random.Next();
            }

            return buffer;
        }

        private int _bytesReceived;
        private int _packetLength = 0;
        private byte[] _previousTail = new byte[0];
        private bool _usePreviousTail;
        private int _packetLengthBytesRead = 0;
        readonly MemoryStream _stream = new MemoryStream();
        protected void OnBufferReceived(byte[] buffer, int offset, int bytesTransferred)
        {
            if (bytesTransferred > 0)
            {
                _bytesReceived += bytesTransferred;

                if (_packetLength == 0)
                {
                    byte[] fullBuffer;

                    if (_usePreviousTail)
                    {
                        _usePreviousTail = false;
                        fullBuffer = TLUtils.Combine(_previousTail, buffer);
                        _previousTail = new byte[0];
                    }
                    else
                    {
                        fullBuffer = buffer;
                    }

                    _packetLength = GetPacketLength(fullBuffer, offset, out _packetLengthBytesRead);
                }

                _stream.Write(buffer, offset, bytesTransferred);

                if (_bytesReceived >= _packetLength + _packetLengthBytesRead)
                {
                    var bytes = _stream.ToArray();

                    var data = bytes.SubArray(_packetLengthBytesRead, _packetLength);
                    _previousTail = new byte[] { };
                    if (_bytesReceived > _packetLength + _packetLengthBytesRead)
                    {
                        _previousTail = bytes.SubArray(_packetLengthBytesRead + _packetLength, _bytesReceived - (_packetLengthBytesRead + _packetLength));
                    }

                    _stream.SetLength(0);
                    _stream.Write(_previousTail, 0, _previousTail.Length);
                    _bytesReceived = _previousTail.Length;

                    if (_previousTail.Length > 0)
                    {
                        if (_previousTail.Length >= 4)
                        {
                            _packetLength = GetPacketLength(_previousTail, 0, out _packetLengthBytesRead);

                            if (_packetLength != 0
                                && _previousTail.Length >= _packetLength + _packetLengthBytesRead)
                            {
                                Execute.ShowDebugMessage("TCPTransport.0x7F forgot package length=" + _packetLength + " tail=" + _previousTail.Length);
                            }
                        }
                        else
                        {
                            _packetLengthBytesRead = 0;
                            _packetLength = 0;
                            _usePreviousTail = true;
                        }
                    }
                    else
                    {
                        _packetLength = GetPacketLength(_previousTail, 0, out _packetLengthBytesRead);
                    }
                    _lastPacketLength = data.Length;
                    RaisePacketReceived(new DataEventArgs(data, PacketLength, LastReceiveTime));
                }
            }
            else
            {
                Execute.ShowDebugMessage("TCP bytesTransferred=" + bytesTransferred);
            }
        }

        #region MessageId
        private readonly object _previousMessageRoot = new object();

        public static long PreviousMessageId;

        public long GenerateMessageId(bool checkPreviousMessageId = false)
        {
            var clientDelta = ClientTicksDelta;
            // serverTime = clientTime + clientDelta
            var now = DateTime.Now;
            //var unixTime = (long)Utils.DateTimeToUnixTimestamp(now) << 32;

            var unixTime = (long)(Utils.DateTimeToUnixTimestamp(now) * 4294967296) + clientDelta; //2^32
            long correctUnixTime;

            var addingTicks = 4 - (unixTime % 4);
            if ((unixTime % 4) == 0)
            {
                correctUnixTime = unixTime;
            }
            else
            {
                correctUnixTime = unixTime + addingTicks;
            }

            // check with previous messageId

            lock (_previousMessageRoot)
            {
                if (PreviousMessageId != 0 && checkPreviousMessageId)
                {
                    correctUnixTime = Math.Max(PreviousMessageId + 4, correctUnixTime);
                }
                PreviousMessageId = correctUnixTime;
            }

            if (correctUnixTime == 0)
                throw new Exception("Bad message id");

            return correctUnixTime;
        }
        #endregion

        #region NonEncryptedHistory

        private readonly object _nonEncryptedHistoryRoot = new object();

        private readonly Dictionary<long, HistoryItem> _nonEncryptedHistory = new Dictionary<long, HistoryItem>();

        public void EnqueueNonEncryptedItem(HistoryItem item)
        {
            lock (_nonEncryptedHistoryRoot)
            {
                _nonEncryptedHistory[item.Hash] = item;
            }
#if LOG_REGISTRATION
            var info = new StringBuilder();
            info.AppendLine(String.Format("Socket.EnqueueNonEncryptedItem {0} item {1} hash={2}", Id, item.Caption, item.Hash));
            info.AppendLine("Items: " + _nonEncryptedHistory.Count);
            foreach (var historyItem in _nonEncryptedHistory.Values)
            {
                info.AppendLine(historyItem.Caption + " " + historyItem.Hash);
            }
            TLUtils.WriteLog(info.ToString());
#endif
        }

        public IList<HistoryItem> RemoveTimeOutRequests(double timeout = Constants.TimeoutInterval)
        {
            var now = DateTime.Now;
            var timedOutKeys = new List<long>();
            var timedOutValues = new List<HistoryItem>();

            lock (_nonEncryptedHistoryRoot)
            {
                foreach (var historyKeyValue in _nonEncryptedHistory)
                {
                    var historyValue = historyKeyValue.Value;
                    if (historyValue.SendTime != default(DateTime)
                        && historyValue.SendTime.AddSeconds(timeout) < now)
                    {
                        timedOutKeys.Add(historyKeyValue.Key);
                        timedOutValues.Add(historyKeyValue.Value);
                    }
                }

                if (timedOutKeys.Count > 0)
                {
#if LOG_REGISTRATION
                    var info = new StringBuilder();
                    info.AppendLine(String.Format("Socket.RemoveTimeOutRequests {0}", Id));
                    info.AppendLine("Items before: " + _nonEncryptedHistory.Count);
                    foreach (var historyItem in _nonEncryptedHistory.Values)
                    {
                        info.AppendLine(historyItem.Caption + " " + historyItem.Hash);
                    }
#endif
                    foreach (var key in timedOutKeys)
                    {
                        _nonEncryptedHistory.Remove(key);
                    }
#if LOG_REGISTRATION
                    info.AppendLine("Items after: " + _nonEncryptedHistory.Count);
                    foreach (var historyItem in _nonEncryptedHistory.Values)
                    {
                        info.AppendLine(historyItem.Caption + " " + historyItem.Hash);
                    }
                    TLUtils.WriteLog(info.ToString());
#endif
                }
            }

            return timedOutValues;
        }

        public HistoryItem DequeueFirstNonEncryptedItem()
        {
            HistoryItem item;
            lock (_nonEncryptedHistoryRoot)
            {
                item = _nonEncryptedHistory.Values.FirstOrDefault();
                if (item != null)
                {
                    _nonEncryptedHistory.Remove(item.Hash);
                }
            }

            return item;
        }

        public bool RemoveNonEncryptedItem(HistoryItem item)
        {
            bool result;
            lock (_nonEncryptedHistoryRoot)
            {
#if LOG_REGISTRATION
                var info = new StringBuilder();
                info.AppendLine(String.Format("Socket.RemoveNonEncryptedItem {0} item {1} hash={2}", Id, item.Caption, item.Hash));
                info.AppendLine("Items before: " + _nonEncryptedHistory.Count);
                foreach (var historyItem in _nonEncryptedHistory.Values)
                {
                    info.AppendLine(historyItem.Caption + " " + historyItem.Hash);
                }
#endif

                result = _nonEncryptedHistory.Remove(item.Hash);

#if LOG_REGISTRATION
                info.AppendLine("Items after: " + _nonEncryptedHistory.Count);
                foreach (var historyItem in _nonEncryptedHistory.Values)
                {
                    info.AppendLine(historyItem.Caption + " " + historyItem.Hash);
                }
                TLUtils.WriteLog(info.ToString());
#endif
            }

            return result;
        }

        public void ClearNonEncryptedHistory(Exception e = null)
        {
            lock (_nonEncryptedHistoryRoot)
            {
                var error = new StringBuilder();
                error.Append(String.Format("Socket.ClearNonEncryptedHistory {0} count={1}", Id, _nonEncryptedHistory.Count));
                if (e != null)
                {
                    error.AppendLine(e.ToString());
                }

#if LOG_REGISTRATION
                TLUtils.WriteLog(error.ToString());
#endif

                foreach (var historyItem in _nonEncryptedHistory)
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog(String.Format("Socket.ClearNonEncryptedHistory {0} item {1}", Id, historyItem.Value.Caption));
#endif
                    historyItem.Value.FaultCallback?.Invoke(new TLRPCError { ErrorCode = 404, ErrorMessage = error.ToString() });
                }

                _nonEncryptedHistory.Clear();
            }
        }

        public string PrintNonEncryptedHistory()
        {
            var sb = new StringBuilder();

            lock (_nonEncryptedHistoryRoot)
            {
                sb.AppendLine("NonEncryptedHistory items:");
                foreach (var historyItem in _nonEncryptedHistory.Values)
                {
                    sb.AppendLine(historyItem.Caption + " msgId " + historyItem.Hash);
                }
            }

            return sb.ToString();
        }
        #endregion

        #region Events
        public event EventHandler<DataEventArgs> PacketReceived;

        protected virtual void RaisePacketReceived(DataEventArgs args)
        {
            var handler = PacketReceived;
            if (handler != null)
            {
                Execute.BeginOnThreadPool(() => handler(this, args));
            }
        }

        public event EventHandler Connecting;

        protected virtual void RaiseConnectingAsync()
        {
            var handler = Connecting;
            if (handler != null)
            {
                Execute.BeginOnThreadPool(() => handler(this, EventArgs.Empty));
            }
        }

        public event EventHandler Connected;

        protected virtual void RaiseConnectedAsync()
        {
            var handler = Connected;
            if (handler != null)
            {
                Execute.BeginOnThreadPool(() => handler(this, EventArgs.Empty));
            }
        }

        public event EventHandler ConnectionLost;

        protected virtual void RaiseConnectionLost()
        {
            var handler = ConnectionLost;
            if (handler != null)
            {
                Execute.BeginOnThreadPool(() => handler(this, EventArgs.Empty));
            }
        }

        #endregion

        public override string ToString()
        {
            return String.Format("Id={0} {1}) {2}:{3} (AuthKey {4})\n  Salt {5}\n  SessionId {6} TicksDelta {7}", Id, DCId, Host, Port, AuthKey != null, Salt, SessionId, ClientTicksDelta);
        }

        protected virtual void WRITE_LOG(string str)
        {
#if LOG_REGISTRATION
            TLUtils.WriteLog(str);
#endif
        }

        protected virtual void WRITE_LOG(string str, Exception ex)
        {
            var type = ex != null ? ex.GetType().Name : "null";
            WRITE_LOG(String.Format("{0} {1} {2}={3}", str, Id, type, ex));
        }
    }
}
