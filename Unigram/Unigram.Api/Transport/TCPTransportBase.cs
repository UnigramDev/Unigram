using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace Telegram.Api.Transport
{
    public abstract class TcpTransportBase : ITransport
    {
        private readonly object _syncRoot = new object();

        private bool _once;

        private int _bytesReceived;

        private int _packetLength;

        private byte[] _previousTail = new byte[0];

        private bool _usePreviousTail;

        private int _packetLengthBytesRead;

        private readonly MemoryStream _stream = new MemoryStream();

        private readonly object _previousMessageRoot = new object();

        public long PreviousMessageId;

        private readonly object _nonEncryptedHistoryRoot = new object();

        private readonly Dictionary<long, HistoryItem> _nonEncryptedHistory = new Dictionary<long, HistoryItem>();

        public event EventHandler<DataEventArgs> PacketReceived;

        public event EventHandler Connecting;

        public event EventHandler Connected;

        public event EventHandler ConnectionLost;

        public string Host
        {
            get;
            protected set;
        }

        public int Port
        {
            get;
            protected set;
        }

        public virtual TransportType Type
        {
            get
            {
                return TransportType.Tcp;
            }
        }

        public bool Initiated
        {
            get;
            set;
        }

        public bool Initialized
        {
            get;
            set;
        }

        public bool IsInitializing
        {
            get;
            set;
        }

        public bool IsAuthorized
        {
            get;
            set;
        }

        public bool IsAuthorizing
        {
            get;
            set;
        }

        public bool Closed
        {
            get;
            protected set;
        }

        public object SyncRoot
        {
            get
            {
                return _syncRoot;
            }
        }

        public int Id
        {
            get;
            protected set;
        }

        public int DCId
        {
            get;
            set;
        }

        public byte[] AuthKey
        {
            get;
            set;
        }

        public long? SessionId
        {
            get;
            set;
        }

        public long? Salt
        {
            get;
            set;
        }

        public int SequenceNumber
        {
            get;
            set;
        }

        public long ClientTicksDelta
        {
            get;
            set;
        }

        public DateTime? FirstSendTime
        {
            get;
            protected set;
        }

        public DateTime? FirstReceiveTime
        {
            get;
            protected set;
        }

        public DateTime? LastReceiveTime
        {
            get;
            protected set;
        }

        protected TcpTransportBase(string host, int port)
        {
            Host = host;
            Port = port;
            Random random = new Random();
            Id = random.Next(0, 255);
        }

        public void UpdateTicksDelta(long? msgId)
        {
            if (_once)
            {
                return;
            }
            lock (SyncRoot)
            {
                if (!_once)
                {
                    _once = true;
                    long value = GenerateMessageId(false);
                    long value2 = msgId.Value;
                    ClientTicksDelta += value2 - value;
                }
            }
        }

        public abstract void SendPacketAsync(string caption, byte[] data, Action<bool> callback, Action<TcpTransportResult> faultCallback = null);

        public abstract void Close();

        public Tuple<int, int, int> GetCurrentPacketInfo()
        {
            return new Tuple<int, int, int>(_packetLengthBytesRead, _packetLength, _bytesReceived);
        }

        public abstract string GetTransportInfo();

        protected static byte[] CreatePacket(byte[] buffer)
        {
            int num = buffer.Length / 4;
            int num2 = (num > 126) ? (4 + buffer.Length) : (1 + buffer.Length);
            byte[] array = new byte[num2];
            if (num > 126)
            {
                array[0] = 127;
                byte[] bytes = BitConverter.GetBytes(num);
                Array.Copy(bytes, 0, array, 1, 3);
                Array.Copy(buffer, 0, array, 4, buffer.Length);
            }
            else
            {
                array[0] = (byte)num;
                Array.Copy(buffer, 0, array, 1, buffer.Length);
            }
            return array;
        }

        protected static int GetPacketLength(byte[] bytes, int position, out int bytesRead)
        {
            if (bytes.Length <= position)
            {
                if (bytes.Length != 0 && position != 0)
                {
                    Execute.ShowDebugMessage(string.Concat(new object[]
                    {
                        "TCPTransport.0x7F l<=p p=",
                        position,
                        " l=",
                        bytes.Length
                    }));
                }
                bytesRead = 0;
                return 0;
            }
            int num;
            if (bytes[position] == 127)
            {
                if (bytes.Length < position + 1 + 3)
                {
                    Execute.ShowDebugMessage(string.Concat(new object[]
                    {
                        "TCPTransport.0x7F error p=",
                        position,
                        " l=",
                        bytes.Length
                    }));
                }
                byte[] array = bytes.SubArray(1 + position, 3);
                IEnumerable<byte> arg_A6_0 = array;
                byte[] second = new byte[1];
                num = BitConverter.ToInt32(arg_A6_0.Concat(second).ToArray<byte>(), 0);
                bytesRead = 4;
            }
            else
            {
                num = (int)bytes[position];
                bytesRead = 1;
            }
            return num * 4;
        }

        protected void OnBufferReceived(byte[] buffer, int offset, int bytesTransferred)
        {
            if (bytesTransferred > 0)
            {
                _bytesReceived += bytesTransferred;
                if (_packetLength == 0)
                {
                    byte[] bytes;
                    if (_usePreviousTail)
                    {
                        _usePreviousTail = false;
                        bytes = TLUtils.Combine(new byte[][]
                        {
                            _previousTail,
                            buffer
                        });
                        _previousTail = new byte[0];
                    }
                    else
                    {
                        bytes = buffer;
                    }
                    _packetLength = TcpTransportBase.GetPacketLength(bytes, offset, out _packetLengthBytesRead);
                }
                _stream.Write(buffer, offset, bytesTransferred);
                if (_bytesReceived >= _packetLength + _packetLengthBytesRead)
                {
                    byte[] data = _stream.ToArray();
                    byte[] data2 = data.SubArray(_packetLengthBytesRead, _packetLength);
                    _previousTail = new byte[0];
                    if (_bytesReceived > _packetLength + _packetLengthBytesRead)
                    {
                        _previousTail = data.SubArray(_packetLengthBytesRead + _packetLength, _bytesReceived - (_packetLengthBytesRead + _packetLength));
                    }
                    _stream.SetLength(0L);
                    _stream.Write(_previousTail, 0, _previousTail.Length);
                    _bytesReceived = _previousTail.Length;
                    if (_previousTail.Length > 0)
                    {
                        if (_previousTail.Length >= 4)
                        {
                            _packetLength = TcpTransportBase.GetPacketLength(_previousTail, 0, out _packetLengthBytesRead);
                            if (_packetLength != 0 && _previousTail.Length >= _packetLength + _packetLengthBytesRead)
                            {
                                Execute.ShowDebugMessage(string.Concat(new object[]
                                {
                                    "TCPTransport.0x7F forgot package length=",
                                    _packetLength,
                                    " tail=",
                                    _previousTail.Length
                                }));
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
                        _packetLength = TcpTransportBase.GetPacketLength(_previousTail, 0, out _packetLengthBytesRead);
                    }
                    RaisePacketReceived(new DataEventArgs(data2));
                    return;
                }
            }
            else
            {
                Execute.ShowDebugMessage("TCP bytesTransferred=" + bytesTransferred);
            }
        }

        public long GenerateMessageId(bool checkPreviousMessageId = false)
        {
            long clientTicksDelta = ClientTicksDelta;
            DateTime now = DateTime.Now;
            long num = (long)(Utils.DateTimeToUnixTimestamp(now) * 4294967296.0) + clientTicksDelta;
            long num2 = 4L - num % 4L;
            long num3;
            if (num % 4L == 0L)
            {
                num3 = num;
            }
            else
            {
                num3 = num + num2;
            }
            lock (_previousMessageRoot)
            {
                if (PreviousMessageId != 0L && checkPreviousMessageId)
                {
                    num3 = Math.Max(PreviousMessageId + 4L, num3);
                }
                PreviousMessageId = num3;
            }
            if (num3 == 0L)
            {
                throw new Exception("Bad message id");
            }
            return num3;
        }

        public void EnqueueNonEncryptedItem(HistoryItem item)
        {
            lock (_nonEncryptedHistoryRoot)
            {
                _nonEncryptedHistory[item.Hash] = item;
            }
        }

        public IList<HistoryItem> RemoveTimeOutRequests(double timeout = 25.0)
        {
            DateTime now = DateTime.Now;
            List<long> list = new List<long>();
            List<HistoryItem> list2 = new List<HistoryItem>();
            lock (_nonEncryptedHistoryRoot)
            {
                foreach (KeyValuePair<long, HistoryItem> current in _nonEncryptedHistory)
                {
                    HistoryItem value = current.Value;
                    if (value.SendTime != default(DateTime) && value.SendTime.AddSeconds(timeout) < now)
                    {
                        list.Add(current.Key);
                        list2.Add(current.Value);
                    }
                }
                if (list.Count > 0)
                {
                    foreach (long current2 in list)
                    {
                        _nonEncryptedHistory.Remove(current2);
                    }
                }
            }
            return list2;
        }

        public HistoryItem DequeueFirstNonEncryptedItem()
        {
            HistoryItem historyItem;
            lock (_nonEncryptedHistoryRoot)
            {
                historyItem = _nonEncryptedHistory.Values.FirstOrDefault<HistoryItem>();
                if (historyItem != null)
                {
                    _nonEncryptedHistory.Remove(historyItem.Hash);
                }
            }
            return historyItem;
        }

        public bool RemoveNonEncryptedItem(HistoryItem item)
        {
            bool result;
            lock (_nonEncryptedHistoryRoot)
            {
                result = _nonEncryptedHistory.Remove(item.Hash);
            }
            return result;
        }

        public void ClearNonEncryptedHistory(Exception e = null)
        {
            lock (_nonEncryptedHistoryRoot)
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(string.Format("Socket.ClearNonEncryptedHistory {0} count={1}", new object[]
                {
                    Id,
                    _nonEncryptedHistory.Count
                }));
                if (e != null)
                {
                    stringBuilder.AppendLine(e.ToString());
                }
                foreach (KeyValuePair<long, HistoryItem> current in _nonEncryptedHistory)
                {
                    current.Value.FaultCallback.SafeInvoke(new TLRPCError
                    {
                        ErrorCode = 404,
                        ErrorMessage = stringBuilder.ToString()
                    });
                }
                _nonEncryptedHistory.Clear();
            }
        }

        public string PrintNonEncryptedHistory()
        {
            StringBuilder stringBuilder = new StringBuilder();
            lock (_nonEncryptedHistoryRoot)
            {
                stringBuilder.AppendLine("NonEncryptedHistory items:");
                foreach (HistoryItem current in _nonEncryptedHistory.Values)
                {
                    stringBuilder.AppendLine(current.Caption + " msgId " + current.Hash);
                }
            }
            return stringBuilder.ToString();
        }

        protected virtual void RaisePacketReceived(DataEventArgs args)
        {
            EventHandler<DataEventArgs> handler = PacketReceived;
            if (handler != null)
            {
                Execute.BeginOnThreadPool(delegate
                {
                    handler.Invoke(this, args);
                });
            }
        }

        protected virtual void RaiseConnectingAsync()
        {
            EventHandler handler = Connecting;
            if (handler != null)
            {
                Execute.BeginOnThreadPool(delegate
                {
                    handler.Invoke(this, EventArgs.Empty);
                });
            }
        }

        protected virtual void RaiseConnectedAsync()
        {
            EventHandler handler = Connected;
            if (handler != null)
            {
                Execute.BeginOnThreadPool(delegate
                {
                    handler.Invoke(this, EventArgs.Empty);
                });
            }
        }

        protected virtual void RaiseConnectionLost()
        {
            EventHandler handler = ConnectionLost;
            if (handler != null)
            {
                Execute.BeginOnThreadPool(delegate
                {
                    handler.Invoke(this, EventArgs.Empty);
                });
            }
        }

        public override string ToString()
        {
            return string.Format("Id={0} {1}) {2}:{3} (AuthKey {4})\n  Salt {5}\n  SessionId {6} TicksDelta {7}", new object[]
            {
                Id,
                DCId,
                Host,
                Port,
                AuthKey != null,
                Salt,
                SessionId,
                ClientTicksDelta
            });
        }

        protected virtual void WRITE_LOG(string str)
        {
        }

        protected virtual void WRITE_LOG(string str, Exception ex)
        {
            string text = (ex != null) ? ex.GetType().Name : "null";
            WRITE_LOG(string.Format("{0} {1} {2}={3}", new object[]
            {
                str,
                Id,
                text,
                ex
            }));
        }
    }
}
