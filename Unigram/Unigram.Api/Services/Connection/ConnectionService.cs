using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using Telegram.Api.Services.DeviceInfo;
#if WP8 || WIN_RT
using Windows.Networking.Connectivity;
#endif
#if WINDOWS_PHONE
using Microsoft.Phone.Net.NetworkInformation;
#endif
using Telegram.Api.Helpers;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Connection
{
    public interface IConnectionService
    {
        void Initialize(IMTProtoService mtProtoService);
        event EventHandler ConnectionLost;
    }

    public class ConnectionService : IConnectionService
    {
        public event EventHandler ConnectionLost;

        protected virtual void RaiseConnectionFailed()
        {
            var handler = ConnectionLost;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private IMTProtoService _mtProtoService;

        public void Initialize(IMTProtoService mtProtoService)
        {
            _mtProtoService = mtProtoService;
        }

        private Timer _connectionScheduler;

        private bool _isNetworkAwailable;

#if WP8 || WIN_RT
        private ConnectionProfile _profile;
#endif

        public ConnectionService(IDeviceInfoService deviceInfoService)
        {
            if (deviceInfoService != null && deviceInfoService.IsBackground)
            {
                return;
            }

            _connectionScheduler = new Timer(CheckConnectionState, this, TimeSpan.FromSeconds(0.0), TimeSpan.FromSeconds(5.0));

#if WINDOWS_PHONE
            _isNetworkAwailable = DeviceNetworkInformation.IsNetworkAvailable;
#endif

#if WP8 || WIN_RT
            _profile = NetworkInformation.GetInternetConnectionProfile();
            //Helpers.Execute.ShowDebugMessage(string.Format("InternetConnectionProfile={0}", _profile != null ? _profile.GetNetworkConnectivityLevel().ToString() : "null"));
            NetworkInformation.NetworkStatusChanged += sender =>
            {
                var previousProfile = _profile;
                _profile = NetworkInformation.GetInternetConnectionProfile();

                if (previousProfile != _profile)
                {
                    if (_profile != null)
                    {
                        if (_mtProtoService == null) return;

                        var activeTransport = _mtProtoService.GetActiveTransport();
                        if (activeTransport == null) return;
                        if (activeTransport.AuthKey == null) return;

                        var transportId = activeTransport.Id;

                        var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
                        if (!isAuthorized)
                        {
                            return;
                        }

                        var reconnect = true;
                        var errorDebugString = string.Format("{0} internet connected",
                            DateTime.Now.ToString("HH:mm:ss.fff"));
                        TLUtils.WriteLine(errorDebugString, LogSeverity.Error);

                        //Helpers.Execute.ShowDebugMessage(string.Format("NetworkStatusChanged Internet connected Profile={0}", _profile));
                        if (reconnect)
                        {
                            TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " reconnect t" + transportId, LogSeverity.Error);

                            RaiseConnectionFailed();

                            return;
                        }
                    }
                    else
                    {
                        var errorDebugString = string.Format("{0} internet disconnected",
                            DateTime.Now.ToString("HH:mm:ss.fff"));
                        TLUtils.WriteLine(errorDebugString, LogSeverity.Error);

                        _mtProtoService.SetMessageOnTime(60.0*60, "Waiting for network...");
                        //Helpers.Execute.ShowDebugMessage(string.Format("NetworkStatusChanged Internet disconnected Profile={0}", _profile));
                    }
                }
            };
#endif

#if WINDOWS_PHONE
            DeviceNetworkInformation.NetworkAvailabilityChanged += (sender, args) =>
            {
                return;

                var isNetworkAvailable = _isNetworkAwailable;
                
                _isNetworkAwailable = DeviceNetworkInformation.IsNetworkAvailable;
                //if (isNetworkAvailable != _isNetworkAwailable)
                {
                    var info = new StringBuilder();
                    info.AppendLine();
                    foreach (var networkInterface in new NetworkInterfaceList())
                    {
                        info.AppendLine(string.Format(" {0} {1} {2}", 
                            networkInterface.InterfaceName,
                            networkInterface.InterfaceState,
                            networkInterface.InterfaceType));
                    }

                    var current = new NetworkInterfaceList();
                    var ni = NetworkInterface.NetworkInterfaceType;
                    Helpers.Execute.ShowDebugMessage(string.Format("NetworkAwailabilityChanged Interface={0}\n{1}", ni, info.ToString()));
                }

                var networkString = string.Format("{0}, {1}, ", args.NotificationType,
                    args.NetworkInterface != null ? args.NetworkInterface.InterfaceState.ToString() : "none");

                var mtProtoService = MTProtoService.Instance;
                if (mtProtoService != null)
                {
                    if (args.NotificationType == NetworkNotificationType.InterfaceDisconnected)
                    {
#if DEBUG
                        var interfaceSubtype = args.NetworkInterface != null
                            ? args.NetworkInterface.InterfaceSubtype.ToString()
                            : "Interface";
                        mtProtoService.SetMessageOnTime(2.0, DateTime.Now.ToString("HH:mm:ss.fff ", CultureInfo.InvariantCulture) + interfaceSubtype + " disconnected...");
                        TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff ", CultureInfo.InvariantCulture) + interfaceSubtype + " disconnected", LogSeverity.Error);
#else
                        //mtProtoService.SetMessageOnTime(2.0, "No Internet connection");
#endif
                    }
                    else if (args.NotificationType == NetworkNotificationType.InterfaceConnected)
                    {
#if DEBUG
                        var interfaceSubtype = args.NetworkInterface != null
                            ? args.NetworkInterface.InterfaceSubtype.ToString()
                            : "Interface";
                        mtProtoService.SetMessageOnTime(2.0, DateTime.Now.ToString("HH:mm:ss.fff ", CultureInfo.InvariantCulture) + interfaceSubtype + " connected...");
                        TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff ", CultureInfo.InvariantCulture) + interfaceSubtype + " connected", LogSeverity.Error);
#else
                        mtProtoService.SetMessageOnTime(0.0, string.Empty);
#endif
                    }
                    else if (args.NotificationType == NetworkNotificationType.CharacteristicUpdate)
                    {
                        //#if DEBUG

                        //                        mtProtoService.SetMessageOnTime(2.0, "Characteristic update...");
                        //                        var networkInterface = args.NetworkInterface;
                        //                        if (networkInterface != null)
                        //                        {
                        //                            var characteristics = new StringBuilder();
                        //                            characteristics.AppendLine();
                        //                            //characteristics.AppendLine("Description=" + networkInterface.Description);
                        //                            characteristics.AppendLine("InterfaceName=" + networkInterface.InterfaceName);
                        //                            characteristics.AppendLine("InterfaceState=" + networkInterface.InterfaceState);
                        //                            characteristics.AppendLine("InterfaceType=" + networkInterface.InterfaceType);
                        //                            characteristics.AppendLine("InterfaceSubtype=" + networkInterface.InterfaceSubtype);
                        //                            characteristics.AppendLine("Bandwidth=" + networkInterface.Bandwidth);
                        //                            //characteristics.AppendLine("Characteristics=" + networkInterface.Characteristics);
                        //                            TLUtils.WriteLine(characteristics.ToString(), LogSeverity.Error);
                        //                        }
                        //#endif
                    }
                }

            };
#endif


        }

        private void CheckConnectionState(object state)
        {
            if (Debugger.IsAttached) return;
//#if DEBUG
//            return;
//#endif

            if (_mtProtoService == null) return;
            
            var activeTransport = _mtProtoService.GetActiveTransport();
            if (activeTransport == null) return;
            if (activeTransport.AuthKey == null) return;

            var transportId = activeTransport.Id;

            var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
            if (!isAuthorized)
            {
                return;
            }

            var connectionFailed = false;
            var now = DateTime.Now;
            if (activeTransport.LastReceiveTime.HasValue)
            {
                connectionFailed = Math.Abs((now - activeTransport.LastReceiveTime.Value).TotalSeconds) > Constants.TimeoutInterval;
            }
            else
            {
                if (activeTransport.FirstSendTime.HasValue)
                {
                    connectionFailed = Math.Abs((now - activeTransport.FirstSendTime.Value).TotalSeconds) > Constants.TimeoutInterval;
                }
            }

            if (connectionFailed)
            {
                RaiseConnectionFailed();
                TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " reconnect t" + transportId, LogSeverity.Error);
                return;
            }

            var pingRequired = false;
            var timeFromLastReceive = 0.0;
            var timeFromFirstSend = 0.0;
            if (activeTransport.LastReceiveTime.HasValue)
            {
                // что-то уже получали по соединению
                var lastReceiveTime = activeTransport.LastReceiveTime.Value;
                timeFromLastReceive = Math.Abs((now - lastReceiveTime).TotalSeconds);

                pingRequired = timeFromLastReceive > 15.0;
            }
            else
            {
                // ничего не получали, но что-то отправляли
                if (activeTransport.FirstSendTime.HasValue)
                {
                    var firstSendTime = activeTransport.FirstSendTime.Value;
                    timeFromFirstSend = Math.Abs((now - firstSendTime).TotalSeconds);

                    pingRequired = timeFromFirstSend > 15.0;
                }
                // хотя бы пинганем для начала
                else
                {
                    pingRequired = true;
                }
            }

            if (pingRequired)
            {
                var pingId = TLLong.Random();
                var pingIdHash = pingId.Value % 1000;

                var debugString = string.Format("{0} ping t{1} ({2}, {3}) [{4}]", 
                    DateTime.Now.ToString("HH:mm:ss.fff"),
                    transportId, 
                    timeFromFirstSend.ToString("N"), 
                    timeFromLastReceive.ToString("N"), 
                    pingIdHash);

                TLUtils.WriteLine(debugString, LogSeverity.Error);
                _mtProtoService.PingAsync(pingId, //new TLInt(35),
                    result =>
                    {
                        var resultDebugString = string.Format("{0} pong t{1} ({2}, {3}) [{4}]",
                            DateTime.Now.ToString("HH:mm:ss.fff"),
                            transportId,
                            timeFromFirstSend.ToString("N"),
                            timeFromLastReceive.ToString("N"),
                            pingIdHash);

                        TLUtils.WriteLine(resultDebugString, LogSeverity.Error);
                    },
                    error =>
                    {
                        var errorDebugString = string.Format("{0} pong error t{1} ({2}, {3}) [{4}] \nSocketError={5}",
                            DateTime.Now.ToString("HH:mm:ss.fff"),
                            transportId,
                            timeFromFirstSend.ToString("N"),
                            timeFromLastReceive.ToString("N"),
                            pingIdHash,
#if WINDOWS_PHONE
                            error.SocketError
#else
                            string.Empty
#endif
                            );

                        TLUtils.WriteLine(errorDebugString, LogSeverity.Error);
                    });
            }
            else
            {
                var checkDebugString = string.Format("{0} check t{1} ({2}, {3})",
                    DateTime.Now.ToString("HH:mm:ss.fff"),
                    transportId,
                    timeFromFirstSend.ToString("N"),
                    timeFromLastReceive.ToString("N"));

                //TLUtils.WriteLine(checkDebugString, LogSeverity.Error);
            }
        }
    }
}
