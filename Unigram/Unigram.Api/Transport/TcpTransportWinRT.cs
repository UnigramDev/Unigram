using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Telegram.Api.Transport
{
    public class TcpTransportWinRT : TcpTransportBase
    {
        private readonly StreamSocket _socket;

        private readonly object _isConnectedSyncRoot = new object();

        private bool _isConnected;

        private DataReader _dataReader;

        private DataWriter _dataWriter;

        private readonly double _timeout;

        private bool _isConnecting;

        private readonly List<Tuple<string, byte[], Action<TcpTransportResult>>> _queue = new List<Tuple<string, byte[], Action<TcpTransportResult>>>();

        public TcpTransportWinRT(string host, int port) : base(host, port)
        {
            _timeout = 25.0;
            _socket = new StreamSocket();
            StreamSocketControl control = _socket.Control;
            control.QualityOfService = SocketQualityOfService.LowLatency;
        }

        private async Task<bool> ConnectAsync(double timeout, Action<TcpTransportResult> faultCallback)
        {
            bool result;
            try
            {
                RaiseConnectingAsync();
                await _socket.ConnectAsync(new HostName(base.Host), base.Port.ToString(CultureInfo.InvariantCulture)).WithTimeout(timeout);
                _dataReader = new DataReader(_socket.InputStream);
                _dataReader.InputStreamOptions = InputStreamOptions.Partial;
                _dataWriter = new DataWriter(_socket.OutputStream);
                lock (_isConnectedSyncRoot)
                {
                    _isConnecting = false;
                    _isConnected = true;
                }
            }
            catch (Exception ex)
            {
                SocketErrorStatus status = SocketError.GetStatus(ex.HResult);
                WRITE_LOG("TCPTransportWinRT.ConnectAsync " + status, ex);
                SocketError.GetStatus(ex.HResult);
                faultCallback.SafeInvoke(new TcpTransportResult(ex));
                result = false;
                return result;
            }
            result = true;
            return result;
        }

        private async Task<bool> SendAsync(double timeout, byte[] data, Action<TcpTransportResult> faultCallback)
        {
            bool result;
            try
            {
                _dataWriter.WriteBytes(data);
                await _dataWriter.StoreAsync();
            }
            catch (Exception ex)
            {
                SocketErrorStatus status = SocketError.GetStatus(ex.HResult);
                WRITE_LOG("TCPTransportWinRT.SendAsync " + status, ex);
                faultCallback.SafeInvoke(new TcpTransportResult(ex));
                result = false;
                return result;
            }
            result = true;
            return result;
        }

        private async void ReceiveAsync(double timeout)
        {
            while (!base.Closed)
            {
                int num = 0;
                try
                {
                    num = (int)await _dataReader.LoadAsync(60);
                }
                catch (Exception ex)
                {
                    SocketErrorStatus status = SocketError.GetStatus(ex.HResult);
                    WRITE_LOG("ReceiveAsync DataReader.LoadAsync " + status, ex);
                    if (ex is ObjectDisposedException)
                    {
                        break;
                    }
                }
                if (num > 0)
                {
                    DateTime now = DateTime.Now;
                    if (!base.FirstReceiveTime.HasValue)
                    {
                        base.FirstReceiveTime = new DateTime?(now);
                        RaiseConnectedAsync();
                    }
                    base.LastReceiveTime = new DateTime?(now);
                    byte[] array = new byte[_dataReader.UnconsumedBufferLength];
                    _dataReader.ReadBytes(array);
                    base.OnBufferReceived(array, 0, num);
                    continue;
                }
                if (!base.Closed)
                {
                    base.Closed = true;
                    RaiseConnectionLost();
                    Execute.ShowDebugMessage("TCPTransportWinRT connection lost; close transport");
                    continue;
                }
            }
        }

        public override void SendPacketAsync(string caption, byte[] data, Action<bool> callback, Action<TcpTransportResult> faultCallback = null)
        {
            DateTime now = DateTime.Now;
            if (!base.FirstSendTime.HasValue)
            {
                base.FirstSendTime = new DateTime?(now);
            }
            Execute.BeginOnThreadPool(async delegate
            {
                bool flag = false;
                bool isConnected;
                lock (_isConnectedSyncRoot)
                {
                    isConnected = _isConnected;
                    if (!_isConnected && !_isConnecting)
                    {
                        _isConnecting = true;
                        flag = true;
                    }
                    if (flag && caption.StartsWith("msgs_ack"))
                    {
                        Execute.ShowDebugMessage("TCPTransportWinRT connect on msgs_ack");
                        flag = false;
                    }
                }
                if (!isConnected)
                {
                    if (!flag)
                    {
                        Enqueue(caption, data, faultCallback);
                        return;
                    }
                    if (!(await ConnectAsync(_timeout, faultCallback)))
                    {
                        return;
                    }
                    if (!(await SendAsync(_timeout, new byte[]
                    {
                        239
                    }, faultCallback)))
                    {
                        return;
                    }
                    ReceiveAsync(_timeout);
                    SendQueue(_timeout);
                }
                if (await SendAsync(_timeout, TcpTransportBase.CreatePacket(data), faultCallback))
                {
                    callback.SafeInvoke(true);
                }
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
            List<Tuple<string, byte[], Action<TcpTransportResult>>> list = new List<Tuple<string, byte[], Action<TcpTransportResult>>>();
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("SendQueue");
            lock (_isConnectedSyncRoot)
            {
                foreach (Tuple<string, byte[], Action<TcpTransportResult>> current in _queue)
                {
                    list.Add(current);
                    stringBuilder.AppendLine(current.Item1);
                }
                _queue.Clear();
            }
            foreach (Tuple<string, byte[], Action<TcpTransportResult>> current2 in list)
            {
                SendAsync(timeout, TcpTransportBase.CreatePacket(current2.Item2), current2.Item3);
            }
        }

        public override void Close()
        {
            base.Closed = true;
            if (_socket != null)
            {
                _socket.Dispose();
            }
        }

        public override string GetTransportInfo()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("TCP_WinRT transport");
            stringBuilder.AppendLine(string.Format("Socket {0}:{1}, Connected={2}, HashCode={3}", new object[]
            {
                base.Host,
                base.Port,
                _isConnected,
                _socket.GetHashCode()
            }));
            stringBuilder.AppendLine(string.Format("LastReceiveTime={0}", new object[]
            {
                base.LastReceiveTime.GetValueOrDefault().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
            }));
            stringBuilder.AppendLine(string.Format("FirstSendTime={0}", new object[]
            {
                base.FirstSendTime.GetValueOrDefault().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
            }));
            return stringBuilder.ToString();
        }
    }
}
