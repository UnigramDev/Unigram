using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace Telegram.Api.Transport
{
    public class TransportService : ITransportService
    {
        public TransportService()
        {
            
        }

        private readonly object _proxyConfigSyncRoot = new object();

        private TLProxyConfig _proxyConfig;
        public TLProxyConfig GetProxyConfig()
        {
            return new TLProxyConfig
            {
                Server = SettingsHelper.ProxyServer,
                Port = SettingsHelper.ProxyPort,
                Username = SettingsHelper.ProxyUsername,
                Password = SettingsHelper.ProxyPassword,
                IsEnabled = SettingsHelper.IsProxyEnabled
            };

            if (_proxyConfig != null)
            {
                return _proxyConfig;
            }

            _proxyConfig = TLUtils.OpenObjectFromMTProtoFile<TLProxyConfig>(_proxyConfigSyncRoot, Constants.ProxyConfigFileName) ?? TLProxyConfig.Empty;
            return _proxyConfig;
        }

        public void SetProxyConfig(TLProxyConfig proxyConfig)
        {
            _proxyConfig = proxyConfig;
            TLUtils.SaveObjectToMTProtoFile(_proxyConfigSyncRoot, Constants.ProxyConfigFileName, _proxyConfig);
        }

        private readonly Dictionary<string, ITransport> _cache = new Dictionary<string, ITransport>();

        private readonly Dictionary<string, ITransport> _fileCache = new Dictionary<string, ITransport>();

        private readonly Dictionary<string, ITransport> _specialCache = new Dictionary<string, ITransport>();

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
                var transport = new HttpTransport(host, MTProtoTransportType.File, GetProxyConfig());

                _fileCache.Add(key, transport);
                isCreated = true;
                return transport;
                //transport.SetAddress(host, port, () => callback(transport));
            }
#endif
            else
            {
                var transport =
#if WIN_RT
                    new TcpTransportWinRT(host, port, MTProtoTransportType.File, GetProxyConfig());
#else
                    new TcpTransport(host, port, MTProtoTransportType.File, GetProxyConfig());
#endif
                transport.ConnectionLost += OnConnectionLost;
                TLUtils.WritePerformance(string.Format("  TCP: New file transport {0}:{1}", host, port));

                _fileCache.Add(key, transport);
                isCreated = true;

                Debug.WriteLine("  TCP: New transport {0}:{1}", host, port);
                return transport;
                //trasport.SetAddress(host, port, () => callback(trasport));
            }
        }

        private readonly Dictionary<string, ITransport> _fileCache2 = new Dictionary<string, ITransport>();

        public ITransport GetFileTransport2(string host, int port, TransportType type, out bool isCreated)
        {
            var key = string.Format("{0} {1} {2}", host, port, type);
            if (_fileCache2.ContainsKey(key))
            {
                isCreated = false;
                return _fileCache2[key];
            }

#if WINDOWS_PHONE
            if (type == TransportType.Http)
            {
                var transport = new HttpTransport(host, MTProtoTransportType.File, GetProxyConfig());

                _fileCache2.Add(key, transport);
                isCreated = true;
                return transport;
                //transport.SetAddress(host, port, () => callback(transport));
            }
#endif
            else
            {
                var transport =
#if WIN_RT
                    new TcpTransportWinRT(host, port, MTProtoTransportType.File, GetProxyConfig());
#else
                    new TcpTransport(host, port, MTProtoTransportType.File, GetProxyConfig());
#endif
                transport.ConnectionLost += OnConnectionLost;
                TLUtils.WritePerformance(string.Format("  TCP: New file transport 2 {0}:{1}", host, port));

                _fileCache2.Add(key, transport);
                isCreated = true;

                Debug.WriteLine("  TCP: New transport {0}:{1}", host, port);
                return transport;
                //trasport.SetAddress(host, port, () => callback(trasport));
            }
        }

        public ITransport GetTransport(string host, int port, TransportType type, out bool isCreated)
        {
            //if (host == "149.154.175.50")
            //{
            //    host = "149.154.175.51";
            //}

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
                var transport = new HttpTransport(host, MTProtoTransportType.Main, GetProxyConfig());

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
                    new TcpTransportWinRT(host, port, MTProtoTransportType.Main, GetProxyConfig());
#else
                    new TcpTransport(host, port, MTProtoTransportType.Main, GetProxyConfig());
#endif
                transport.Connecting += OnConnecting;
                transport.Connected += OnConnected;
                transport.ConnectionLost += OnConnectionLost;
                transport.CheckConfig += OnCheckConfig;

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

        public ITransport GetSpecialTransport(string host, int port, TransportType type, out bool isCreated)
        {
            //if (host == "149.154.175.50")
            //{
            //    host = "149.154.175.51";
            //}

            var key = string.Format("{0} {1} {2}", host, port, type);
            if (_specialCache.ContainsKey(key))
            {
                isCreated = false;

#if LOG_REGISTRATION
                TLUtils.WriteLog(string.Format("Old transport {2} {0}:{1}", host, port, _specialCache[key].Id));
#endif
                return _specialCache[key];
            }

#if WINDOWS_PHONE
            if (type == TransportType.Http)
            {
                var transport = new HttpTransport(host, MTProtoTransportType.Special, GetProxyConfig());

                _specialCache.Add(key, transport);
                isCreated = true;
                return transport;
                //transport.SetAddress(host, port, () => callback(transport));
            }
            else
#endif
            {
                var transport =
#if WIN_RT
                    new TcpTransportWinRT(host, port, MTProtoTransportType.Special, GetProxyConfig());
#else
                    new TcpTransport(host, port, MTProtoTransportType.Special, GetProxyConfig());
#endif
                transport.Connecting += OnConnecting;
                transport.Connected += OnConnected;
                transport.ConnectionLost += OnConnectionLost;
                transport.CheckConfig += OnCheckConfig;

#if LOG_REGISTRATION
                TLUtils.WriteLog(string.Format("New transport {2} {0}:{1}", host, port, transport.Id));
#endif
                TLUtils.WritePerformance(string.Format("  TCP: New transport {0}:{1}", host, port));

                _specialCache.Add(key, transport);
                isCreated = true;

                Debug.WriteLine("  TCP: New transport {0}:{1}", host, port);
                return transport;
                //trasport.SetAddress(host, port, () => callback(trasport));
            }
        }

        public event EventHandler CheckConfig;
        protected virtual void RaiseCheckConfig()
        {
            CheckConfig?.Invoke(this, EventArgs.Empty);
        }

        private void OnCheckConfig(object sender, EventArgs e)
        {
            var transport = sender as ITransport;
            if (transport != null && transport.MTProtoType == MTProtoTransportType.Main)
            {
                Logs.Log.Write(string.Format("TransportService CheckConfig Transport=[dc_id={0} ip={1} port={2} proxy=[{3}]]", transport.DCId, transport.Host, transport.Port, transport.ProxyConfig));

                RaiseCheckConfig();
            }
        }

        public void Close()
        {
            var transports = new List<ITransport>(_cache.Values);

            foreach (var transport in transports)
            {
                transport.Connecting -= OnConnecting;
                transport.Connected -= OnConnected;
                transport.ConnectionLost -= OnConnectionLost;
                transport.Close();
            }
            _cache.Clear();

            var fileTransports = new List<ITransport>(_fileCache.Values);

            foreach (var transport in fileTransports)
            {
                transport.Connecting -= OnConnecting;
                transport.Connected -= OnConnected;
                transport.ConnectionLost -= OnConnectionLost;
                transport.Close();
            }

            _fileCache.Clear();

            var fileTransports2 = new List<ITransport>(_fileCache2.Values);

            foreach (var transport in fileTransports2)
            {
                transport.Connecting -= OnConnecting;
                transport.Connected -= OnConnected;
                transport.ConnectionLost -= OnConnectionLost;
                transport.Close();
            }

            _fileCache2.Clear();
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

            foreach (var value in _fileCache.Values.Where(x => string.Equals(x.Host, transport.Host, StringComparison.OrdinalIgnoreCase)))
            {
                value.Close();
                transport.Connecting -= OnConnecting;
                transport.Connected -= OnConnected;
                transport.ConnectionLost -= OnConnectionLost;
            }
            _fileCache.Remove(string.Format("{0} {1} {2}", transport.Host, transport.Port, transport.Type));

            foreach (var value in _fileCache2.Values.Where(x => string.Equals(x.Host, transport.Host, StringComparison.OrdinalIgnoreCase)))
            {
                value.Close();
                transport.Connecting -= OnConnecting;
                transport.Connected -= OnConnected;
                transport.ConnectionLost -= OnConnectionLost;
            }
            _fileCache2.Remove(string.Format("{0} {1} {2}", transport.Host, transport.Port, transport.Type));
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
            ConnectionLost?.Invoke(this, new TransportEventArgs { Transport = transport });
        }

        public event EventHandler<TransportEventArgs> FileConnectionLost;
        protected virtual void RaiseFileConnectionLost(ITransport transport)
        {
            FileConnectionLost?.Invoke(this, new TransportEventArgs { Transport = transport });
        }

        public event EventHandler<TransportEventArgs> SpecialConnectionLost;
        protected virtual void RaiseSpecialConnectionLost(ITransport transport)
        {
            SpecialConnectionLost?.Invoke(this, new TransportEventArgs { Transport = transport });
        }

        private void OnConnectionLost(object sender, EventArgs e)
        {
            var transport = (ITransport)sender;
            if (transport.MTProtoType == MTProtoTransportType.File)
            {
                RaiseFileConnectionLost(sender as ITransport);
            }
            else if (transport.MTProtoType == MTProtoTransportType.Special)
            {
                RaiseSpecialConnectionLost(sender as ITransport);
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

    public class TLProxyConfig : TLObject
    {
        public const uint Signature = 4294967066u;

        public bool IsEnabled { get; set; }

        public string Server { get; set; }
        public int Port { get; set; }

        public string Username { get; set; }
        public string Password { get; set; }

        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(this.Server) || this.Port < 0;
            }
        }

        //public override TLObject FromStream(Stream input)
        //{
        //    this.IsEnabled = TLObject.GetObject<TLBool>(input);
        //    this.Server = TLObject.GetObject<TLString>(input);
        //    this.Port = TLObject.GetObject<TLInt>(input);
        //    this.Username = TLObject.GetObject<TLString>(input);
        //    this.Password = TLObject.GetObject<TLString>(input);
        //    return this;
        //}

        //public override void ToStream(Stream output)
        //{
        //    output.Write(TLUtils.SignatureToBytes(4294967066u));
        //    this.IsEnabled.ToStream(output);
        //    this.Server.ToStream(output);
        //    this.Port.ToStream(output);
        //    this.Username.ToStream(output);
        //    this.Password.ToStream(output);
        //}

        public override string ToString()
        {
            return string.Format("TLProxyConfig server={0} port={1} username={2} password={3}", Server, Port, Username, Password );
        }

        public static TLProxyConfig Empty
        {
            get
            {
                return new TLProxyConfig
                {
                    IsEnabled = false,
                    Server = string.Empty,
                    Port = -1,
                    Username = string.Empty,
                    Password = string.Empty
                };
            }
        }
    }
}
