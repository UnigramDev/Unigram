//#define DEBUG_UPDATEDCOPTIONS
using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading;
using Telegram.Api.Services.DeviceInfo;
using Windows.UI.Xaml;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Connection;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.Native.TL;
using Telegram.Api.Native;
using Telegram.Api.TL.Help.Methods;

namespace Telegram.Api.Services
{
    public class CountryEventArgs : EventArgs
    {
        public string Country { get; set; }
    }

    public partial class MTProtoService : ServiceBase, IMTProtoService, IDisposable
    {
        public event EventHandler<CountryEventArgs> GotUserCountry;

        protected void RaiseGotUserCountry(string country)
        {
            _country = country;
            GotUserCountry?.Invoke(this, new CountryEventArgs { Country = country });
        }

        public void SetInitState()
        {
            _updatesService.SetInitState();
        }

        private string _country;
        public string Country => _country;

        public int CurrentUserId
        {
            get
            {
                return _connectionManager.UserId;
            }
            set
            {
                _connectionManager.UserId = value;
            }
        }

        public NetworkType NetworkType
        {
            get
            {
                return _connectionService.NetworkType;
            }
        }

        public long ClientTicksDelta { get { return _connectionManager.TimeDifference; } }

        //private bool _isInitialized;

        /// <summary>
        /// Получен ли ключ авторизации
        /// </summary>
        //public bool IsInitialized
        //{
        //    get { return _isInitialized; }
        //    protected set
        //    {
        //        if (_isInitialized != value)
        //        {
        //            _isInitialized = value;
        //            RaisePropertyChanged(() => IsInitialized);
        //        }
        //    }
        //}

        public event EventHandler Initialized;

