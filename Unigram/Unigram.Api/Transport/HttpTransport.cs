using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Action = System.Action;
using TransportType = Telegram.Api.Services.TransportType;

namespace Telegram.Api.Transport
{
    public class HttpTransport : ITransport
    {
        public bool Additional { get; set; }

        private bool _once;

        public void UpdateTicksDelta(TLLong msgId)
        {
            lock (SyncRoot)
            {
                if (_once) return;
                _once = true;

                var clientTime = GenerateMessageId().Value;
                var serverTime = msgId.Value;
                ClientTicksDelta += serverTime - clientTime;
            }
        }

        public event EventHandler ConnectionLost;

        public event EventHandler Connecting;

        public event EventHandler Connected;

        public DateTime? FirstReceiveTime
        {
            get { throw new NotImplementedException(); }
        }

        public DateTime? LastReceiveTime
        {
            get { throw new NotImplementedException(); }
        }

        public int PacketLength
        {
            get { throw new NotImplementedException(); }
        }

        public int LastPacketLength
        {
            get { throw new NotImplementedException(); }
        }

        public DateTime? FirstSendTime
        {
            get { throw new NotImplementedException(); }
        }

        public DateTime? LastSendTime
        {
            get { throw new NotImplementedException(); }
        }

        public WindowsPhone.Tuple<int, int, int> GetCurrentPacketInfo()
        {
            throw new NotImplementedException();
        }

        public string GetTransportInfo()
        {
            throw new NotImplementedException();
        }

        public int Id { get; protected set; }

        private readonly object _previousMessageRoot = new object();

        public long PreviousMessageId;

        public TLLong GenerateMessageId(bool checkPreviousMessageId = false)
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

            // refactor this:
            // addTicks = 4 - (unixTime % 4)
            // fixedUnixTime = unixTime + addTicks
            // max(fixedUnixTime, previousMessageId + 4)

            //if ((unixTime % 4) == 0)
            //{
            //    correctUnixTime = unixTime;
            //}
            //else
            //{
            //    for (int i = 0; i < 300; i++)
            //    {
            //        var temp = unixTime - i;
            //        if ((temp % 4) == 0)
            //        {
            //            correctUnixTime = unixTime;
            //            break;
            //        }
            //    }
            //}
            //TLUtils.WriteLine("TLMessage ID: " + correctUnixTime);
            //TLUtils.WriteLine("MessageId % 4 =" + (correctUnixTime % 4));
            //TLUtils.WriteLine("Corresponding time: " + Utils.UnixTimestampToDateTime(correctUnixTime >> 32));

            if (correctUnixTime == 0)
                throw new Exception("Bad message id");

            return new TLLong(correctUnixTime);
        }

        #region NonEncryptedHistory

        private readonly object _nonEncryptedHistoryRoot = new object();

        private readonly Dictionary<long, HistoryItem> _nonEncryptedHistory = new Dictionary<long, HistoryItem>();

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

