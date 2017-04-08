#define TCP_OBFUSCATED_2
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Windows.Security.Cryptography;

namespace Telegram.Api.Transport
{
    public class TcpTransportWinRT : TcpTransportBase
    {
        private readonly StreamSocket _socket;

        private readonly object _isConnectedSyncRoot = new object();

        private bool _isConnected;

        private readonly object _dataWriterSyncRoot = new object();

        private readonly DataReader _dataReader;

        private readonly DataWriter _dataWriter;

        private readonly double _timeout;

        public TcpTransportWinRT(string host, int port) : base(host, port)
        {
            _timeout = 25.0;
            _socket = new StreamSocket();
            var control = _socket.Control;
            control.QualityOfService = SocketQualityOfService.LowLatency;

            _dataReader = new DataReader(_socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
            _dataWriter = new DataWriter(_socket.OutputStream);

            lock (_dataWriterSyncRoot)
            {
                var buffer = GetInitBufferInternal();
                _dataWriter.WriteBytes(buffer);
            }
        }

#if TCP_OBFUSCATED_2
        private byte[] GetInitBufferInternal()
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

            var keyIvEncrypt = buffer.SubArray(8, 48);
            EncryptKey = CryptographicBuffer.CreateFromByteArray(keyIvEncrypt.SubArray(0, 32));
            EncryptIV = keyIvEncrypt.SubArray(32, 16);
            //Array.Reverse(EncryptIV);

            Array.Reverse(keyIvEncrypt);
            DecryptKey = CryptographicBuffer.CreateFromByteArray(keyIvEncrypt.SubArray(0, 32));
            DecryptIV = keyIvEncrypt.SubArray(32, 16);
            //Array.Reverse(DecryptIV);

            var shortStamp = BitConverter.GetBytes(0xefefefef);
            for (var i = 0; i < shortStamp.Length; i++)
            {
                buffer[56 + i] = shortStamp[i];
            }

            var encryptedBuffer = Encrypt(buffer);
            for (var i = 56; i < encryptedBuffer.Length; i++)
            {
                buffer[i] = encryptedBuffer[i];
            }

            return buffer;
        }

        protected override byte[] GetInitBuffer()
        {
            return GetInitBufferInternal();
        }
#endif

        private async Task<bool> ConnectAsync(double timeout, Action<TcpTransportResult> faultCallback)
        {
            try
            {
                RaiseConnectingAsync();

                //var address = IPAddress.IsValidIPv6(Host);
                await _socket.ConnectAsync(new HostName(Host), Port.ToString(CultureInfo.InvariantCulture)).WithTimeout(timeout);

                lock (_isConnectedSyncRoot)
                {
                    _isConnecting = false;
                    _isConnected = true;
                }
            }
            catch (Exception ex)
            {
                var status = SocketError.GetStatus(ex.HResult);
                WRITE_LOG("TCPTransportWinRT.ConnectAsync " + status, ex);

                var error = SocketError.GetStatus(ex.HResult);
                faultCallback?.Invoke(new TcpTransportResult(ex));
                return false;
            }

            return true;
        }

        private async Task<bool> SendAsync(double timeout, byte[] data, Action<TcpTransportResult> faultCallback)
        {
            try
            {
#if TCP_OBFUSCATED_2
                lock(_dataWriterSyncRoot)
                {
                    data = Encrypt(data);
                    _dataWriter.WriteBytes(data); 
                    var result = _dataWriter.StoreAsync().AsTask().Result;
                }

                //var storeResult = await _dataWriter.StoreAsync().AsTask().Result;
#else
                lock (_dataWriterSyncRoot)
                {
                    _dataWriter.WriteBytes(data);
                }
                var storeResult = await _dataWriter.StoreAsync();//.WithTimeout(timeout);
#endif
            }
            catch (Exception ex)
            {
                var status = SocketError.GetStatus(ex.HResult);
                WRITE_LOG("TCPTransportWinRT.SendAsync " + status, ex);

                faultCallback?.Invoke(new TcpTransportResult(ex));
                return false;
            }

            return true;
        }

