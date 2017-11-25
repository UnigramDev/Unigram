#define TCP_OBFUSCATED_2
using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Windows.Security.Cryptography;
using Action = System.Action;
using SocketError = System.Net.Sockets.SocketError;

namespace Telegram.Api.Transport
{
    internal class TcpTransport : TcpTransportBase
    {
        private readonly object _isConnectedSocketRoot = new object();

        private readonly object _encryptedStreamSyncRoot = new object();

        private readonly Socket _socket;

        private const int BufferSize = 64;

        private readonly byte[] _buffer;

        private readonly SocketAsyncEventArgs _listener = new SocketAsyncEventArgs();

        private IPAddress _address;

        public TcpTransport(string host, int port, MTProtoTransportType mtProtoType, TLProxyConfig proxyConfig)
            : base(host, port, mtProtoType, proxyConfig)
        {
            // ipv6 support

            _address = IPAddress.Parse(host);

            //var addressFamily = AddressFamily.InterNetwork;
            //if (IPAddress.TryParse(host, out _address))
            //{
            //    if (_address.AddressFamily == AddressFamily.InterNetworkV6)
            //    {
            //        addressFamily = AddressFamily.InterNetworkV6;
            //    }
            //}

            _socket = new Socket(_address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _buffer = new byte[BufferSize];
            _listener.SetBuffer(_buffer, 0, _buffer.Length);
            _listener.Completed += OnReceived;
        }

        public override string GetTransportInfo()
        {
            var info = new StringBuilder();
            info.AppendLine("TCP transport");
            info.AppendLine(string.Format("Socket {0}:{1}, Connected={2}, Ttl={3}, HashCode={4}", Host, Port, _socket.Connected, _socket.Ttl, _socket.GetHashCode()));
            info.AppendLine(string.Format("Listener LastOperation={0}, SocketError={1}, RemoteEndPoint={2}, SocketHash={3}", _listener.LastOperation, _listener.SocketError, _listener.RemoteEndPoint, _listener.ConnectSocket != null ? _listener.ConnectSocket.GetHashCode().ToString() : "null"));
            info.AppendLine(string.Format("FirstReceiveTime={0}", FirstReceiveTime.GetValueOrDefault().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
            info.AppendLine(string.Format("FirstSendTime={0}", FirstSendTime.GetValueOrDefault().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
            info.AppendLine(string.Format("LastSendTime={0}", LastSendTime.GetValueOrDefault().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));

            return info.ToString();
        }

        public override void SendPacketAsync(string caption, byte[] data, Action<bool> callback, Action<TcpTransportResult> faultCallback = null)
        {
            var now = DateTime.Now;
            if (!FirstSendTime.HasValue)
            {
                FirstSendTime = now;
            }
            LastSendTime = now;

            Execute.BeginOnThreadPool(() =>
            {
                TLUtils.WriteLine("  TCP: Send " + caption);

                lock (_isConnectedSocketRoot)
                {
                    var manualResetEvent = new ManualResetEvent(false);
                    if (!_socket.Connected)
                    {
                        if (caption.StartsWith("msgs_ack"))
                        {
                            TLUtils.WriteLine("!!!!!!MSGS_ACK FAULT!!!!!!!", LogSeverity.Error);
                            faultCallback?.Invoke(new TcpTransportResult(SocketAsyncOperation.Send, new Exception("MSGS_ACK_FAULT")));
                            return;
                        }

                        ConnectAsync(() =>
                        {
                            manualResetEvent.Set();

                            try
                            {
                                lock (_encryptedStreamSyncRoot)
                                {
                                    var args = CreateArgs(data, callback);
                                    _socket.SendAsync(args);
                                }
                            }
                            catch (Exception ex)
                            {
                                faultCallback?.Invoke(new TcpTransportResult(SocketAsyncOperation.Send, ex));

                                WRITE_LOG("Socket.ConnectAsync SendAsync[1]", ex);
                            }
                        },
                            error =>
                            {
                                manualResetEvent.Set();
                                faultCallback?.Invoke(error);
                            });

                        var connected = manualResetEvent.WaitOne(25000);
                        if (!connected)
                        {
                            faultCallback?.Invoke(new TcpTransportResult(SocketAsyncOperation.Connect, new Exception("Connect timeout exception 25s")));
                        }
                    }
                    else
                    {
                        try
                        {
                            lock (_encryptedStreamSyncRoot)
                            {
                                var args = CreateArgs(data, callback);
                                _socket.SendAsync(args);
                            }
                        }
                        catch (Exception ex)
                        {
                            faultCallback?.Invoke(new TcpTransportResult(SocketAsyncOperation.Send, ex));

                            WRITE_LOG("Socket.SendAsync[1]", ex);
                        }
                    }
                }
            });
        }

        private SocketAsyncEventArgs CreateArgs(byte[] data, Action<bool> callback = null)
        {
            var packet = CreatePacket(data);

#if TCP_OBFUSCATED_2
            packet = Encrypt(packet);
#endif

            var args = new SocketAsyncEventArgs();
            args.SetBuffer(packet, 0, packet.Length);
            args.Completed += (sender, eventArgs) =>
            {
                callback?.Invoke(eventArgs.SocketError == SocketError.Success);
            };
            return args;
        }

        private void ConnectAsync(Action callback, Action<TcpTransportResult> faultCallback)
        {
            WRITE_LOG(string.Format("Socket.ConnectAsync[#3] {0} ({1}:{2})", Id, Host, Port));

            if (ProxyConfig != null && ProxyConfig.IsEnabled && !ProxyConfig.IsEmpty)
            {
                try
                {
                    RaiseConnectingAsync();

                    System.Diagnostics.Debug.WriteLine("  Connecting proxy=[{0}] mtproto=[server={1} port={2}]", ProxyConfig, Host, Port);

                    // TODO: ask Evgeny
                    // SocksProxy.ConnectToSocks5Proxy(_socket, ProxyConfig.Server, (ushort)ProxyConfig.Port, Host, (ushort)Port, ProxyConfig.Username.ToString(), ProxyConfig.Password.ToString());
                }
                catch (Exception ex)
                {
                    faultCallback?.Invoke(new TcpTransportResult(SocketAsyncOperation.Connect, ex));

                    WRITE_LOG("Socket.ConnectAsync[#3]", ex);
                }

                OnConnected(new SocketAsyncEventArgs { SocketError = SocketError.Success }, callback, faultCallback);
            }
            else
            {

                System.Diagnostics.Debug.WriteLine("  Connecting mtproto=[server={0} port={1}]", Host, Port);

                var args = new SocketAsyncEventArgs
                {
                    RemoteEndPoint = new IPEndPoint(_address, Port)
                    //RemoteEndPoint = _address != null? (EndPoint)new IPEndPoint(_address, Port) : new DnsEndPoint(Host, Port)
                };

                args.Completed += (o, e) => OnConnected(e, callback, faultCallback);

                try
                {
                    RaiseConnectingAsync();
                    _socket.ConnectAsync(args);
                }
                catch (Exception ex)
                {
                    faultCallback?.Invoke(new TcpTransportResult(SocketAsyncOperation.Connect, ex));

                    WRITE_LOG("Socket.ConnectAsync[#3]", ex);
                }
            }
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

        private void OnConnected(SocketAsyncEventArgs args, Action callback = null, Action<TcpTransportResult> faultCallback = null)
        {
            WRITE_LOG(string.Format("Socket.OnConnected[#4] {0} socketError={1}", Id, args.SocketError));

            try
            {
                if (args.SocketError != SocketError.Success)
                {
                    faultCallback?.Invoke(new TcpTransportResult(SocketAsyncOperation.Connect, args.SocketError));
                }
                else
                {
                    RaiseConnectedAsync();

                    ReceiveAsync();

                    try
                    {
                        lock (_encryptedStreamSyncRoot)
                        {
                            var sendArgs = new SocketAsyncEventArgs();
                            var buffer = GetInitBuffer();
                            sendArgs.SetBuffer(buffer, 0, buffer.Length);

                            //sendArgs.SetBuffer(new byte[] { 0xef }, 0, 1);
                            sendArgs.Completed += (o, e) => callback?.Invoke();
                            _socket.SendAsync(sendArgs);
                        }
                    }
                    catch (Exception ex)
                    {
                        faultCallback?.Invoke(new TcpTransportResult(SocketAsyncOperation.Send, ex));

                        WRITE_LOG("Socket.OnConnected[#4]", ex);
                    }
                }

            }
            catch (Exception ex)
            {
                faultCallback?.Invoke(new TcpTransportResult(SocketAsyncOperation.Connect, ex));

                WRITE_LOG("Socket.OnConnected[#4] SendAsync", ex);
            }
        }

        private void ReceiveAsync()
        {
            if (Closed)
            {
                //Execute.ShowDebugMessage("TCPTransport ReceiveAsync closed=true");
                return;
            }

            try
            {
                if (_socket != null)
                {
                    if (_socket.Connected)
                    {
#if DEBUG
                        for (var i = 0; i < _buffer.Length; i++)
                        {
                            _buffer[i] = 0x0;
                        }
#endif

                        try
                        {
                            _socket.ReceiveAsync(_listener);
                        }
                        catch (Exception ex)
                        {
                            WRITE_LOG("Socket.ReceiveAsync[#5] ReceiveAsync", ex);

                            if (ex is ObjectDisposedException)
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        //Execute.ShowDebugMessage("TCPTransport ReceiveAsync socket.Connected=false");
                        //throw new Exception("Socket is not connected");
                    }
                }
                else
                {
                    throw new NullReferenceException("Socket is null");
                }
            }
            catch (Exception ex)
            {
                WRITE_LOG("Socket.ReceiveAsync[#5]", ex);
            }
        }

        private void OnReceived(object sender, SocketAsyncEventArgs e)
        {
            if (MTProtoType == MTProtoTransportType.Special)
            {

            }

            var dcId = DCId;
            var hashCode = GetHashCode();
            var socket = sender as Socket;
            if (socket == null || socket != _socket)
            {
                return;
            }

            if (e.SocketError != SocketError.Success)
            {
                //Log.Write(string.Format("  TCPTransport.OnReceived transport={0} error={1}", Id, e.SocketError));
                Execute.ShowDebugMessage("TCPTransport OnReceived connection lost; SocketError=" + e.SocketError);
                ReceiveAsync();
                return;
            }

            if (e.BytesTransferred > 0)
            {
                //Log.Write(string.Format("  TCPTransport.OnReceived transport={0} bytes_transferred={1}", Id, e.BytesTransferred));
                var now = DateTime.Now;

                if (!FirstReceiveTime.HasValue)
                {
                    FirstReceiveTime = now;
                }

                LastReceiveTime = now;

                // AES-CTR decrypt
#if TCP_OBFUSCATED_2
                var buffer = e.Buffer.SubArray(e.Offset, e.BytesTransferred);
                buffer = Decrypt(buffer);
                OnBufferReceived(buffer, 0, buffer.Length);
#else
                OnBufferReceived(e.Buffer, e.Offset, e.BytesTransferred);
#endif
            }
            else
            {
                Closed = true;
                RaiseConnectionLost();
                //Log.Write("  TCPTransport.Recconect reason=BytesTransferred=0 transport=" + Id);
                //Execute.ShowDebugMessage(string.Format("TCPTransport id={0} dc_id={1} hash={2} OnReceived connection lost bytesTransferred=0; close transport; error={3}", Id, DCId, GetHashCode(), e.SocketError));
            }

            ReceiveAsync();
        }

        public override void Close()
        {
            WRITE_LOG(string.Format("Close socket {2} {0}:{1}", Host, Port, Id));

            if (_socket != null)
            {
                _socket.Dispose();
                Closed = true;
            }

            StopCheckConfigTimer();
        }

        public DateTime? LastSendTime { get; protected set; }
    }
}
