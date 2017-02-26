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
            ConnectionLost?.Invoke(this, EventArgs.Empty);
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
        private NetworkConnectivityLevel? _connectivityLevel;
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
            _connectivityLevel = _profile != null ? _profile.GetNetworkConnectivityLevel() : (NetworkConnectivityLevel?)null;

            //var connectivityLevel = _profile.GetNetworkConnectivityLevel();
            //_profile.NetworkAdapter.IanaInterfaceType != 71 // mobile data
            //_profile.GetConnectionCost().Roaming;

            //Helpers.Execute.ShowDebugMessage(string.Format("InternetConnectionProfile={0}", _profile != null ? _profile.GetNetworkConnectivityLevel().ToString() : "null"));
            
            // new solution
            NetworkInformation.NetworkStatusChanged += sender =>
            {
                var previousProfile = _profile;
                var previousConnectivityLevel = _connectivityLevel;

                _profile = NetworkInformation.GetInternetConnectionProfile();
                _connectivityLevel = _profile != null ? _profile.GetNetworkConnectivityLevel() : (NetworkConnectivityLevel?)null;

                if (_profile != null)
                {
                    if (_mtProtoService == null) return;

                    var activeTransport = _mtProtoService.GetActiveTransport();
                    if (activeTransport == null) return;
                    if (activeTransport.AuthKey == null) return;

                    var transportId = activeTransport.Id;

                    var isAuthorized = SettingsHelper.IsAuthorized;
                    if (!isAuthorized)
                    {
                        return;
                    }

                    var errorDebugString = string.Format("{0} internet connected", DateTime.Now.ToString("HH:mm:ss.fff"));
                    TLUtils.WriteLine(errorDebugString, LogSeverity.Error);

                    var reconnect = _connectivityLevel == NetworkConnectivityLevel.InternetAccess && previousConnectivityLevel != NetworkConnectivityLevel.InternetAccess;
                    if (reconnect)
                    {
                        TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " reconnect t" + transportId, LogSeverity.Error);

                        Logs.Log.Write(string.Format("  Reconnect reason=NetworkStatusChanged profile={0} internet_access={1} previous_profile={2} previous_internet_access={3}",
                            _profile != null ? _profile.ProfileName : "none",
                            _profile != null ? _connectivityLevel.ToString() : "none",
                            previousProfile != null ? previousProfile.ProfileName : "none",
                            previousProfile != null ? previousConnectivityLevel.ToString() : "none"));

                        RaiseConnectionFailed();

                        return;
                    }
                }
                else
                {
                    var errorDebugString = string.Format("{0} internet disconnected", DateTime.Now.ToString("HH:mm:ss.fff"));
                    TLUtils.WriteLine(errorDebugString, LogSeverity.Error);

                    _mtProtoService.SetMessageOnTime(60.0 * 60, "Waiting for network...");
                    //Helpers.Execute.ShowDebugMessage(string.Format("NetworkStatusChanged Internet disconnected Profile={0}", _profile));
                }
            };
#endif

#if WINDOWS_PHONE
            // old solution
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
//#if !WIN_RT && DEBUG
//            Microsoft.Devices.VibrateController.Default.Start(TimeSpan.FromMilliseconds(50));
//#endif

            if (Debugger.IsAttached) return;
//#if DEBUG
//            return;
//#endif

            if (_mtProtoService == null) return;
            
            var activeTransport = _mtProtoService.GetActiveTransport();
            if (activeTransport == null) return;
            if (activeTransport.AuthKey == null) return;

            var transportId = activeTransport.Id;

            var isAuthorized = SettingsHelper.IsAuthorized;
            if (!isAuthorized)
            {
                return;
            }

            var connectionFailed = false;
            var now = DateTime.Now;
            if (activeTransport.LastReceiveTime.HasValue)
            {
                connectionFailed = Math.Abs((now - activeTransport.LastReceiveTime.Value).TotalSeconds) > Constants.TimeoutInterval;
                if (connectionFailed)
                {
                    Logs.Log.Write(string.Format("  Reconnect reason=ConnectionFailed transport={3} now={0} - last_receive_time={1} > timeout={2}", now.ToString("dd-MM-yyyy HH:mm:ss.fff"), activeTransport.LastReceiveTime.Value.ToString("dd-MM-yyyy HH:mm:ss.fff"), Constants.TimeoutInterval, activeTransport.Id));
                }
            }
            else
            {
                if (activeTransport.FirstSendTime.HasValue)
                {
                    connectionFailed = Math.Abs((now - activeTransport.FirstSendTime.Value).TotalSeconds) > Constants.TimeoutInterval;
                    if (connectionFailed)
                    {
                        Logs.Log.Write(string.Format("  Reconnect reason=ConnectionFailed transport={3} now={0} - first_send_time={1} > timeout={2}", now.ToString("dd-MM-yyyy HH:mm:ss.fff"), activeTransport.FirstSendTime.Value.ToString("dd-MM-yyyy HH:mm:ss.fff"), Constants.TimeoutInterval, activeTransport.Id));
                    }
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
            var pingTimeout = Math.Max(Constants.TimeoutInterval - 10.0, 10.0);
            if (activeTransport.LastReceiveTime.HasValue)
            {
                // что-то уже получали по соединению
                var lastReceiveTime = activeTransport.LastReceiveTime.Value;
                timeFromLastReceive = Math.Abs((now - lastReceiveTime).TotalSeconds);

                pingRequired = timeFromLastReceive > pingTimeout;
                if (pingRequired)
                {
                    Logs.Log.Write(string.Format("  CheckReconnect reason=PingRequired transport={3} now={0} - last_receive_time={1} > ping_timeout={2}", now.ToString("HH:mm:ss.fff"), lastReceiveTime.ToString("HH:mm:ss.fff"), pingTimeout, activeTransport.Id));
                }
            }
            else
            {
                // ничего не получали, но что-то отправляли
                if (activeTransport.FirstSendTime.HasValue)
                {
                    var firstSendTime = activeTransport.FirstSendTime.Value;
                    timeFromFirstSend = Math.Abs((now - firstSendTime).TotalSeconds);

                    pingRequired = timeFromFirstSend > pingTimeout;
                    if (pingRequired)
                    {
                        Logs.Log.Write(string.Format("  CheckReconnect reason=PingRequired transport={3} now={0} - first_send_time={1} > ping_timeout={2}", now.ToString("HH:mm:ss.fff"), firstSendTime.ToString("HH:mm:ss.fff"), pingTimeout, activeTransport.Id));
                    }
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
                var pingIdHash = pingId % 1000;

                var debugString = string.Format("{0} ping t{1} ({2}, {3}) [{4}]", 
                    DateTime.Now.ToString("HH:mm:ss.fff"),
                    transportId, 
                    timeFromFirstSend.ToString("N"), 
                    timeFromLastReceive.ToString("N"), 
                    pingIdHash);

                TLUtils.WriteLine(debugString, LogSeverity.Error);
                _mtProtoService.PingCallback(pingId, //35,
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