        private async void ReceiveAsync(double timeout)
        {
            while (true)
            {
                if (Closed)
                {
                    return;
                }

                int bytesTransferred = 0;
                try
                {
                    bytesTransferred = (int) await _dataReader.LoadAsync(64); //WithTimeout(timeout);
                }
                catch (Exception ex)
                {
                    //Log.Write(string.Format("  TCPTransport.ReceiveAsync transport={0} LoadAsync exception={1}", Id, ex));
                    var status = SocketError.GetStatus(ex.HResult);
                    WRITE_LOG("ReceiveAsync DataReader.LoadAsync " + status, ex);

                    if (ex is ObjectDisposedException)
                    {
                        return;
                    }
                }

                if (bytesTransferred > 0)
                {
                    //Log.Write(string.Format("  TCPTransport.ReceiveAsync transport={0} bytes_transferred={1}", Id, bytesTransferred));
                
                    var now = DateTime.Now;

                    if (!FirstReceiveTime.HasValue)
                    {
                        FirstReceiveTime = now;
                        RaiseConnectedAsync();
                    }

                    LastReceiveTime = now;

                    var buffer = new byte[_dataReader.UnconsumedBufferLength];
                    _dataReader.ReadBytes(buffer);

#if TCP_OBFUSCATED_2
                    buffer = Decrypt(buffer);
#endif

                    OnBufferReceived(buffer, 0, bytesTransferred);
                }
                else
                {
                    //Log.Write(string.Format("  TCPTransport.ReceiveAsync transport={0} bytes_transferred={1} closed={2}", Id, bytesTransferred, Closed));
                    if (!Closed)
                    {
                        Closed = true;
                        RaiseConnectionLost();
                        Execute.ShowDebugMessage("TCPTransportWinRT ReceiveAsync connection lost bytesTransferred=0; close transport");
                    }
                }
            }
        }

        private bool _isConnecting;

        private readonly List<Tuple<string, byte[], Action<TcpTransportResult>>> _queue = new List<Tuple<string, byte[], Action<TcpTransportResult>>>(); 

        public override void SendPacketAsync(string caption, byte[] data, Action<bool> callback, Action<TcpTransportResult> faultCallback = null)
        {
            var now = DateTime.Now;
            if (!FirstSendTime.HasValue)
            {
                FirstSendTime = now;
            }

            Execute.BeginOnThreadPool(async () =>
            {
                bool connect = false;
                bool isConnected;
                lock (_isConnectedSyncRoot)
                {
                    isConnected = _isConnected;
                    if (!_isConnected && !_isConnecting)
                    {
                        _isConnecting = true;
                        connect = true;
                    }

                    if (connect
                        && caption.StartsWith("msgs_ack"))
                    {
                        Execute.ShowDebugMessage("TCPTransportWinRT connect on msgs_ack");
                        connect = false;
                    }
                }

                if (!isConnected)
                {
                    if (connect)
                    {
                        var connectResult = await ConnectAsync(_timeout, faultCallback);
                        if (!connectResult) return;

                        //var buffer = GetInitBuffer();
                        //var sendResult = await SendAsync(_timeout, buffer, faultCallback);
                        //if (!sendResult) return;
                        
                        ReceiveAsync(_timeout);

                        SendQueue(_timeout);
                    }
                    else
                    {
                        Enqueue(caption, data, faultCallback);
                        return;
                    }
                }

                var sendPacketResult = await SendAsync(_timeout, CreatePacket(data), faultCallback);
                if (!sendPacketResult) return;

                callback?.Invoke(true);
            });
        }

        private void Enqueue(string caption, byte[] data, Action<TcpTransportResult> faultCallback)
        {
            lock (_isConnectedSyncRoot)
            {
                _queue.Add(new Tuple<string, byte[], Action<TcpTransportResult>>(caption, data, faultCallback));
            }
        }

        private void SendQueue(double timeout)
        {
            var queue = new List<Tuple<string, byte[], Action<TcpTransportResult>>>();
            var info = new StringBuilder();
            info.Append("SendQueue");
            lock (_isConnectedSyncRoot)
            {
                foreach (var tuple in _queue)
                {
                    queue.Add(tuple);
                    info.AppendLine(tuple.Item1);
                }

                _queue.Clear();
            }

            //Execute.ShowDebugMessage(info.ToString());

            foreach (var tuple in queue)
            {
                SendAsync(timeout, CreatePacket(tuple.Item2), tuple.Item3);
            }
        }

        public override void Close()
        {
            Closed = true;
            if (_socket != null)
            {
                _socket.Dispose();
            }
        }

        public override string GetTransportInfo()
        {
            var info = new StringBuilder();
            info.AppendLine("TCP_WinRT transport");
            info.AppendLine(string.Format("Socket {0}:{1}, Connected={2}, HashCode={3}", Host, Port, _isConnected, _socket.GetHashCode()));
            info.AppendLine(string.Format("LastReceiveTime={0}", LastReceiveTime.GetValueOrDefault().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
            info.AppendLine(string.Format("FirstSendTime={0}", FirstSendTime.GetValueOrDefault().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));

            return info.ToString();
        }
    }

    public static class AsyncExtensions
    {
        public static async Task WithTimeout(this IAsyncAction task, double timeout)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            await task.AsTask(cts.Token);
        }

        public static async Task<T> WithTimeout<T>(this IAsyncOperation<T> task, double timeout)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            return await task.AsTask(cts.Token);
        }
    }
}
