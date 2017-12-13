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
using System.Diagnostics;

namespace Telegram.Api.Transport
{
    internal class TcpTransportWinRT : TcpTransportBase
    {
        private readonly StreamSocket _socket;

        private readonly object _isConnectedSyncRoot = new object();

        private bool _isConnected;

        private readonly object _dataWriterSyncRoot = new object();

        private readonly DataReader _dataReader;

        private readonly DataWriter _dataWriter;

        private readonly double _timeout;

        public TcpTransportWinRT(string host, int port, MTProtoTransportType mtProtoType, TLProxyConfig proxyConfig)
            : base(host, port, mtProtoType, proxyConfig)
        {
            _timeout = 25.0;
            _socket = new StreamSocket();
            var control = _socket.Control;
            control.QualityOfService = SocketQualityOfService.LowLatency;

            _dataReader = new DataReader(_socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial };
            _dataWriter = new DataWriter(_socket.OutputStream);

            //lock (_dataWriterSyncRoot)
            //{
            //    var buffer = GetInitBufferInternal();
            //    _dataWriter.WriteBytes(buffer);
            //}
        }

#if TCP_OBFUSCATED_2
        protected override byte[] GetInitBuffer()
        {
            var buffer = new byte[64];
            var random = new Random();
            while (true)
            {
                random.NextBytes(buffer);

                var val = (buffer[3] << 24) | (buffer[2] << 16) | (buffer[1] << 8) | (buffer[0]);
                var val2 = (buffer[7] << 24) | (buffer[6] << 16) | (buffer[5] << 8) | (buffer[4]);
                if (buffer[0] != 0xef
                    && val != 0x44414548
                    && val != 0x54534f50
                    && val != 0x20544547
                    && val != 0x4954504f
                    && val != 0xeeeeeeee
                    && val2 != 0x00000000)
                {
                    buffer[56] = buffer[57] = buffer[58] = buffer[59] = 0xef;
                    break;
                }
            }

            var keyIvEncrypt = buffer.SubArray(8, 48);
            EncryptKey = CryptographicBuffer.CreateFromByteArray(keyIvEncrypt.SubArray(0, 32));
            EncryptIV = keyIvEncrypt.SubArray(32, 16);
            //Array.Reverse(EncryptIV);

            Array.Reverse(keyIvEncrypt);
            DecryptKey = CryptographicBuffer.CreateFromByteArray(keyIvEncrypt.SubArray(0, 32));
            DecryptIV = keyIvEncrypt.SubArray(32, 16);
            //Array.Reverse(DecryptIV);

            //var shortStamp = BitConverter.GetBytes(0xefefefef);
            //for (var i = 0; i < shortStamp.Length; i++)
            //{
            //    buffer[56 + i] = shortStamp[i];
            //}

            var encryptedBuffer = Encrypt(buffer);
            for (var i = 56; i < encryptedBuffer.Length; i++)
            {
                buffer[i] = encryptedBuffer[i];
            }

            return buffer;
        }
#endif

        private async Task<bool> ConnectAsync(double timeout, Action<TcpTransportResult> faultCallback)
        {
            try
            {
                RaiseConnectingAsync();

                if (ProxyConfig != null && ProxyConfig.IsEnabled && !ProxyConfig.IsEmpty)
                {
                    Debug.WriteLine(">>> Connecting through SOCKS5");

                    await _socket.ConnectAsync(new HostName(ProxyConfig.Server), ProxyConfig.Port.ToString()).WithTimeout(timeout);

                    _dataWriter.WriteByte(0x05); // version
                    _dataWriter.WriteByte(0x02); // number of auth methods
                    _dataWriter.WriteByte(0x00); // no auth
                    _dataWriter.WriteByte(0x02); // password

                    await _dataWriter.StoreAsync();
                    await _dataReader.LoadAsync(2);

                    var response = new byte[_dataReader.UnconsumedBufferLength];
                    _dataReader.ReadBytes(response);

                    if (response[1] == 0x02)
                    {
                        var username = ProxyConfig.Username ?? string.Empty;
                        var password = ProxyConfig.Password ?? string.Empty;

                        _dataWriter.WriteByte(0x01); // version
                        _dataWriter.WriteByte((byte)username.Length);
                        _dataWriter.WriteBytes(Encoding.UTF8.GetBytes(username));
                        _dataWriter.WriteByte((byte)password.Length);
                        _dataWriter.WriteBytes(Encoding.UTF8.GetBytes(password));

                        await _dataWriter.StoreAsync();
                        await _dataReader.LoadAsync(2);

                        response = new byte[_dataReader.UnconsumedBufferLength];
                        _dataReader.ReadBytes(response);

                        if (response[1] != 0x00)
                        {
                            // TODO: failed
                        }
                    }

                    _dataWriter.WriteByte(0x05); // version
                    _dataWriter.WriteByte(0x01); // connect
                    _dataWriter.WriteByte(0x00); // reserved

                    var dest = System.Net.IPAddress.Parse(Host);
                    switch (dest.AddressFamily)
                    {
                        case System.Net.Sockets.AddressFamily.InterNetwork:
                            _dataWriter.WriteByte(0x01); // Ipv4
                            break;
                        case System.Net.Sockets.AddressFamily.InterNetworkV6:
                            _dataWriter.WriteByte(0x04); // Ipv6
                            break;
                    }

                    _dataWriter.WriteBytes(dest.GetAddressBytes());
                    _dataWriter.WriteUInt16((ushort)Port);

                    await _dataWriter.StoreAsync();
                    await _dataReader.LoadAsync(64);

                    response = new byte[_dataReader.UnconsumedBufferLength];
                    _dataReader.ReadBytes(response);

                    if (response[1] != 0x00)
                    {
                        // TODO: failed
                    }
                }
                else
                {
                    Debug.WriteLine(">>> Connecting");

                    //var address = IPAddress.IsValidIPv6(Host);
                    await _socket.ConnectAsync(new HostName(Host), Port.ToString(CultureInfo.InvariantCulture)).WithTimeout(timeout);
                }

                lock (_dataWriterSyncRoot)
                {
                    var buffer = GetInitBuffer();
                    _dataWriter.WriteBytes(buffer);
                }

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

            StopCheckConfigTimer();
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

    internal static class AsyncExtensions
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
