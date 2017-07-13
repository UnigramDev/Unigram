//#define DEBUG_UPDATEDCOPTIONS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using Telegram.Api.Services.DeviceInfo;
#if WIN_RT
using Windows.UI.Xaml;
#elif WINDOWS_PHONE
using System.Windows.Threading;
using Microsoft.Devices;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Connection;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Logs;
using Environment = System.Environment;
using Telegram.Api.TL.Messages.Methods;
using Telegram.Api.TL.Auth.Methods;
using Telegram.Api.TL.Upload.Methods;
using Telegram.Api.TL.Messages;
using Telegram.Api.Native.TL;
using Telegram.Api.Native;

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
            GotUserCountry?.Invoke(this, new CountryEventArgs { Country = country });
        }

        public void SetInitState()
        {
            _updatesService.SetInitState();
        }

        public string Country
        {
            //get { return _config?.Country; }
            get { return "IT"; }
        }

        public int CurrentUserId
        {
            get
            {
                return ConnectionManager.Instance.UserId;
            }
            set
            {
                ConnectionManager.Instance.UserId = value;
            }
        }

        public NetworkType NetworkType
        {
            get
            {
                return _connectionService.NetworkType;
            }
        }

        public long ClientTicksDelta { get { return ConnectionManager.Instance.TimeDifference; } }

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

        private Timer _deviceLockedTimer;
        private Timer _checkTransportTimer;

        public MTProtoService(IDeviceInfoService deviceInfo, IUpdatesService updatesService, ICacheService cacheService, IConnectionService connectionService, IStatsService statsService)
        {
            var isBackground = deviceInfo != null && deviceInfo.IsBackground;

            CurrentUserId = SettingsHelper.UserId;

            _statsService = statsService;

            _deviceInfo = deviceInfo;

            _deviceLockedTimer = new Timer(CheckDeviceLockedInternal, this, TimeSpan.FromSeconds(60.0), TimeSpan.FromSeconds(60.0));

            _connectionService = connectionService;
            _connectionService.Initialize(this);
            //_connectionService.ConnectionLost += OnConnectionLost;

            var sendStatusEvents = Observable.FromEventPattern<EventHandler<SendStatusEventArgs>, SendStatusEventArgs>(
                keh => { SendStatus += keh; },
                keh => { SendStatus -= keh; });

            _statusSubscription = sendStatusEvents
                .Throttle(TimeSpan.FromSeconds(Constants.UpdateStatusInterval))
                .Subscribe(e => UpdateStatusAsync(e.EventArgs.Offline, result => { }));

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
            }

            var connectionManager = ConnectionManager.Instance;
            //connectionManager.CurrentNetworkTypeChanged += ConnectionManager_CurrentNetworkTypeChanged;
            //connectionManager.ConnectionStateChanged += ConnectionManager_ConnectionStateChanged;
            connectionManager.UnprocessedMessageReceived += ConnectionManager_UnprocessedMessageReceived;
            connectionManager.AuthenticationRequired += ConnectionManager_AuthenticationRequired;
            connectionManager.UserConfigurationRequired += ConnectionManager_UserConfigurationRequired;

            Current = this;
        }

        private void ConnectionManager_CurrentNetworkTypeChanged(ConnectionManager sender, object args)
        {
            throw new NotImplementedException();
        }

        private void ConnectionManager_ConnectionStateChanged(ConnectionManager sender, object args)
        {
            throw new NotImplementedException();
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

        public event EventHandler<SendStatusEventArgs> SendStatus;

        public void RaiseSendStatus(SendStatusEventArgs e)
        {
            SendStatus?.Invoke(this, e);
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