                foreach (var key in timedOutKeys)
                {
                    _nonEncryptedHistory.Remove(key);
                }
            }

            return timedOutValues;
        }

        public void EnqueueNonEncryptedItem(HistoryItem item)
        {
            lock (_nonEncryptedHistoryRoot)
            {
                _nonEncryptedHistory[item.Hash] = item;
            }
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
                result = _nonEncryptedHistory.Remove(item.Hash);
            }

            return result;
        }

        public void ClearNonEncryptedHistory(Exception e = null)
        {
            lock (_nonEncryptedHistoryRoot)
            {
                foreach (var historyItem in _nonEncryptedHistory)
                {
                    var error = new StringBuilder();
                    error.AppendLine("Clear NonEncrypted History: ");
                    if (e != null)
                    {
                        error.AppendLine(e.ToString());
                    }
                    historyItem.Value.FaultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404), Message = new TLString(error.ToString()) });
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
                    sb.AppendLine(historyItem.Caption);
                }
            }

            return sb.ToString();
        }
        #endregion

        public void StartListening()
        {
            
        }
        private readonly object _syncRoot = new object();

        public object SyncRoot { get { return _syncRoot; } }

        public int DCId { get; set; }

        public byte[] AuthKey { get; set; }
        public TLLong SessionId { get; set; }
        public TLLong Salt { get; set; }
        public int SequenceNumber { get; set; }
        public long ClientTicksDelta { get; set; }

        public bool Initiated { get; set; }

        public bool Initialized { get; set; }

        public bool IsInitializing { get; set; }

        public bool IsAuthorized { get; set; }

        public bool IsAuthorizing { get; set; }

        public string Host { get { return _host; } }
        
        public int Port { get { return 80; } }
        
        public TransportType Type { get { return TransportType.Http; } }
        public bool Closed { get; private set; }

        private string _host;

        public HttpTransport(string address)
        {
            var host = string.IsNullOrEmpty(address) ? Constants.FirstServerIpAddress : address;
            _host = string.Format("http://{0}:80/api", host);
        }

        public void ConnectAsync(Action callback, Action<TcpTransportResult> faultCallback)
        {
            
        }

        public event EventHandler<DataEventArgs> PacketReceived;

        private void RaiseGetBytes(byte[] data)
        {
            var eventHandler = PacketReceived;
            if (eventHandler != null)
            {
                eventHandler.Invoke(this, new DataEventArgs(data));
            }
        }

        private static HttpWebRequest CreateRequest(int contentLength, string address)
        {
            var request = (HttpWebRequest)WebRequest.Create(address);
            request.Method = "POST";
#if SILVERLIGHT
            
            request.Headers["Connection"] = "Keep-alive";
            request.Headers["Content-Length"] = contentLength.ToString(CultureInfo.InvariantCulture);
#else
            request.KeepAlive = true;
            request.ContentLength = contentLength;
            ServicePointManager.Expect100Continue = false;
#endif
            //IWebProxy proxy = request.Proxy;
            //if (proxy != null)
            //{
            //    string proxyuri = proxy.GetProxy(request.RequestUri).ToString();
            //    request.UseDefaultCredentials = true;
            //    request.Proxy = new WebProxy(proxyuri, false);
            //    request.Proxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
            //}

            return request;
        }

        public void SendPacketAsync(string caption, byte[] message, Action<bool> callback, Action<TcpTransportResult> faultCallback = null)
        {
            //var guid = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();
            TLUtils.WriteLine("  HTTP: Send " + caption);
            var request = CreateRequest(message.Length, _host);


            request.BeginAsync(message, result =>
            {
                TLUtils.WriteLine();
                TLUtils.WriteLine();
                TLUtils.WriteLine("  HTTP: Receive " + caption + " (" + stopwatch.Elapsed + ")");
                RaiseGetBytes(result);
                ///callback(result);
            }, 
            () =>
                {
                    TLUtils.WriteLine();
                    TLUtils.WriteLine();
                    TLUtils.WriteLine("  HTTP: Receive Falt " + caption + " (" + stopwatch.Elapsed + ")");
                    faultCallback.SafeInvoke(null);
                });
        }

        public byte[] SendBytes(byte[] message)
        {
            //95.142.192.65:80
            //173.240.5.253:443
#if SILVERLIGHT
            throw new NotImplementedException("Sync mode is not supported in Windows Phone");
#else

            var request = CreateRequest(message.Length, _host);
            var dataStream = request.GetRequestStream();
            dataStream.Write(message, 0, message.Length);
            dataStream.Close();
            var response = request.GetResponse();
            //TLUtils.WriteLine(((HttpWebResponse)response).StatusDescription);
            dataStream = response.GetResponseStream();
            var buffer = new byte[Int32.Parse(response.Headers["Content-Length"])];
            var bytesRead = 0;
            var totalBytesRead = bytesRead;
            while (totalBytesRead < buffer.Length)
            {
                bytesRead = dataStream.Read(buffer, bytesRead, buffer.Length - bytesRead);
                totalBytesRead += bytesRead;
            }
            dataStream.Close();
            dataStream.Close();
            response.Close();

            RaiseGetBytes(buffer);
            return buffer;
#endif
        }

        public void Close()
        {
            
        }
    }
}
