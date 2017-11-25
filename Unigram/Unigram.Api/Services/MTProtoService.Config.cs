using System;
using System.Linq;
using Telegram.Api.Services.Connection;
using Telegram.Api.TL;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void SaveConfig()
        {
            _cacheService.SetConfig(_config);
        }

        public TLConfig LoadConfig()
        {
            throw new NotImplementedException();
        }

#if DEBUG
        public void CheckPublicConfig()
        {
            OnCheckConfig(null, null);
        }
#endif

        private readonly IPublicConfigService _publicConfigService;

        private void LogPublicConfig(string str)
        {
            Logs.Log.Write(string.Format("  MTProtoService.CheckConfig {0}", str));
        }

        private void OnCheckConfig(object sender, EventArgs e)
        {
            _publicConfigService.GetAsync(
                configSimple =>
                {
                    if (configSimple != null)
                    {
                        var now = TLUtils.DateToUniversalTimeTLInt(ClientTicksDelta, DateTime.Now);
                        if (configSimple.Expires < now || now < configSimple.Date )
                        {
                            LogPublicConfig(string.Format("Config expired date={0} expires={1} now={2}", configSimple.Date, configSimple.Expires, now));
                            return;
                        }

                        var dcId = configSimple.DCId;
                        var ipPort = configSimple.IpPortList.FirstOrDefault();
                        if (ipPort == null)
                        {
                            LogPublicConfig("ipPort is null");
                            return;
                        }

                        var dcOption = _config.DCOptions.FirstOrDefault(x => x.IsValidIPv4Option(dcId));
                        if (dcOption == null)
                        {
                            LogPublicConfig("dcOption is null");
                            return;
                        }

                        bool isCreated;

                        var transport = _transportService.GetSpecialTransport(ipPort.GetIpString(), ipPort.Port, Type, out isCreated); 
                            //(dcOption.IpAddress.ToString(), dcOption.Port.Value, Type, out isCreated);  
                            //ipPort.GetIpString(), ipPort.Port.Value, Type, out isCreated);

                        if (isCreated)
                        {
                            transport.DCId = dcId;
                            transport.AuthKey = dcOption.AuthKey;
                            transport.Salt = dcOption.Salt;
                            transport.SessionId = TLLong.Random();
                            transport.SequenceNumber = 0;
                            transport.ClientTicksDelta = dcOption.ClientTicksDelta;
                            transport.PacketReceived += OnPacketReceivedByTransport;
                        }

                        if (transport.AuthKey == null)
                        {
                            LogPublicConfig(string.Format("Init transport id={0} dc_id={1} ip={2} port={3} proxy=[{4}]", transport.Id, transport.DCId, transport.Host, transport.Port, transport.ProxyConfig));
                            InitTransportAsync(transport,
                                tuple =>
                                {
                                    LogPublicConfig(string.Format("Init transport completed id={0}", transport.Id));
                                    lock (transport.SyncRoot)
                                    {
                                        transport.AuthKey = tuple.Item1;
                                        transport.Salt = tuple.Item2;
                                        transport.SessionId = tuple.Item3;

                                        transport.IsInitializing = false;
                                    }
                                    var authKeyId = TLUtils.GenerateLongAuthKeyId(tuple.Item1);

                                    lock (_authKeysRoot)
                                    {
                                        if (!_authKeys.ContainsKey(authKeyId))
                                        {
                                            _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = tuple.Item1, AutkKeyId = authKeyId });
                                        }
                                    }

                                    CheckAndUpdateMainTransportAsync(transport);
                                },
                                error =>
                                {
                                    LogPublicConfig(string.Format("Init transport error id={0} error={1}", transport.Id, error));
                                });
                        }
                        else
                        {
                            CheckAndUpdateMainTransportAsync(transport);
                        }
                    }
                }
                ,
                error =>
                {
                    LogPublicConfig(string.Format("PublicConfigService.GetAsync error {0}", error));
                });
        }

        public static void ReplaceIP(TLConfig config, string oldIp, string newIp)
        {
#if DEBUG
            if (config == null) return;

            foreach (var option in config.DCOptions)
            {
                if (string.Equals(option.IpAddress, oldIp, StringComparison.OrdinalIgnoreCase))
                {
                    option.IpAddress = newIp;
                }
            }
#endif
        }

        private void CheckAndUpdateMainTransportAsync(ITransport transport)
        {
            LogPublicConfig(string.Format("Get config from id={0} dc_id={1} ip={2} port={3} proxy=[{4}]", transport.Id, transport.DCId, transport.Host, transport.Port, transport.ProxyConfig));
            GetConfigByTransportAsync(transport,
                config =>
                {
                    LogPublicConfig(string.Format("Get config completed id={0}", transport.Id));

                    //ReplaceIP(config, "149.154.175.50", "149.154.175.51");

                    var dcId = _activeTransport.DCId;
                    var dcOption = config.DCOptions.FirstOrDefault(x => x.IsValidIPv4Option(dcId));
                    if (dcOption == null)
                    {
                        LogPublicConfig(string.Format("dcOption is null id={0}", transport.Id));
                        return;
                    }
                    LogPublicConfig("Close transport id=" + transport.Id);
                    transport.Close();

                    // replace main dc ip and port
                    var isCreated = false;
                    transport = _transportService.GetSpecialTransport(dcOption.IpAddress, dcOption.Port, Type, out isCreated);
                    if (isCreated)
                    {
                        transport.DCId = _activeTransport.DCId;
                        transport.AuthKey = _activeTransport.AuthKey;
                        //transport.IsAuthorized = (_activeTransport != null && _activeTransport.DCId == dcOption.Id.Value) || dcOption.IsAuthorized;
                        transport.Salt = _activeTransport.Salt;
                        transport.SessionId = TLLong.Random();
                        transport.SequenceNumber = 0;
                        transport.ClientTicksDelta = _activeTransport.ClientTicksDelta;
                        transport.PacketReceived += OnPacketReceivedByTransport;
                    }

                    LogPublicConfig(string.Format("Ping id={0} dc_id={1} ip={2} port={3} proxy=[{4}]", transport.Id, transport.DCId, transport.Host, transport.Port, transport.ProxyConfig));
                    PingByTransportAsync(transport, TLLong.Random(),
                        pong =>
                        {
                            LogPublicConfig(string.Format("Ping completed id={0}", transport.Id));

                            LogPublicConfig("Close transport id=" + transport.Id);
                            transport.Close();

                            LogPublicConfig(string.Format("Update info ip={0} port={1}", dcOption.IpAddress, dcOption.Port));
                            UpdateTransportInfoAsync(_activeTransport.DCId, dcOption.IpAddress.ToString(), dcOption.Port,
                                result =>
                                {
                                    LogPublicConfig("Update info completed");
                                });
                        },
                        error2 =>
                        {
                            LogPublicConfig(string.Format("Ping error id={0} error={1}", transport.Id, error2));
                        });

                    // reconnect
                },
                error2 =>
                {
                    LogPublicConfig(string.Format("Get config error id={0} error={1}", transport.Id, error2));
                });
        }
    }
}
