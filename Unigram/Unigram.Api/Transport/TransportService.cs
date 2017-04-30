using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace Telegram.Api.Transport
{
    public class TransportService : ITransportService
    {
        public TransportService()
        {
            
        }

        private readonly Dictionary<string, ITransport> _cache = new Dictionary<string, ITransport>();

        private readonly Dictionary<string, ITransport> _fileCache = new Dictionary<string, ITransport>();

        public ITransport GetFileTransport(string host, int port, TransportType type, out bool isCreated)
        {
            var key = string.Format("{0} {1} {2}", host, port, type);
            if (_fileCache.ContainsKey(key))
            {
                isCreated = false;
                return _fileCache[key];
            }

#if WINDOWS_PHONE
            if (type == TransportType.Http)
            {
                var transport = new HttpTransport(host);

                _fileCache.Add(key, transport);
                isCreated = true;
                return transport;
                //transport.SetAddress(host, port, () => callback(transport));
            }
#endif
            else
            {
                var transport = 
#if !WIN_RT
                    new TcpTransportWinRT(host, port);
#else
                    new TcpTransport(host, port);
#endif
                transport.Additional = true;
                transport.ConnectionLost += OnConnectionLost;
                TLUtils.WritePerformance(string.Format("  TCP: New file transport {0}:{1}", host, port));

                _fileCache.Add(key, transport);
                isCreated = true;

                Debug.WriteLine("  TCP: New transport {0}:{1}", host, port);
                return transport;
                //trasport.SetAddress(host, port, () => callback(trasport));
            }
        }

        public ITransport GetTransport(string host, int port, TransportType type, out bool isCreated)
        {
            var key = string.Format("{0} {1} {2}", host, port, type);
            if (_cache.ContainsKey(key))
            {
                isCreated = false;

#if LOG_REGISTRATION
                TLUtils.WriteLog(string.Format("Old transport {2} {0}:{1}", host, port, _cache[key].Id));
#endif
                return _cache[key];
            }

#if WINDOWS_PHONE
            if (type == TransportType.Http)
            {
                var transport = new HttpTransport(host);

                _cache.Add(key, transport);
                isCreated = true;
                return transport;
                //transport.SetAddress(host, port, () => callback(transport));
            }
            else
#endif
            {
                var transport = 
#if WIN_RT
                    new TcpTransportWinRT(host, port);
#else
                    new TcpTransport(host, port);
#endif
                transport.Connecting += OnConnecting;
                transport.Connected += OnConnected;
                transport.ConnectionLost += OnConnectionLost;

#if LOG_REGISTRATION
                TLUtils.WriteLog(string.Format("New transport {2} {0}:{1}", host, port, transport.Id));
#endif
                TLUtils.WritePerformance(string.Format("  TCP: New transport {0}:{1}", host, port));

                _cache.Add(key, transport);
                isCreated = true;

                Debug.WriteLine("  TCP: New transport {0}:{1}", host, port);
                return transport;
                //trasport.SetAddress(host, port, () => callback(trasport));
            }
        }

        public void Close()
        {
            var transports = new List<ITransport>(_cache.Values);

            foreach (var transport in transports)
            {
                transport.Close();
                transport.Connecting -= OnConnecting;
                transport.Connected -= OnConnected;
                transport.ConnectionLost -= OnConnectionLost;
            }
            _cache.Clear();

            var fileTransports = new List<ITransport>(_fileCache.Values);

            foreach (var transport in fileTransports)
            {
                transport.Close();
                transport.Connecting -= OnConnecting;
                transport.Connected -= OnConnected;
                transport.ConnectionLost -= OnConnectionLost;
            }
            _fileCache.Clear();
        }

        public void CloseTransport(ITransport transport)
        {
            var transports = new List<ITransport>(_cache.Values);

            foreach (var value in _cache.Values.Where(x => string.Equals(x.Host, transport.Host, StringComparison.OrdinalIgnoreCase)))
            {
                value.Close();
                transport.Connecting -= OnConnecting;
                transport.Connected -= OnConnected;
                transport.ConnectionLost -= OnConnectionLost;
            }
            _cache.Remove(string.Format("{0} {1} {2}", transport.Host, transport.Port, transport.Type));

            var fileTransports = new List<ITransport>(_fileCache.Values);

            foreach (var value in _fileCache.Values.Where(x => string.Equals(x.Host, transport.Host, StringComparison.OrdinalIgnoreCase)))
            {
                value.Close();
                transport.Connecting -= OnConnecting;
                transport.Connected -= OnConnected;
                transport.ConnectionLost -= OnConnectionLost;
            }
            _fileCache.Remove(string.Format("{0} {1} {2}", transport.Host, transport.Port, transport.Type));
        }

        public event EventHandler<TransportEventArgs> TransportConnecting;

        protected virtual void RaiseTransportConnecting(ITransport transport)
        {
            TransportConnecting?.Invoke(this, new TransportEventArgs { Transport = transport });
        }

        public void OnConnecting(object sender, EventArgs args)
        {
            RaiseTransportConnecting(sender as ITransport);
            //return;

            //var mtProtoService = MTProtoService.Instance;
            //if (mtProtoService != null)
            //{
            //    mtProtoService.SetMessageOnTime(25.0, string.Format("Connecting..."));
            //}
        }

        public event EventHandler<TransportEventArgs> TransportConnected;

        protected virtual void RaiseTransportConnected(ITransport transport)
        {
            TransportConnected?.Invoke(this, new TransportEventArgs { Transport = transport });
        }

        public void OnConnected(object sender, EventArgs args)
        {
            RaiseTransportConnected(sender as ITransport);
            //return;

            //var mtProtoService = MTProtoService.Instance;
            //if (mtProtoService != null)
            //{
            //    mtProtoService.SetMessageOnTime(0.0, string.Empty);
            //}
        }

        public event EventHandler<TransportEventArgs> ConnectionLost;

        protected virtual void RaiseConnectionLost(ITransport transport)
        {
            var handler = ConnectionLost;
            if (handler != null) handler(this, new TransportEventArgs { Transport = transport });
        }

        public event EventHandler<TransportEventArgs> FileConnectionLost;

        protected virtual void RaiseFileConnectionLost(ITransport transport)
        {
            var handler = FileConnectionLost;
            if (handler != null) handler(this, new TransportEventArgs { Transport = transport });
        }

        private void OnConnectionLost(object sender, EventArgs e)
        {
            var transport = (ITransport)sender;
            if (transport.Additional)
            {
                RaiseFileConnectionLost(sender as ITransport);
            }
            else
            {
                RaiseConnectionLost(sender as ITransport);
            }
        }
    }

    public class TransportEventArgs : EventArgs
    {
        public ITransport Transport { get; set; }
    }
}