        protected virtual void RaiseInitialized()
        {
            Initialized?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler InitializationFailed;

        protected virtual void RaiseInitializationFailed()
        {
            InitializationFailed?.Invoke(this, EventArgs.Empty);
        }

        private readonly ICacheService _cacheService;
        private readonly IUpdatesService _updatesService;
        private readonly IConnectionService _connectionService;
        private readonly IDeviceInfoService _deviceInfo;
        private readonly IStatsService _statsService;
        private readonly ConnectionManager _connectionManager;

        private bool _connectionLost;

        private Timer _deviceLockedTimer;
        private Timer _checkTransportTimer;

        public MTProtoService(int account, IDeviceInfoService deviceInfo, IUpdatesService updatesService, ICacheService cacheService, IConnectionService connectionService, IStatsService statsService)
        {
            var isBackground = deviceInfo != null && deviceInfo.IsBackground;

            _connectionManager = ConnectionManager.Instance;
            _connectionManager.Logger = new Logs.Log();
            _connectionManager.CurrentNetworkTypeChanged += ConnectionManager_CurrentNetworkTypeChanged;
            _connectionManager.ConnectionStateChanged += ConnectionManager_ConnectionStateChanged;
            _connectionManager.UnprocessedMessageReceived += ConnectionManager_UnprocessedMessageReceived;
            _connectionManager.AuthenticationRequired += ConnectionManager_AuthenticationRequired;
            _connectionManager.UserConfigurationRequired += ConnectionManager_UserConfigurationRequired;
            _connectionManager.SessionCreated += ConnectionManager_SessionCreated;

            CurrentUserId = SettingsHelper.UserId;

            _statsService = statsService;

            _deviceInfo = deviceInfo;

            _deviceLockedTimer = new Timer(CheckDeviceLockedInternal, this, TimeSpan.FromSeconds(60.0), TimeSpan.FromSeconds(60.0));

            _connectionService = connectionService;
            _connectionService.Initialize(this);
            //_connectionService.ConnectionLost += OnConnectionLost;

            var sendStatusEvents = Observable.FromEventPattern<EventHandler<bool>, bool>(
                keh => { SendStatus += keh; },
                keh => { SendStatus -= keh; });

            _statusSubscription = sendStatusEvents
                .Throttle(TimeSpan.FromSeconds(Constants.UpdateStatusInterval))
                .Subscribe(e => UpdateStatusAsync(e.EventArgs, result => { }));

            _cacheService = cacheService;

            _updatesService = updatesService;

            if (_updatesService != null)
            {
                //_updatesService.DCOptionsUpdated += OnDCOptionsUpdated;

                _updatesService.GetDifferenceAsync = GetDifferenceAsync;
                _updatesService.GetStateAsync = GetStateAsync;
                _updatesService.GetCurrentUserId = GetCurrentUserId;
                _updatesService.GetDHConfigAsync = GetDHConfigAsync;
                //_updatesService.AcceptEncryptionAsync = AcceptEncryptionAsync;
                _updatesService.SendEncryptedServiceAsync = SendEncryptedServiceAsync;
                _updatesService.SetMessageOnTimeAsync = SetMessageOnTime;
                _updatesService.RemoveFromQueue = RemoveFromQueue;
                _updatesService.UpdateChannelAsync = UpdateChannelAsync;
                _updatesService.GetParticipantAsync = GetParticipantAsync;
                _updatesService.GetFullChatAsync = GetFullChatAsync;
                _updatesService.GetFullUserAsync = GetFullUserAsync;
                _updatesService.GetChannelMessagesAsync = GetMessagesAsync;
                _updatesService.GetMessagesAsync = GetMessagesAsync;
            }

            if (!SettingsHelper.IsAuthorized)
            {
                SendInformativeMessage<TLNearestDC>("help.getNearestDc", new TLHelpGetNearestDC(), result =>
                {
                    RaiseGotUserCountry(result.Country);
                }, null, flags: RequestFlag.FailOnServerError | RequestFlag.WithoutLogin);
            }

            ToggleProxy();
            Current = this;
        }

        public void ToggleProxy()
        {
            if (SettingsHelper.IsProxyEnabled)
            {
                var server = SettingsHelper.ProxyServer;
                var port = (uint)SettingsHelper.ProxyPort;
                var username = SettingsHelper.ProxyUsername;
                var password = SettingsHelper.ProxyPassword;

                if (username == null || password == null)
                {
                    _connectionManager.ProxySettings = new ProxySettings(server, port);
                }
                else
                {
                    _connectionManager.ProxySettings = new ProxySettings(server, port, username, password);
                }
            }
            else
            {
                _connectionManager.ProxySettings = null;
            }
        }

        private void ConnectionManager_CurrentNetworkTypeChanged(ConnectionManager sender, object args)
        {
            if (sender.CurrentNetworkType == ConnectionNeworkType.None)
            {
                _connectionLost = true;
            }
        }

        private void ConnectionManager_ConnectionStateChanged(ConnectionManager sender, object args)
        {
            if (sender.ConnectionState == ConnectionState.Connected && _connectionLost)
            {
                //if (SettingsHelper.IsAuthorized)
                //{
                //    _connectionLost = false;
                //    _updatesService.LoadStateAndUpdate(() => Debug.WriteLine("State updated"));
                //}
            }
        }

        private void ConnectionManager_UnprocessedMessageReceived(ConnectionManager sender, MessageResponse args)
        {
            if (args.Object is TLConfig config)
            {
                _cacheService.SetConfig(config);
            }
            else if (args.Object is TLUpdatesBase updates)
            {
                _updatesService.ProcessUpdates(updates, true);
            }
            else
            {
                Debugger.Break();
            }
        }

        private void ConnectionManager_AuthenticationRequired(ConnectionManager sender, object args)
        {
            RaiseAuthorizationRequired(new AuthorizationRequiredEventArgs());
        }

        private void ConnectionManager_UserConfigurationRequired(ConnectionManager sender, UserConfiguration args)
        {
            args.AppId = Constants.ApiId;
        }

        private void ConnectionManager_SessionCreated(ConnectionManager sender, object args)
        {
            _updatesService.LoadStateAndUpdate(() => Debug.WriteLine("State updated"));
        }

        public static IMTProtoService Current { get; protected set; }



        private int GetCurrentUserId()
        {
            return CurrentUserId;
        }

        private void TryReadConfig(Action<bool> callback)
        {
            _cacheService.GetConfigAsync(
                config =>
                {
                    _config = config;
                    if (_config == null)
                    {
                        callback(false);
                        return;
                    }

                    callback(true);
                });
        }

        public event EventHandler<AuthorizationRequiredEventArgs> AuthorizationRequired;
        public void RaiseAuthorizationRequired(AuthorizationRequiredEventArgs args)
        {
            AuthorizationRequired?.Invoke(this, args);
        }

        private readonly IDisposable _statusSubscription;

        public event EventHandler<bool> SendStatus;
        public void RaiseSendStatus(bool e)
        {
            UpdateStatusAsync(e, result => { });
            //SendStatus?.Invoke(this, e);
        }

        public void Dispose()
        {
            _statusSubscription.Dispose();
        }

        private string _message;

        public string Message
        {
            get { return _message; }
            set
            {
                if (_message != value)
                {
                    _message = value;
                    RaisePropertyChanged(() => Message);
                }
            }
        }

        private DispatcherTimer _messageScheduler;

        public void SetMessageOnTime(double seconds, string message)
        {
            //Logs.Log.Write(string.Format("MTProtoService.SetMessageOnTime sec={0}, message={1}", seconds, message));

            Execute.BeginOnUIThread(() =>
            {
                if (_messageScheduler == null)
                {
                    _messageScheduler = new DispatcherTimer();
                    _messageScheduler.Tick += MessageScheduler_Tick;
                }

                _messageScheduler.Stop();
                Message = message;
                _messageScheduler.Interval = TimeSpan.FromSeconds(seconds);
                _messageScheduler.Start();
            });
        }

#if WINDOWS_PHONE
        private void MessageScheduler_Tick(object sender, EventArgs e)
#elif WIN_RT
        private void MessageScheduler_Tick(object sender, object args)
#endif
        {
            Message = string.Empty;
            _messageScheduler.Stop();
        }
    }

    public class AuthorizationRequiredEventArgs : EventArgs
    {
        public string MethodName { get; set; }

        public long AuthKeyId { get; set; }

        public TLRPCError Error { get; set; }
    }

    public class SendStatusEventArgs : EventArgs
    {
        public bool Offline { get; set; }

        public SendStatusEventArgs(bool offline)
        {
            Offline = offline;
        }
    }

    public class TransportCheckedEventArgs : EventArgs
    {
        public int TransportId { get; set; }
        public long? SessionId { get; set; }
        public byte[] AuthKey { get; set; }
        public DateTime? LastReceiveTime { get; set; }
        public int HistoryCount { get; set; }
        public int NextPacketLength { get; set; }
        public int LastPacketLength { get; set; }
        public string HistoryDescription { get; set; }
    }
}
