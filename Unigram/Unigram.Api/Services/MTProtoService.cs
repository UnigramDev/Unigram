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
#endif
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Connection;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.Transport;
using Telegram.Logs;
using System.Threading.Tasks;
using Telegram.Api.TL.Methods.Upload;
using Telegram.Api.TL.Methods.Auth;
using Telegram.Api.TL.Methods.Messages;

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
            var handler = GotUserCountry;
            if (handler != null)
            {
                handler(this, new CountryEventArgs { Country = country });
            }
        }

        public void SetInitState()
        {
            _updatesService.SetInitState();
        }

        public ITransport GetActiveTransport()
        {
            return _activeTransport;
        }

        public Tuple<int, int, int> GetCurrentPacketInfo()
        {
            return _activeTransport != null ? _activeTransport.GetCurrentPacketInfo() : null;
        }

        public string GetTransportInfo()
        {
            return _activeTransport != null ? _activeTransport.GetTransportInfo() : null;
        }

        public string Country
        {
            get { return _config != null ? _config.Country : null; }
        }

        public int CurrentUserId { get; set; }

        public long ClientTicksDelta { get { return _activeTransport.ClientTicksDelta; } }

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
        //            NotifyOfPropertyChange(() => IsInitialized);
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

        private readonly object _fileTransportRoot = new object();

        private readonly object _activeTransportRoot = new object();

        private ITransport _activeTransport;

        private readonly ITransportService _transportService;

        private readonly ICacheService _cacheService;

        private readonly IUpdatesService _updatesService;

        private readonly IConnectionService _connectionService;

        private readonly IDeviceInfoService _deviceInfo;

        private readonly Dictionary<long, HistoryItem> _history = new Dictionary<long, HistoryItem>();

        public IList<HistoryItem> History
        {
            get { return _history.Values.ToList(); }
        }

        private TransportType _type = TransportType.Tcp;

        public TransportType Type
        {
            get { return _type; }
            set { _type = value; }
        }

        private readonly object _delayedItemsRoot = new object();

        /// <summary>
        /// Отложенные запросы, будут выполнены сразу же после получения авторизационного ключа
        /// </summary>
        private readonly List<DelayedItem> _delayedItems = new List<DelayedItem>();

        private Timer _timeoutsTimer;

        private Timer _deviceLockedTimer;

        public MTProtoService(IDeviceInfoService deviceInfo, IUpdatesService updatesService, ICacheService cacheService, ITransportService transportService, IConnectionService connectionService)
        {
            var isBackground = deviceInfo != null && deviceInfo.IsBackground;

            _deviceInfo = deviceInfo;

            _sendingTimer = new Timer(CheckSendingMessages, this, Timeout.Infinite, Timeout.Infinite);
            _getConfigTimer = new Timer(CheckGetConfig, this, TimeSpan.FromSeconds(10.0), TimeSpan.FromSeconds(Constants.CheckGetConfigInterval));
            _timeoutsTimer = new Timer(CheckTimeouts, this, TimeSpan.FromSeconds(10.0), TimeSpan.FromSeconds(10.0));
            _deviceLockedTimer = new Timer(CheckDeviceLockedInternal, this, TimeSpan.FromSeconds(60.0), TimeSpan.FromSeconds(60.0));

            _connectionService = connectionService;
            _connectionService.Initialize(this);
            _connectionService.ConnectionLost += OnConnectionLost;

            var sendStatusEvents = Observable.FromEventPattern<EventHandler<SendStatusEventArgs>, SendStatusEventArgs>(
                keh => { SendStatus += keh; },
                keh => { SendStatus -= keh; });

            //_statusSubscription = sendStatusEvents
            //    .Throttle(TimeSpan.FromSeconds(Constants.UpdateStatusInterval))
            //    .Subscribe(e => UpdateStatusAsync(e.EventArgs.Offline, result => { }));

            _updatesService = updatesService;
            _updatesService.DCOptionsUpdated += OnDCOptionsUpdated;

            _cacheService = cacheService;

            if (_updatesService != null)
            {
                //_updatesService.GetDifferenceAsync = GetDifferenceAsync;
                //_updatesService.GetStateAsync = GetStateAsync;
                //_updatesService.GetCurrentUserId = GetCurrentUserId;
                //_updatesService.GetDHConfigAsync = GetDHConfigAsync;
                //_updatesService.AcceptEncryptionAsync = AcceptEncryptionAsync;
                //_updatesService.SendEncryptedServiceAsync = SendEncryptedServiceAsync;
                //_updatesService.SetMessageOnTimeAsync = SetMessageOnTime;
                //_updatesService.RemoveFromQueue = RemoveFromQueue;
                //_updatesService.UpdateChannelAsync = UpdateChannelAsync;
                //_updatesService.GetParticipantAsync = GetParticipantAsync;
                //_updatesService.GetFullChatAsync = GetFullChatAsync;
                _updatesService.GetDifferenceAsync = GetDifferenceCallbackAsync;
                _updatesService.GetStateAsync = GetStateCallbackAsync;
                _updatesService.GetCurrentUserId = GetCurrentUserId;
                _updatesService.GetDHConfigAsync = GetDHConfigCallbackAsync;
                _updatesService.AcceptEncryptionAsync = AcceptEncryptionCallbackAsync;
                _updatesService.SendEncryptedServiceAsync = SendEncryptedServiceCallbackAsync;
                _updatesService.SetMessageOnTimeAsync = SetMessageOnTimeAsync;
                _updatesService.RemoveFromQueue = RemoveFromQueue;
                _updatesService.UpdateChannelAsync = UpdateChannelCallbackAsync;
                _updatesService.GetParticipantAsync = GetParticipantCallbackAsync;
                _updatesService.GetFullChatAsync = GetFullChatCallbackAsync;

            }

            _transportService = transportService;
            lock (_activeTransportRoot)
            {
                var transportDCId = _activeTransport != null ? _activeTransport.DCId : Constants.FirstServerDCId;
                var transportKey = _activeTransport != null ? _activeTransport.AuthKey : null;
                var transportSalt = _activeTransport != null ? _activeTransport.Salt : null;
                var transportSessionId = _activeTransport != null ? _activeTransport.SessionId : null;
                var transportSequenceNumber = _activeTransport != null ? _activeTransport.SequenceNumber : 0;
                var transportClientTicksDelta = _activeTransport != null ? _activeTransport.ClientTicksDelta : 0;
                bool isCreated;
                _activeTransport = _transportService.GetTransport(Constants.FirstServerIpAddress, Constants.FirstServerPort, Type, out isCreated);
                if (isCreated)
                {
                    _activeTransport.DCId = Constants.FirstServerDCId;
                    _activeTransport.AuthKey = transportKey;
                    _activeTransport.Salt = transportSalt;
                    _activeTransport.SessionId = transportSessionId;
                    _activeTransport.SequenceNumber = transportSequenceNumber;
                    _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                    _activeTransport.PacketReceived += OnPacketReceived;
                }
            }

            Initialized += OnServiceInitialized;
            InitializationFailed += OnServiceInitializationFailed;

            //IsInitialized = true;
            if (!isBackground)
            {
                Initialize();
            }

            Current = this;
        }

        public static IMTProtoService Current { get; protected set; }

        private void CheckTimeouts(object state)
        {
#if DEBUG
            if (Debugger.IsAttached)
            {
                return;
            }
#endif

            const double timeout = Constants.TimeoutInterval;
            const double delayedTimeout = Constants.DelayedTimeoutInterval;
            const double nonEncryptedTimeout = Constants.NonEncryptedTimeoutInterval;

            var timedOutKeys = new List<long>();
            var timedOutValues = new List<HistoryItem>();
            var now = DateTime.Now;

            // history
            lock (_historyRoot)
            {
                foreach (var historyKeyValue in _history)
                {
                    var historyValue = historyKeyValue.Value;
                    if (historyValue.SendTime != default(DateTime)
                        && historyValue.SendTime.AddSeconds(timeout) < now)
                    {
                        timedOutKeys.Add(historyKeyValue.Key);
                        timedOutValues.Add(historyKeyValue.Value);
                    }
                }

                foreach (var key in timedOutKeys)
                {
                    _history.Remove(key);
                }
            }

            if (timedOutValues.Count > 0)
            {
                Execute.BeginOnThreadPool(() =>
                {
                    foreach (var item in timedOutValues)
                    {
                        try
                        {
                            item.FaultCallback.SafeInvoke(new TLRPCError { ErrorCode = 408, ErrorMessage = $"MTProtoService: operation timed out ({timeout}s)" });
#if DEBUG
                            TLUtils.WriteLine(item.Caption + " time out", LogSeverity.Error);
#endif
                        }
                        catch (Exception ex)
                        {
                            TLUtils.WriteException("Timeout exception", ex);
                        }
                    }
                });
            }

            // delayed history
            var timedOutItems = new List<DelayedItem>();
            lock (_delayedItemsRoot)
            {
                foreach (var item in _delayedItems)
                {
                    if (item.SendTime != default(DateTime)
                        && item.SendTime.AddSeconds(delayedTimeout) < now)
                    {
                        timedOutItems.Add(item);
                    }
                }

                foreach (var item in timedOutItems)
                {
                    _delayedItems.Remove(item);
                }
            }

            if (timedOutItems.Count > 0)
            {
                Execute.BeginOnThreadPool(() =>
                {
                    foreach (var item in timedOutItems)
                    {
                        try
                        {
                            //item.FaultCallback.SafeInvoke(
                            //    new TLRPCError
                            //    {
                            //        Code = new int?((int)ErrorCode.TIMEOUT),
                            //        Message = new string("MTProtoService: operation timed out (" + delayedTimeout + "s)")
                            //    });
#if DEBUG
                            TLUtils.WriteLine(item.Caption + " time out", LogSeverity.Error);
#endif
                        }
                        catch (Exception ex)
                        {
                            TLUtils.WriteException("Timeout exception", ex);
                        }
                    }
                });
            }

            // generating key
            if (_activeTransport != null)
            {
                var requests = _activeTransport.RemoveTimeOutRequests(nonEncryptedTimeout);

                if (requests.Count > 0)
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog("MTProtoService.CheckTimeouts clear history and replace transport");
#endif
                    ClearHistory("CheckTimeouts", false);

                    Execute.BeginOnThreadPool(() =>
                    {
                        foreach (var item in requests)
                        {
                            try
                            {
                                item.FaultCallback.SafeInvoke(new TLRPCError { ErrorCode = 408, ErrorMessage = $"MTProtoService: operation timed out ({timeout}s)" });
#if DEBUG
                                TLUtils.WriteLine(item.Caption + " time out", LogSeverity.Error);
#endif
                            }
                            catch (Exception ex)
                            {
                                TLUtils.WriteException("Timeout exception", ex);
                            }
                        }
                    });
                }
            }
        }

        private void OnConnectionLost(object sender, EventArgs e)
        {
            ResetConnection(null);
        }

        public void GetConfigInformationAsync(Action<string> callback)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var now = DateTime.Now;
                var currentTime = TLUtils.DateToUniversalTimeTLInt(ClientTicksDelta, now);

                var activeTransportString = _activeTransport != null ? _activeTransport.ToString() : null;

                var sb = new StringBuilder();
                sb.AppendLine("current_time utc0:");
                sb.AppendLine(string.Format("{0} {1}", currentTime, TLUtils.ToDateTime(currentTime).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
                sb.AppendLine("config:");
                sb.AppendLine(_config.ToString());
                sb.AppendLine("active transport:");
                sb.AppendLine(activeTransportString);
                sb.AppendLine("dc_options:");
                foreach (var option in _config.DCOptions)
                {
                    sb.AppendLine(option.ToString());
                }

                callback(sb.ToString());
            });
        }

        public void GetTransportInformationAsync(Action<string> callback)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var activeTransportString = _activeTransport != null ? _activeTransport.ToString() : null;

                var sb = new StringBuilder();
                sb.AppendLine("active transport:");
                sb.AppendLine(activeTransportString);
                sb.AppendLine("Date: " + TLUtils.ToDateTime(_config.Date));
                callback(sb.ToString());
            });
        }

        public void UpdateTransportInfoAsync(int dcId, string dcIpAddress, int dcPort)
        {
            var dcOption = new TLDCOption
            {
                Id = dcId,
                Hostname = string.Empty,
                IpAddress = dcIpAddress,
                Port = dcPort
            };

            var args = new DCOptionsUpdatedEventArgs();
            args.Update = new TLUpdateDCOptions { DCOptions = new TLVector<TLDCOption> { dcOption } };

            OnDCOptionsUpdated(this, args);

            ClearHistory("UpdateTransportInfoAsync", false);

            lock (_activeTransportRoot)
            {
                // continue listening on fault
                var transportDCId = _activeTransport != null ? _activeTransport.DCId : 0;
                var transportKey = _activeTransport != null ? _activeTransport.AuthKey : null;
                var transportSalt = _activeTransport != null ? _activeTransport.Salt : null;
                var transportSessionId = _activeTransport != null ? _activeTransport.SessionId : null;
                var transportSequenceNumber = _activeTransport != null ? _activeTransport.SequenceNumber : 0;
                var transportClientTicksDelta = _activeTransport != null ? _activeTransport.ClientTicksDelta : 0;
                bool isCreated;
                _activeTransport = _transportService.GetTransport(dcIpAddress, dcPort, Type, out isCreated);
                if (isCreated)
                {
                    _activeTransport.DCId = transportDCId;
                    _activeTransport.AuthKey = transportKey;
                    _activeTransport.Salt = transportSalt;
                    _activeTransport.SessionId = transportSessionId;
                    _activeTransport.SequenceNumber = transportSequenceNumber;
                    _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                    _activeTransport.PacketReceived += OnPacketReceived;
                }
            }

            //callback.SafeInvoke(true);
        }


        private void OnDCOptionsUpdated(object sender, DCOptionsUpdatedEventArgs e)
        {
            var newOptions = e.Update.DCOptions;

            var dcOptionsInfo = new StringBuilder();
            dcOptionsInfo.AppendLine("TLUpdateDCOptions");
            foreach (var option in newOptions)
            {
                dcOptionsInfo.AppendLine(string.Format("DCId={0} {1}:{2}", option.Id, option.IpAddress, option.Port));
            }
            Execute.ShowDebugMessage(dcOptionsInfo.ToString());

            if (_config != null && _config.DCOptions != null)
            {
                foreach (var newOption in newOptions)
                {
                    //if (newOption is TLDCOption30)
                    //{
                    //    continue;
                    //}

                    var updated = false;
                    // 1) update ip address, port, hostname
                    foreach (var oldOption in _config.DCOptions)
                    {
                        if (newOption.Id == oldOption.Id &&
                            newOption.IsIpv6 == oldOption.IsIpv6 &&
                            newOption.IsMediaOnly == oldOption.IsMediaOnly)
                        {
                            oldOption.Hostname = newOption.Hostname;
                            oldOption.IpAddress = newOption.IpAddress;
                            oldOption.Port = newOption.Port;
                            // keep AuthKey, SessionId and Salt

                            updated = true;
                        }
                    }

                    // 2) append new DCOption
                    if (!updated)
                    {
                        // fix readonly array of dcOption
                        var list = _config.DCOptions.ToList();
                        list.Add(newOption);
                        _config.DCOptions = new TLVector<TLDCOption>(list);
                    }
                }
                SaveConfig();
            }
        }

        private int GetCurrentUserId()
        {
            return CurrentUserId;
        }

        private void OnPacketReceived(object sender, DataEventArgs e)
        {
            var position = 0;
            var handled = false;

            var transport = (ITransport)sender;
            if (transport.AuthKey == null)
            {
                try
                {

                    //var message = TLObject.GetObject<TLNonEncryptedTransportMessage>(e.Data, ref position);
                    var message = TLFactory.From<TLNonEncryptedTransportMessage>(e.Data);
                    var historyItem = transport.DequeueFirstNonEncryptedItem();
                    if (historyItem != null)
                    {
#if LOG_REGISTRATION
                        TLUtils.WriteLog(
                            string.Format("OnReceivedBytes by {0} AuthKey==null: invoke historyItem {1} with result {2} (data length={3})",
                                transport.Id, historyItem.Caption, message.Data.GetType(), e.Data.Length));
#endif
                        historyItem.Callback?.TrySetResult(new MTProtoResponse(message.Query));
                    }
                    else
                    {
#if LOG_REGISTRATION
                        TLUtils.WriteLog(
                            string.Format("OnReceivedBytes by {0} AuthKey==null: cannot find historyItem {1} with result {2} (data length={3})",
                                transport.Id, string.Empty, message.Data.GetType(), e.Data.Length));
#endif
                    }

                    handled = true;
                }
                catch (Exception ex)
                {
#if LOG_REGISTRATION

                    TLUtils.WriteLog(
                            string.Format("OnReceivedBytes by {0} AuthKey==null exception: cannot parse TLNonEncryptedMessage with History\n {1} \nand exception\n{2} (data length={3})",
                                transport.Id, transport.PrintNonEncryptedHistory(), ex, e.Data.Length));
#endif
                }

                if (!handled)
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog(
                            string.Format("OnReceivedBytes by {0} AuthKey==null !handled: invoke ReceiveBytesAsync with data length {1}",
                                transport.Id, e.Data.Length));
#endif
                    ReceiveBytesAsync(transport, e.Data);
                }
            }
            else
            {
#if LOG_REGISTRATION
                TLUtils.WriteLog(
                            string.Format("OnReceivedBytes by {0} AuthKey!=null: invoke ReceiveBytesAsync with data length {1}",
                                transport.Id, e.Data.Length));
#endif
                ReceiveBytesAsync(transport, e.Data);
            }
        }

        private void OnServiceInitializationFailed(object sender, EventArgs e)
        {
            Execute.BeginOnThreadPool(TimeSpan.FromSeconds(1.0), () =>
            {
#if LOG_REGISTRATION
                TLUtils.WriteLog("Service initialization failed");
#endif
                CancelDelayedItemsAsync();

                // если генерация ключа прошла успешно, но предыдущая попытка завершилась неудачно на методах help.getNearestDc, help.getConfig
                // обнуляем ключ, т.к. в противном случае resPQ будем пытаться расшифровать ключем

                lock (_activeTransportRoot)
                {
                    _activeTransport.AuthKey = null;
                }

                Initialize();
            });
        }

        private void CancelDelayedItemsAsync(bool force = false)
        {
            Execute.BeginOnThreadPool(() =>
            {
#if LOG_REGISTRATION
                TLUtils.WriteLog("Cancel delayed items");
#endif
                lock (_delayedItemsRoot)
                {
                    var canceledItems = new List<DelayedItem>();
                    foreach (var item in _delayedItems)
                    {
                        if (force
                            || (item.MaxAttempt.HasValue
                                && item.MaxAttempt <= item.CurrentAttempt))
                        {
                            item.AttemptFailed.SafeInvoke(item.CurrentAttempt);
                            canceledItems.Add(item);
                        }
                        item.CurrentAttempt++;
                    }

                    foreach (var canceledItem in canceledItems)
                    {
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Cancel delayed item\n " + canceledItem);
#endif
                        _delayedItems.Remove(canceledItem);

                        if (canceledItem.FaultCallback != null)
                        {
                            //canceledItem.FaultCallback(new TLRPCError { Code = new int?(404) });
                        }
                    }
                }
            });
        }

        private void OnServiceInitialized(object sender, EventArgs e)
        {
#if LOG_REGISTRATION
            TLUtils.WriteLog("Service initialized");
#endif
            if (Constants.IsLongPollEnabled)
            {
                StartLongPoll();
            }

            SendDelayedItemsAsync();
        }

        private void SendDelayedItemsAsync()
        {
            Execute.BeginOnThreadPool(() =>
            {
#if LOG_REGISTRATION
                TLUtils.WriteLog("Send delayed items (count=" + _delayedItems.Count + ")");
#endif
                lock (_delayedItemsRoot)
                {
                    if (_delayedItems.Count > 0)
                    {
                        foreach (var item in _delayedItems)
                        {
#if LOG_REGISTRATION
                            TLUtils.WriteLog("Dequeue and send delayed item \n" + item);
#endif
                            //SendInformativeMessage(item.Caption, item.Object, item.Callback, item.FaultCallback, item.MaxAttempt, item.AttemptFailed);
                        }

                        _delayedItems.Clear();
                    }
                }
            });
        }

        public async Task Initialize()
        {

            try
            {
                var result = TryReadConfig();
                if (result == null)
                {

#if LOG_REGISTRATION
                            TLUtils.WriteLog("Read config with result: " + result);
#endif

#if LOG_REGISTRATION
                                TLUtils.WriteLog("TLUtils.LogRegistration=true");
                                TLUtils.IsLogEnabled = true;
#endif

                    lock (_activeTransportRoot)
                    {
                        var transportDCId = _activeTransport != null ? _activeTransport.DCId : Constants.FirstServerDCId;
                        var transportKey = _activeTransport != null ? _activeTransport.AuthKey : null;
                        var transportSalt = _activeTransport != null ? _activeTransport.Salt : null;
                        var transportSessionId = _activeTransport != null ? _activeTransport.SessionId : null;
                        var transportSequenceNumber = _activeTransport != null ? _activeTransport.SequenceNumber : 0;
                        var transportClientTicksDelta = _activeTransport != null ? _activeTransport.ClientTicksDelta : 0;
                        bool isCreated;
                        _activeTransport = _transportService.GetTransport(Constants.FirstServerIpAddress, Constants.FirstServerPort, Type, out isCreated);
                        if (isCreated)
                        {
                            _activeTransport.DCId = Constants.FirstServerDCId;
                            _activeTransport.AuthKey = transportKey;
                            _activeTransport.Salt = transportSalt;
                            _activeTransport.SessionId = transportSessionId;
                            _activeTransport.SequenceNumber = transportSequenceNumber;
                            _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                            _activeTransport.PacketReceived += OnPacketReceived;
                        }
                    }
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Start generating auth key");
#endif
                    var initRequest = await InitAsync();
                    if (initRequest.Error == null)
                    {
                        var tuple = initRequest.Value;
#if LOG_REGISTRATION
                                    TLUtils.WriteLog("Stop generating auth key");
                                    TLUtils.WriteLog("Start help.getNearestDc");
#endif
                        lock (_activeTransportRoot)
                        {
                            _activeTransport.DCId = Constants.FirstServerDCId;
                            _activeTransport.AuthKey = tuple.Item1;
                            _activeTransport.Salt = tuple.Item2;
                            _activeTransport.SessionId = tuple.Item3;
                        }
                        var authKeyId = TLUtils.GenerateLongAuthKeyId(tuple.Item1);

                        lock (_authKeysRoot)
                        {
                            if (!_authKeys.ContainsKey(authKeyId))
                            {
                                _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = tuple.Item1, AutkKeyId = authKeyId });
                            }
                        }
                        //IsInitialized = true;   // Важно, используется тут, чтобы OnPacketReceived не пытался рассматривать ответ как NonEncryptedMessage


                        var timer = Stopwatch.StartNew();
                        var nearestDC = await GetNearestDCAsync();
                        if (nearestDC.Error == null)
                        {

#if LOG_REGISTRATION
                                        TLUtils.WriteLog("Stop help.getNearestDc");
                                        TLUtils.WriteLog("Start help.getConfig");
#endif
                            lock (_activeTransportRoot)
                            {
                                _activeTransport.DCId = nearestDC.Value.ThisDC;
                            }
                            var elapsed = timer.Elapsed;
                            var timer2 = Stopwatch.StartNew();
                            var config = await GetConfigAsync();
                            if (config.Error == null)
                            {
                                var elapsed2 = timer2.Elapsed;
                                var sb = new StringBuilder();
                                sb.AppendLine("help.getNearestDc " + elapsed.ToString("g"));
                                sb.AppendLine("help.getConfig " + elapsed2.ToString("g"));
                                sb.AppendLine("auth time " + _authTimeElapsed.ToString("g"));
                                Execute.ShowDebugMessage(sb.ToString());
#if LOG_REGISTRATION
                                                TLUtils.WriteLog("Stop help.getConfig");
#endif
                                config.Value.Country = nearestDC.Value.Country.ToString();

                                Execute.BeginOnThreadPool(() => RaiseGotUserCountry(config.Value.Country));

                                _config = TLExtensions.Merge(_config, config.Value);
                                var dcOption = config.Value.DCOptions.First(x => x.IsValidIPv4Option(_activeTransport.DCId));

                                dcOption.AuthKey = _activeTransport.AuthKey;
                                dcOption.Salt = _activeTransport.Salt;
                                dcOption.SessionId = _activeTransport.SessionId;

                                config.Value.ActiveDCOptionIndex = config.Value.DCOptions.IndexOf(dcOption);

                                _cacheService.SetConfig(config.Value);
                                RaiseInitialized();
                            }
                        }
                    }
                }
                else
                {
                    var configQ = _config;
                    var activeDCOPtionIndex = _config.ActiveDCOptionIndex;


                    var activeDCOption = configQ.DCOptions[activeDCOPtionIndex];
                    var getConfigRequired = activeDCOption.Id == null;
                    // fix to update from 0.1.2.1 to 0.1.2.4
                    // previously Id is not saved for first DC
                    lock (_activeTransportRoot)
                    {
                        var transportDCId = _activeTransport != null ? _activeTransport.DCId : 0;
                        var transportKey = _activeTransport != null ? _activeTransport.AuthKey : null;
                        var transportSalt = _activeTransport != null ? _activeTransport.Salt : null;
                        var transportSessionId = _activeTransport != null ? _activeTransport.SessionId : null;
                        var transportSequenceNumber = _activeTransport != null ? _activeTransport.SequenceNumber : 0;
                        var transportClientTicksDelta = _activeTransport != null ? _activeTransport.ClientTicksDelta : 0;
                        bool isCreated;
                        _activeTransport = _transportService.GetTransport(activeDCOption.IpAddress.ToString(), activeDCOption.Port, Type, out isCreated);
                        if (isCreated)
                        {
                            _activeTransport.DCId = transportDCId;
                            _activeTransport.AuthKey = transportKey;
                            _activeTransport.Salt = transportSalt;
                            _activeTransport.SessionId = transportSessionId;
                            _activeTransport.SequenceNumber = transportSequenceNumber;
                            _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                            _activeTransport.PacketReceived += OnPacketReceived;
                        }
                    }

                    lock (_activeTransportRoot)
                    {
                        _activeTransport.DCId = getConfigRequired ? 0 : activeDCOption.Id;
                        _activeTransport.AuthKey = activeDCOption.AuthKey;
                        _activeTransport.Salt = activeDCOption.Salt;
                        _activeTransport.SessionId = TLLong.Random();
                        _activeTransport.ClientTicksDelta = activeDCOption.ClientTicksDelta;
                    }

                    //fix for version 0.1.3.12
                    if (activeDCOption.AuthKey == null)
                    {
                        //clear config and try again 
                        RaiseAuthorizationRequired(new AuthorizationRequiredEventArgs { MethodName = "Initialize activeDCOption.AuthKey==null", Error = null, AuthKeyId = 0 });
                        _config = null;
                        _cacheService.SetConfig(_config);

                        RaiseInitializationFailed();

                        return;
                    }

                    var authKeyId = TLUtils.GenerateLongAuthKeyId(activeDCOption.AuthKey);
                    //Log.Write("Use authKey=" + authKeyId);
                    lock (_authKeysRoot)
                    {
                        if (!_authKeys.ContainsKey(authKeyId))
                        {
                            _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = activeDCOption.AuthKey, AutkKeyId = authKeyId });
                        }
                    }

                    if (getConfigRequired)
                    {
                        var timer = Stopwatch.StartNew();
                        var nearestDC = await GetNearestDCAsync();
                        if (nearestDC.Error == null)
                        {

#if LOG_REGISTRATION
                            TLUtils.WriteLog("Stop help.getNearestDc");
                            TLUtils.WriteLog("Start help.getConfig");
#endif
                            lock (_activeTransportRoot)
                            {
                                _activeTransport.DCId = nearestDC.Value.ThisDC;
                            }
                            var elapsed = timer.Elapsed;
                            var timer2 = Stopwatch.StartNew();
                            var config = await GetConfigAsync();
                            if (config.Error == null)
                            {
                                var elapsed2 = timer2.Elapsed;
                                var sb = new StringBuilder();
                                sb.AppendLine("help.getNearestDc: " + elapsed);
                                sb.AppendLine("help.getConfig: " + elapsed2);

                                Execute.ShowDebugMessage(sb.ToString());
#if LOG_REGISTRATION
                                                TLUtils.WriteLog("Stop help.getConfig");
#endif
                                config.Value.Country = nearestDC.Value.Country;
                                _config = TLExtensions.Merge(_config, config.Value);
                                var dcOption = config.Value.DCOptions.First(x => x.IsValidIPv4Option(_activeTransport.DCId));

                                dcOption.AuthKey = _activeTransport.AuthKey;
                                dcOption.Salt = _activeTransport.Salt;
                                dcOption.SessionId = _activeTransport.SessionId;

                                config.Value.ActiveDCOptionIndex = config.Value.DCOptions.IndexOf(dcOption);
                                _cacheService.SetConfig(config.Value);
                                RaiseInitialized();
                            }
                        }
                    }
                    else
                    {
                        RaiseInitialized();
                    }
                }
            }
            catch (Exception e)
            {
                TLUtils.WriteException(e);
                RaiseInitializationFailed();
            }
        }

        private TLConfig TryReadConfig()
        {
            TLConfig result = null;
            _cacheService.GetConfigAsync(
                config =>
                {
                    result = config;
                });

            if (result != null)
            {
                _config = result;
            }

            return result;
        }

        private void SendAcknowledgments(TLTransportMessage response)
        {
            var ids = new TLVector<long>();

            if (response.SeqNo % 2 == 1)
            {
                ids.Add(response.MsgId);
            }
            if (response.Query is TLMessageContainer)
            {
                var container = (TLMessageContainer)response.Query;
                foreach (var message in container.Messages)
                {
                    if (message.SeqNo % 2 == 1)
                    {
                        ids.Add(message.MsgId);
                    }
                }
            }

            if (ids.Count > 0)
            {
                MessageAcknowledgments(ids);
            }
        }

        private void SendAcknowledgmentsByTransport(ITransport transport, TLTransportMessage response)
        {
            var ids = new TLVector<long>();

            if (response.SeqNo % 2 == 1)
            {
                ids.Add(response.MsgId);
            }
            if (response.Query is TLMessageContainer)
            {
                var container = (TLMessageContainer)response.Query;
                foreach (var message in container.Messages)
                {
                    if (message.SeqNo % 2 == 1)
                    {
                        ids.Add(message.MsgId);
                    }
                }
            }

            if (ids.Count > 0)
            {
                MessageAcknowledgmentsByTransport(transport, ids);
            }
        }

        public event EventHandler<AuthorizationRequiredEventArgs> AuthorizationRequired;

        public void RaiseAuthorizationRequired(AuthorizationRequiredEventArgs args)
        {
            var handler = AuthorizationRequired;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        private void ReceiveBytesAsync(ITransport transport, byte[] bytes)
        {
            try
            {
                if (bytes.Length == 4)
                {
                    if (BitConverter.ToInt32(bytes, 0) == -404)
                    {
                        // PREVIOUS REQUEST WAS INVALID
                        Debugger.Break();
                    }
                }

                //var position = 0;
                //var encryptedMessage = (TLEncryptedTransportMessage)new TLEncryptedTransportMessage().FromBytes(bytes, ref position);
                var encryptedMessage = new TLEncryptedTransportMessage();
                using (var reader = new TLBinaryReader(bytes))
                {
                    encryptedMessage.Read(reader);
                }

                byte[] authKey2 = null;
                lock (_authKeysRoot)
                {
                    try
                    {
                        authKey2 = _authKeys[encryptedMessage.AuthKeyId].AuthKey;
                    }
                    catch (Exception e)
                    {
                        TLUtils.WriteException(e);
                    }
                }

                using (var reader = new TLBinaryReader(bytes))
                {
                    encryptedMessage.Read(reader, authKey2);
                }

                //encryptedMessage.Decrypt(authKey2);

                //position = 0;
                TLTransportMessage transportMessage = encryptedMessage.Query as TLTransportMessage;
                {
                    if (transportMessage.SessionId != transport.SessionId.Value)
                    {
                        throw new Exception("Incorrect session_id");
                    }
                    if ((transportMessage.MsgId % 2) == 0)
                    {
                        throw new Exception("Incorrect message_id");
                    }

                    if (transportMessage != null)
                    {
                        transport.UpdateTicksDelta(transportMessage.MsgId);
                    }
                    // correct ticks delta on first message

                    if (_deviceInfo != null && _deviceInfo.IsBackground)
                    {

                    }

                    // get acknowledgments
                    foreach (var acknowledgment in transportMessage.FindInnerObjects<TLMsgsAck>())
                    {
                        var ids = acknowledgment.MsgIds;
                        lock (_historyRoot)
                        {
                            foreach (var id in ids)
                            {
                                if (_history.ContainsKey(id))
                                {
                                    _history[id].Status = RequestStatus.Confirmed;
                                }
                            }
                        }
                    }
                    // send acknowledgments
                    SendAcknowledgments(transportMessage);


                    // updates
                    _updatesService.ProcessTransportMessage(transportMessage);

                    // bad messages
                    foreach (var badMessage in transportMessage.FindInnerObjects<TLBadMsgNotification>())
                    {
                        HistoryItem item = null;
                        lock (_historyRoot)
                        {
                            if (_history.ContainsKey(badMessage.BadMsgId))
                            {
                                item = _history[badMessage.BadMsgId];
                            }
                        }

                        ProcessBadMessage(transportMessage, badMessage, item);
                    }

                    // bad server salts
                    foreach (var badServerSalt in transportMessage.FindInnerObjects<TLBadServerSalt>())
                    {
                        lock (_activeTransportRoot)
                        {
                            _activeTransport.Salt = badServerSalt.NewServerSalt;
                        }

                        // save config
                        if (_config != null && _config.DCOptions != null)
                        {
                            var activeDCOption = _config.DCOptions[_config.ActiveDCOptionIndex];
                            activeDCOption.Salt = badServerSalt.NewServerSalt;

                            SaveConfig();
                        }

                        HistoryItem item = null;
                        lock (_historyRoot)
                        {
                            if (_history.ContainsKey(badServerSalt.BadMsgId))
                            {
                                item = _history[badServerSalt.BadMsgId];
                            }
                        }

                        ProcessBadServerSalt(transportMessage, badServerSalt, item);
                    }

                    // new session created
                    foreach (var newSessionCreated in TLUtils.FindInnerObjects<TLNewSessionCreated>(transportMessage))
                    {
                        TLUtils.WritePerformance(string.Format("NEW SESSION CREATED: {0} (old {1})", transportMessage.SessionId, _activeTransport.SessionId));
                        lock (_activeTransportRoot)
                        {
                            _activeTransport.SessionId = transportMessage.SessionId;
                            _activeTransport.Salt = newSessionCreated.ServerSalt;
                        }
                    }

                    foreach (var pong in transportMessage.FindInnerObjects<TLPong>())
                    {
                        HistoryItem item;
                        lock (_historyRoot)
                        {
                            if (_history.ContainsKey(pong.MsgId))
                            {
                                item = _history[pong.MsgId];
                                _history.Remove(pong.MsgId);
                            }
                            else
                            {
                                continue;
                            }
                        }
#if DEBUG
                        NotifyOfPropertyChange(() => History);
#endif

                        if (item != null)
                        {
                            item.Callback?.TrySetResult(new MTProtoResponse(pong));
                        }
                    }

                    // rpcresults
                    foreach (var result in transportMessage.FindInnerObjects<TLRPCResult>())
                    {
                        HistoryItem historyItem = null;

                        lock (_historyRoot)
                        {
                            if (_history.ContainsKey(result.RequestMsgId))
                            {
                                historyItem = _history[result.RequestMsgId];
                                _history.Remove(result.RequestMsgId);
                            }
                            else
                            {
                                continue;
                            }
                        }
#if DEBUG
                        NotifyOfPropertyChange(() => History);
#endif

                        //RemoveItemFromSendingQueue(result.RequestMessageId.Value);

                        var error = result.Query as TLRPCError;
                        if (error != null)
                        {
                            string errorString;
                            var reqError = error as TLRPCReqError;
                            if (reqError != null)
                            {
                                errorString = string.Format("RPCReqError {1} {2} (query_id={0})", reqError.QueryId, reqError.ErrorCode, reqError.ErrorMessage);
                            }
                            else
                            {
                                errorString = string.Format("RPCError {0} {1}", error.ErrorCode, error.ErrorMessage);
                            }

                            Execute.ShowDebugMessage(historyItem + Environment.NewLine + errorString);
                            ProcessRPCError(error, historyItem, encryptedMessage.AuthKeyId);
                            Debug.WriteLine(errorString + " msg_id=" + result.RequestMsgId);
                            TLUtils.WriteLine(errorString);
                        }
                        else
                        {
                            var messageData = result.Query;
                            if (messageData is TLGzipPacked)
                            {
                                messageData = ((TLGzipPacked)messageData).Query;
                            }

                            // TODO:
                            if (/*messageData is TLSentMessageBase ||
                                messageData is TLStatedMessageBase ||*/
                                messageData is TLUpdatesBase ||
                                messageData is TLMessagesSentEncryptedMessage ||
                                messageData is TLMessagesSentEncryptedFile ||
                                messageData is TLMessagesAffectedHistory ||
                                messageData is TLMessagesAffectedMessages ||
                                historyItem.Object is TLMessagesReadEncryptedHistory)
                            {
                                RemoveFromQueue(historyItem);
                            }

                            if (historyItem.Caption == "messages.getDialogs")
                            {
#if DEBUG_UPDATEDCOPTIONS
                                var dcOption = new TLDCOption
                                {
                                    Hostname = new string(""),
                                    Id = new int?(2),
                                    IpAddress = new string("109.239.131.193"),
                                    Port = new int?(80)
                                };
                                var dcOptions = new TLVector<TLDCOption> {dcOption};
                                var update = new TLUpdateDCOptions {DCOptions = dcOptions};
                                var updateShort = new TLUpdatesShort {Date = new int?(0), Update = update};

                                _updatesService.ProcessTransportMessage(new TLTransportMessage{MessageData = updateShort});
#endif
                            }
                            try
                            {
                                historyItem.Callback.TrySetResult(new MTProtoResponse(messageData));
                            }
                            catch (Exception e)
                            {
#if LOG_REGISTRATION
                                TLUtils.WriteLog(e.ToString());
#endif
                                TLUtils.WriteException(e);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
#if LOG_REGISTRATION
                TLUtils.WriteLog("ReceiveBytesAsync Exception=\n" + e);
#endif
                TLUtils.WriteException(e);

                ResetConnection(e);
            }
        }

        private async Task ResetConnection(Exception ex)
        {
            ClearHistory("ResetConnection", false, ex);

            lock (_activeTransportRoot)
            {
                // continue listening on fault
                var transportDCId = _activeTransport != null ? _activeTransport.DCId : 0;
                var transportKey = _activeTransport != null ? _activeTransport.AuthKey : null;
                var transportSalt = _activeTransport != null ? _activeTransport.Salt : null;
                var transportSessionId = _activeTransport != null ? _activeTransport.SessionId : null;
                var transportSequenceNumber = _activeTransport != null ? _activeTransport.SequenceNumber : 0;
                var transportClientTicksDelta = _activeTransport != null ? _activeTransport.ClientTicksDelta : 0;
                bool isCreated;
                _activeTransport = _transportService.GetTransport(_activeTransport.Host, _activeTransport.Port, Type, out isCreated);
                if (isCreated)
                {
                    _activeTransport.DCId = transportDCId;
                    _activeTransport.AuthKey = transportKey;
                    _activeTransport.Salt = transportSalt;
                    _activeTransport.SessionId = transportSessionId;
                    _activeTransport.SequenceNumber = transportSequenceNumber;
                    _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                    _activeTransport.PacketReceived += OnPacketReceived;
                }
            }

            // to bind authKey to current TCPTransport, get changes, etc...
            await UpdateStatusAsync(false);
        }

        public void ClearHistory(string caption, bool createNewSession, Exception e = null)
        {
            var errorDebugString = string.Format("{0} clear history start {1}", DateTime.Now.ToString("HH:mm:ss.fff"), _history.Count);
            TLUtils.WriteLine(errorDebugString, LogSeverity.Error);

            _transportService.Close();
            if (createNewSession)
            {
                _activeTransport.SessionId = TLLong.Random();
            }
            // сначала очищаем reqPQ, reqDHParams и setClientDHParams
            _activeTransport.ClearNonEncryptedHistory();

            //затем очищаем help.getNearestDc, help.getConfig и любые другие методы
            //иначе при вызове faultCallback для help.getNearestDc, help.getConfig и reqPQ снова бы завершился с ошибкой
            // при вызове ClearNonEncryptedHistory
            var history = new List<HistoryItem>();
            lock (_historyRoot)
            {
                foreach (var keyValue in _history)
                {
                    history.Add(keyValue.Value);
                }
                _history.Clear();
            }

            Execute.BeginOnThreadPool(() =>
            {
                foreach (var keyValue in history)
                {
                    //if (createNewSession && keyValue.Caption == "updates.getDifference")
                    //{
                    //    continue;
                    //}
                    keyValue.FaultCallback.SafeInvoke(new TLRPCError { ErrorCode = 404, ErrorMessage = $"MTProtoService.ClearHistory{caption}", /* Exception = e */ });
                }
            });
        }

        private async Task ProcessRPCError(TLRPCError error, HistoryItem historyItem, long keyId)
        {
            Log.Write(string.Format("RPCError {0} {1}", historyItem.Caption, error));

#if LOG_REGISTRATION
            TLUtils.WriteLog(string.Format("RPCError {0} {1}", historyItem.Caption, error));
#endif

            if (error.CodeEquals(TLErrorCode.UNAUTHORIZED))
            {
                Execute.ShowDebugMessage(string.Format("RPCError {0} {1}", historyItem.Caption, error));

                if (historyItem != null
                    && historyItem.Caption != "account.updateStatus"
                    && historyItem.Caption != "account.registerDevice"
                    && historyItem.Caption != "auth.signIn")
                {
                    //Execute.ShowDebugMessage(string.Format("RPCError {0} {1} (auth required)", historyItem.Caption, error));
                    RaiseAuthorizationRequired(new AuthorizationRequiredEventArgs { Error = error, MethodName = historyItem.Caption, AuthKeyId = keyId });
                }
                else if (historyItem != null)
                {
                    historyItem.FaultCallback?.Invoke(error);
                    historyItem.Callback.TrySetResult(new MTProtoResponse(error));
                }
            }
            else if (error.CodeEquals(TLErrorCode.ERROR_SEE_OTHER) && (error.TypeStarsWith(TLErrorType.NETWORK_MIGRATE) || error.TypeStarsWith(TLErrorType.PHONE_MIGRATE) /*|| error.TypeStarsWith(ErrorType.FILE_MIGRATE)*/ ))
            {

                var serverNumber = Convert.ToInt32(
                    error.GetErrorTypeString()
                    .Replace(TLErrorType.NETWORK_MIGRATE.ToString(), string.Empty)
                    .Replace(TLErrorType.PHONE_MIGRATE.ToString(), string.Empty)
                    //.Replace(ErrorType.FILE_MIGRATE.ToString(), string.Empty)
                    .Replace("_", string.Empty));

                if (_config == null
                    || _config.DCOptions.FirstOrDefault(x => x.IsValidIPv4Option(serverNumber)) == null)
                {
                    var config = await GetConfigAsync();
                    if (config.Error == null)
                    {
                        // параметры предыдущего подключения не сохраняются, поэтому когда ответ приходит после
                        // подключения к следующему серверу, то не удается расшифровать старые сообщения, пришедшие с 
                        // задержкой с новой солью и authKey
                        _config = TLExtensions.Merge(_config, config.Value);
                        SaveConfig();
                        if (historyItem.Object.GetType() == typeof(TLAuthSendCode))
                        {
                            var dcOption = _config.DCOptions.First(x => x.IsValidIPv4Option(serverNumber));

                            lock (_activeTransportRoot)
                            {
                                var transportDCId = dcOption.Id;
                                var transportKey = dcOption.AuthKey;
                                var transportSalt = dcOption.Salt;
                                var transportSessionId = TLLong.Random();
                                var transportSequenceNumber = 0;
                                var transportClientsTicksDelta = dcOption.ClientTicksDelta;
                                bool isCreated;
                                _activeTransport = _transportService.GetTransport(dcOption.IpAddress.ToString(), dcOption.Port, Type, out isCreated);
                                if (isCreated)
                                {
                                    _activeTransport.DCId = transportDCId;
                                    _activeTransport.AuthKey = transportKey;
                                    _activeTransport.Salt = transportSalt;
                                    _activeTransport.SessionId = transportSessionId;
                                    _activeTransport.SequenceNumber = transportSequenceNumber;
                                    _activeTransport.ClientTicksDelta = transportClientsTicksDelta;
                                    _activeTransport.PacketReceived += OnPacketReceived;
                                }
                            }

                            //IsInitialized = false;
                            var tuple = await InitAsync();
                            if (tuple.Error == null)
                            {
                                lock (_activeTransportRoot)
                                {
                                    _activeTransport.DCId = serverNumber;
                                    _activeTransport.AuthKey = tuple.Value.Item1;
                                    _activeTransport.Salt = tuple.Value.Item2;
                                    _activeTransport.SessionId = tuple.Value.Item3;
                                }
                                var authKeyId = TLUtils.GenerateLongAuthKeyId(tuple.Value.Item1);

                                lock (_authKeysRoot)
                                {
                                    if (!_authKeys.ContainsKey(authKeyId))
                                    {
                                        _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = tuple.Value.Item1, AutkKeyId = authKeyId });
                                    }
                                }

                                dcOption.AuthKey = tuple.Value.Item1;
                                dcOption.Salt = tuple.Value.Item2;
                                dcOption.SessionId = tuple.Value.Item3;

                                _config.ActiveDCOptionIndex = _config.DCOptions.IndexOf(dcOption);
                                _cacheService.SetConfig(_config);

                                //IsInitialized = true;
                                RaiseInitialized();
                                // , historyItem.Callback, historyItem.FaultCallback
                                var response = await SendInformativeMessage<TLObject>(historyItem.Caption, historyItem.Object);
                            }
                            else
                            {

                                //restore previous transport
                                var activeDCOption = _config.DCOptions[_config.ActiveDCOptionIndex];
                                lock (_activeTransportRoot)
                                {
                                    bool isCreated;
                                    _activeTransport = _transportService.GetTransport(activeDCOption.IpAddress.ToString(), activeDCOption.Port, Type, out isCreated);
                                    if (isCreated)
                                    {
                                        _activeTransport.DCId = activeDCOption.Id;
                                        _activeTransport.AuthKey = activeDCOption.AuthKey;
                                        _activeTransport.Salt = activeDCOption.Salt;
                                        _activeTransport.SessionId = TLLong.Random();
                                        _activeTransport.SequenceNumber = 0;
                                        _activeTransport.ClientTicksDelta = activeDCOption.ClientTicksDelta;
                                        _activeTransport.PacketReceived += OnPacketReceived;
                                    }
                                }
#if LOG_REGISTRATION
                                TLUtils.WriteLog(string.Format("RPCError restore transport {0} {1}:{2} item {3}", _activeTransport.Id, _activeTransport.Host, _activeTransport.Port, historyItem.Caption));
#endif

                                // historyItem.FaultCallback.SafeInvoke(er);
                            }
                        }
                        else
                        {
                            var response = await SendInformativeMessage<TLObject>(historyItem.Caption, historyItem.Object);
                            //MigrateAsync(serverNumber, auth => SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback));
                        }
                    }
                }
                else
                {
                    if (historyItem.Object.GetType() == typeof(TLAuthSendCode)
                        || historyItem.Object.GetType() == typeof(TLUploadGetFile))
                    {
                        var activeDCOption = _config.DCOptions.First(x => x.IsValidIPv4Option(serverNumber));

                        lock (_activeTransportRoot)
                        {
                            var transportDCId = activeDCOption.Id;
                            var transportKey = activeDCOption.AuthKey;
                            var transportSalt = activeDCOption.Salt;
                            var transportSessionId = TLLong.Random();
                            var transportSequenceNumber = 0;
                            var transportClientsTicksDelta = activeDCOption.ClientTicksDelta;
                            bool isCreated;
                            _activeTransport = _transportService.GetTransport(activeDCOption.IpAddress.ToString(), activeDCOption.Port, Type, out isCreated);
                            if (isCreated)
                            {
                                _activeTransport.DCId = transportDCId;
                                _activeTransport.AuthKey = transportKey;
                                _activeTransport.Salt = transportSalt;
                                _activeTransport.SessionId = transportSessionId;
                                _activeTransport.SequenceNumber = transportSequenceNumber;
                                _activeTransport.ClientTicksDelta = transportClientsTicksDelta;
                                _activeTransport.PacketReceived += OnPacketReceived;
                            }
                        }

                        if (activeDCOption.AuthKey == null)
                        {
                            //IsInitialized = false;
                            var tuple = await InitAsync();
                            if (tuple.Error == null)
                            {

                                lock (_activeTransportRoot)
                                {
                                    _activeTransport.DCId = serverNumber;
                                    _activeTransport.AuthKey = tuple.Value.Item1;
                                    _activeTransport.Salt = tuple.Value.Item2;
                                    _activeTransport.SessionId = tuple.Value.Item3;
                                }

                                var authKeyId = TLUtils.GenerateLongAuthKeyId(tuple.Value.Item1);

                                lock (_authKeysRoot)
                                {
                                    if (!_authKeys.ContainsKey(authKeyId))
                                    {
                                        _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = tuple.Value.Item1, AutkKeyId = authKeyId });
                                    }
                                }

                                activeDCOption.AuthKey = tuple.Value.Item1;
                                activeDCOption.Salt = tuple.Value.Item2;
                                activeDCOption.SessionId = tuple.Value.Item3;

                                _config.ActiveDCOptionIndex = _config.DCOptions.IndexOf(activeDCOption);
                                _cacheService.SetConfig(_config);

                                //IsInitialized = true;
                                RaiseInitialized();

                                SendInformativeMessageInternal<TLObject>(historyItem.Caption, historyItem.Object, historyItem.Callback);
                                // SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                            }
                            else
                            {

                                //restore previous transport
                                var activeDCOption2 = _config.DCOptions[_config.ActiveDCOptionIndex];
                                lock (_activeTransportRoot)
                                {
                                    bool isCreated;
                                    _activeTransport = _transportService.GetTransport(activeDCOption2.IpAddress.ToString(), activeDCOption2.Port, Type, out isCreated);
                                    if (isCreated)
                                    {
                                        _activeTransport.DCId = activeDCOption2.Id;
                                        _activeTransport.AuthKey = activeDCOption2.AuthKey;
                                        _activeTransport.Salt = activeDCOption2.Salt;
                                        _activeTransport.SessionId = TLLong.Random();
                                        _activeTransport.SequenceNumber = 0;
                                        _activeTransport.ClientTicksDelta = activeDCOption2.ClientTicksDelta;
                                        _activeTransport.PacketReceived += OnPacketReceived;
                                    }
                                }
#if LOG_REGISTRATION
                                TLUtils.WriteLog(string.Format("RPCError restore transport {0} {1}:{2} item {3}", _activeTransport.Id, _activeTransport.Host, _activeTransport.Port, historyItem.Caption));
#endif

                                //  historyItem.FaultCallback.SafeInvoke(er);
                            }
                        }
                        else
                        {
                            lock (_activeTransportRoot)
                            {
                                _activeTransport.AuthKey = activeDCOption.AuthKey;
                                _activeTransport.Salt = activeDCOption.Salt;
                                _activeTransport.SessionId = TLLong.Random();
                            }
                            var authKeyId = TLUtils.GenerateLongAuthKeyId(activeDCOption.AuthKey);

                            lock (_authKeysRoot)
                            {
                                if (!_authKeys.ContainsKey(authKeyId))
                                {
                                    _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = activeDCOption.AuthKey, AutkKeyId = authKeyId });
                                }
                            }

                            _config.ActiveDCOptionIndex = _config.DCOptions.IndexOf(activeDCOption);
                            _cacheService.SetConfig(_config);

                            //IsInitialized = true;
                            RaiseInitialized();

                            SendInformativeMessageInternal<TLObject>(historyItem.Caption, historyItem.Object, historyItem.Callback);
                            //SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                        }
                    }
                    else
                    {
                        SendInformativeMessageInternal<TLObject>(historyItem.Caption, historyItem.Object, historyItem.Callback);
                        //MigrateAsync(serverNumber, auth => SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback));
                    }
                }
            }
            else if (error.CodeEquals(TLErrorCode.ERROR_SEE_OTHER) && error.TypeStarsWith(TLErrorType.USER_MIGRATE))
            {
                //return;

                var serverNumber = Convert.ToInt32(
                    error.GetErrorTypeString()
                    .Replace(TLErrorType.USER_MIGRATE.ToString(), string.Empty)
                    .Replace("_", string.Empty));

                // фикс версии 0.1.3.13 когда первый конфиг для dc2 отличался от стартового dc2
                // можно убрать после
                if (_config.ActiveDCOptionIndex == 0 && serverNumber == 2)
                {
                    var activeDCOption = _config.DCOptions.First(x => x.IsValidIPv4Option(serverNumber));

                    lock (_activeTransportRoot)
                    {
                        var transportDCId = activeDCOption.Id;
                        var transportKey = activeDCOption.AuthKey;
                        var transportSalt = activeDCOption.Salt;
                        var transportSessionId = TLLong.Random();
                        var transportSequenceNumber = 0;
                        var transportClientsTicksDelta = activeDCOption.ClientTicksDelta;
                        bool isCreated;
                        _activeTransport = _transportService.GetTransport(activeDCOption.IpAddress.ToString(), activeDCOption.Port, Type, out isCreated);
                        if (isCreated)
                        {
                            _activeTransport.DCId = transportDCId;
                            _activeTransport.AuthKey = transportKey;
                            _activeTransport.Salt = transportSalt;
                            _activeTransport.SessionId = transportSessionId;
                            _activeTransport.SequenceNumber = transportSequenceNumber;
                            _activeTransport.ClientTicksDelta = transportClientsTicksDelta;
                            _activeTransport.PacketReceived += OnPacketReceived;
                        }
                    }

                    var authKeyId = TLUtils.GenerateLongAuthKeyId(activeDCOption.AuthKey);

                    lock (_authKeysRoot)
                    {
                        if (!_authKeys.ContainsKey(authKeyId))
                        {
                            _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = activeDCOption.AuthKey, AutkKeyId = authKeyId });
                        }
                    }

                    _config.ActiveDCOptionIndex = _config.DCOptions.IndexOf(activeDCOption);
                    _cacheService.SetConfig(_config);

                    SendInformativeMessageInternal<TLObject>(historyItem.Caption, historyItem.Object, historyItem.Callback);
                    // SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                }
                // конец фикса

                //ITransport newTransport;
                //TLDCOption newActiveDCOption;
                //lock (_activeTransportRoot)
                //{
                //    newActiveDCOption = _config.DCOptions.First(x => x.IsValidIPv4Option(new int?(serverNumber)));

                //    var transportClientsTicksDelta = newActiveDCOption.ClientTicksDelta;
                //    bool isCreated;
                //    newTransport = _transportService.GetTransport(newActiveDCOption.IpAddress.ToString(), newActiveDCOption.Port.Value, Type, out isCreated);
                //    newTransport.ClientTicksDelta = transportClientsTicksDelta;
                //    newTransport.PacketReceived += OnPacketReceived;
                //}

                //if (newTransport.AuthKey == null)
                //{
                //    InitTransportAsync(newTransport,
                //        tuple =>
                //        {
                //            lock (newTransport.SyncRoot)
                //            {
                //                newTransport.AuthKey = tuple.Item1;
                //                newTransport.Salt = tuple.Item2;
                //                newTransport.SessionId = tuple.Item3;

                //                newTransport.IsInitializing = false;
                //            }
                //            var authKeyId = TLUtils.GenerateLongAuthKeyId(tuple.Item1);

                //            lock (_authKeysRoot)
                //            {
                //                if (!_authKeys.ContainsKey(authKeyId))
                //                {
                //                    _authKeys.Add(authKeyId, new AuthKeyItem {AuthKey = tuple.Item1, AutkKeyId = authKeyId});
                //                }
                //            }

                //            foreach (var dcOption in _config.DCOptions)
                //            {
                //                if (dcOption.Id.Value == newTransport.Id)
                //                {
                //                    dcOption.AuthKey = tuple.Item1;
                //                    dcOption.Salt = tuple.Item2;
                //                    dcOption.SessionId = tuple.Item3;
                //                }
                //            }

                //            _cacheService.SetConfig(_config);

                //            ExportImportAuthorizationAsync(
                //                newTransport,
                //                () =>
                //                {
                //                    lock (_activeTransportRoot)
                //                    {
                //                        _activeTransport = newTransport;
                //                    }

                //                    _config.ActiveDCOptionIndex = _config.DCOptions.IndexOf(newActiveDCOption);
                //                    SaveConfig();

                //                    SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                //                },
                //                err =>
                //                {

                //                });
                //        },
                //        er =>
                //        {

                //        });
                //}
                //else
                //{
                //    ExportImportAuthorizationAsync(
                //        newTransport,
                //        () =>
                //        {
                //            lock (_activeTransportRoot)
                //            {
                //                _activeTransport = newTransport;
                //            }

                //            _config.ActiveDCOptionIndex = _config.DCOptions.IndexOf(newActiveDCOption);
                //            SaveConfig();

                //            SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                //        },
                //        err =>
                //        {

                //        });
                //}

            }
            else
            {
                historyItem.FaultCallback?.Invoke(error);
                historyItem.Callback.TrySetResult(new MTProtoResponse(error));
            }
        }

        private void MigrateAsync(int serverNumber, Action<TLAuthorization> callback)
        {
            throw new NotImplementedException();

            //ExportAuthorizationAsync(new int?(serverNumber), 
            //    exportedAuthorization =>
            //    {
            //        var dcOption = _config.DCOptions.First(x => x.IsValidIPv4Option(new int?(serverNumber)));
            //        _activeTransport.SetAddress(dcOption.IpAddress.ToString(), dcOption.Port.Value);

            //        _isInitialized = false;
            //        NotifyOfPropertyChange(() => IsInitialized);

            //        _authHelper.InitAsync(tuple =>
            //        {
            //            ImportAuthorizationAsync(exportedAuthorization.Id, exportedAuthorization.Bytes, callback);
            //            _isInitialized = true;
            //            NotifyOfPropertyChange(() => IsInitialized);
            //            RaiseInitialized();
            //        });
            //    });
        }

        private void ProcessBadMessage(TLTransportMessage message, TLBadMsgNotification badMessage, HistoryItem historyItem)
        {
            if (historyItem == null) return;

            switch (badMessage.ErrorCode)
            {
                case 16:    // слишком маленький msg_id
                case 17:    // слишком большой msg_id
                    var errorInfo = new StringBuilder();
                    errorInfo.AppendLine("2. CORRECT TIME DELTA for active transport " + _activeTransport.DCId);
                    errorInfo.AppendLine(historyItem.Caption);

                    lock (_historyRoot)
                    {
                        _history.Remove(historyItem.Hash);
                    }
#if DEBUG
                    NotifyOfPropertyChange(() => History);
#endif

                    var saveConfig = false;
                    lock (_activeTransportRoot)
                    {
                        var serverTime = message.MsgId;
                        var clientTime = _activeTransport.GenerateMessageId();

                        var serverDateTime = Utils.UnixTimestampToDateTime(serverTime >> 32);
                        var clientDateTime = Utils.UnixTimestampToDateTime(clientTime >> 32);

                        errorInfo.AppendLine("Server time: " + serverDateTime);
                        errorInfo.AppendLine("Client time: " + clientDateTime);

                        if (historyItem.ClientTicksDelta == _activeTransport.ClientTicksDelta)
                        {
                            saveConfig = true;
                            _activeTransport.ClientTicksDelta += serverTime - clientTime;
                            errorInfo.AppendLine("Set ticks delta: " + _activeTransport.ClientTicksDelta + "(" + (serverDateTime - clientDateTime).TotalSeconds + " seconds)");
                        }
                    }

                    TLUtils.WriteLine(errorInfo.ToString(), LogSeverity.Error);

                    if (saveConfig && _config != null)
                    {
                        var dcOption = _config.DCOptions.FirstOrDefault(x => string.Equals(x.IpAddress.ToString(), _activeTransport.Host, StringComparison.OrdinalIgnoreCase));
                        if (dcOption != null)
                        {
                            dcOption.ClientTicksDelta = _activeTransport.ClientTicksDelta;
                            _cacheService.SetConfig(_config);
                        }
                    }

                    // TODO: replace with SendInformativeMessage


                    var transportMessage = (TLContainerTransportMessage)historyItem.Message;
                    int sequenceNumber;
                    lock (_activeTransportRoot)
                    {
                        if (transportMessage.SeqNo % 2 == 0)
                        {
                            sequenceNumber = 2 * _activeTransport.SequenceNumber;
                        }
                        else
                        {
                            sequenceNumber = 2 * _activeTransport.SequenceNumber + 1;
                            _activeTransport.SequenceNumber++;
                        }

                        transportMessage.SeqNo = sequenceNumber;
                        transportMessage.MsgId = _activeTransport.GenerateMessageId(false);
                    }

                    TLUtils.WriteLine("Corrected client time: " + TLUtils.MessageIdString(transportMessage.MsgId));
                    var authKey = _activeTransport.AuthKey;
                    var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

                    lock (_historyRoot)
                    {
                        _history[historyItem.Hash] = historyItem;
                    }
                    var faultCallback = historyItem.FaultCallback;

                    lock (_activeTransportRoot)
                    {
                        if (_activeTransport.Closed)
                        {
                            var transportDCId = _activeTransport.DCId;
                            var transportKey = _activeTransport.AuthKey;
                            var transportSalt = _activeTransport.Salt;
                            var transportSessionId = _activeTransport.SessionId;
                            var transportSequenceNumber = _activeTransport.SequenceNumber;
                            var transportClientTicksDelta = _activeTransport.ClientTicksDelta;
                            bool isCreated;
                            _activeTransport = _transportService.GetTransport(_activeTransport.Host, _activeTransport.Port, Type, out isCreated);
                            if (isCreated)
                            {
                                _activeTransport.DCId = transportDCId;
                                _activeTransport.AuthKey = transportKey;
                                _activeTransport.Salt = transportSalt;
                                _activeTransport.SessionId = transportSessionId;
                                _activeTransport.SequenceNumber = transportSequenceNumber;
                                _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                                _activeTransport.PacketReceived += OnPacketReceived;
                            }
                        }
                    }
                    Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2,-4} SessionId {3} BadMsgId {4}", string.Format("{0}: {1}", historyItem.Caption, "time"), transportMessage.MsgId, transportMessage.SeqNo, message.SessionId, badMessage.BadMsgId);

                    var captionString = string.Format("{0} {1} {2}", historyItem.Caption, message.SessionId, transportMessage.MsgId);
                    SendPacketAsync(_activeTransport, captionString, encryptedMessage, (result) => 
                    {
                        Debug.WriteLine("@{0} {1} result {2}", string.Format("{0}: {1}", historyItem.Caption, "time"), transportMessage.MsgId, result);
                    },
                    (error) =>
                    {
                        lock (_historyRoot)
                        {
                            _history.Remove(historyItem.Hash);
                        }

#if DEBUG
                        NotifyOfPropertyChange(() => History);
#endif

                        faultCallback.SafeInvoke(new TLRPCError { ErrorCode = 404 });
                    });

                    break;

                case 32:
                case 33:
                    TLUtils.WriteLine(string.Format("ErrorCode={0} INCORRECT MSGSEQNO, CREATE NEW SESSION {1}", badMessage.ErrorCode, historyItem.Caption), LogSeverity.Error);
                    Execute.ShowDebugMessage(string.Format("ErrorCode={0} INCORRECT MSGSEQNO, CREATE NEW SESSION {1}", badMessage.ErrorCode, historyItem.Caption));

                    var previousMessageId = historyItem.Hash;

                    // fix seqNo with creating new Session
                    lock (_activeTransportRoot)
                    {
                        _activeTransport.SessionId = TLLong.Random();
                        _activeTransport.SequenceNumber = 0;
                        transportMessage = (TLTransportMessage)historyItem.Message;
                        if (transportMessage.SeqNo % 2 == 0)
                        {
                            sequenceNumber = 2 * _activeTransport.SequenceNumber;
                        }
                        else
                        {
                            sequenceNumber = 2 * _activeTransport.SequenceNumber + 1;
                            _activeTransport.SequenceNumber++;
                        }

                        transportMessage.SeqNo = sequenceNumber;
                        transportMessage.MsgId = _activeTransport.GenerateMessageId(true);
                    }
                    ((TLTransportMessage)transportMessage).SessionId = _activeTransport.SessionId.Value;


                    // TODO: replace with SendInformativeMessage
                    TLUtils.WriteLine("Corrected client time: " + TLUtils.MessageIdString(transportMessage.MsgId));
                    authKey = _activeTransport.AuthKey;
                    encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

                    lock (_historyRoot)
                    {
                        _history.Remove(previousMessageId);
                        _history[historyItem.Hash] = historyItem;
                    }
                    faultCallback = historyItem.FaultCallback;

                    lock (_activeTransportRoot)
                    {
                        if (_activeTransport.Closed)
                        {
                            var transportDCId = _activeTransport.DCId;
                            var transportKey = _activeTransport.AuthKey;
                            var transportSalt = _activeTransport.Salt;
                            var transportSessionId = _activeTransport.SessionId;
                            var transportSequenceNumber = _activeTransport.SequenceNumber;
                            var transportClientTicksDelta = _activeTransport.ClientTicksDelta;
                            bool isCreated;
                            _activeTransport = _transportService.GetTransport(_activeTransport.Host, _activeTransport.Port, Type, out isCreated);
                            if (isCreated)
                            {
                                _activeTransport.DCId = transportDCId;
                                _activeTransport.AuthKey = transportKey;
                                _activeTransport.Salt = transportSalt;
                                _activeTransport.SessionId = transportSessionId;
                                _activeTransport.SequenceNumber = transportSequenceNumber;
                                _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                                _activeTransport.PacketReceived += OnPacketReceived;
                            }
                        }
                    }
                    Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2,-4} SessionId {3} BadMsgId {4}", string.Format("{0}: {1}", historyItem.Caption, "seqNo"), transportMessage.MsgId, transportMessage.SeqNo, message.SessionId, badMessage.BadMsgId);

                    captionString = string.Format("{0} {1} {2}", historyItem.Caption, message.SessionId, transportMessage.MsgId);
                    SendPacketAsync(_activeTransport, captionString, encryptedMessage, (result) =>
                    {
                        Debug.WriteLine("@{0} {1} result {2}", string.Format("{0}: {1}", historyItem.Caption, "seqNo"), transportMessage.MsgId, result);
                    }, 
                    (error) =>
                    {
                        if (faultCallback != null)
                        {
                            faultCallback.Invoke(null);
                        }
                    });
                    break;
            }
        }



        private void ProcessBadServerSalt(TLTransportMessage message, TLBadServerSalt badServerSalt, HistoryItem historyItem)
        {
            if (historyItem == null)
            {
                return;
            }

            var transportMessage = (TLContainerTransportMessage)historyItem.Message;
            lock (_historyRoot)
            {
                _history.Remove(historyItem.Hash);
            }
#if DEBUG
            NotifyOfPropertyChange(() => History);
#endif

            TLUtils.WriteLine("CORRECT SERVER SALT:");
            ((TLTransportMessage)transportMessage).Salt = badServerSalt.NewServerSalt;
            //Salt = badServerSalt.NewServerSalt;
            TLUtils.WriteLine("New salt: " + _activeTransport.Salt);

            switch (badServerSalt.ErrorCode)
            {
                case 16:
                case 17:
                    TLUtils.WriteLine("3. CORRECT TIME DELTA with Salt by activeTransport " + _activeTransport.DCId);

                    var saveConfig = false;
                    lock (_activeTransportRoot)
                    {
                        var serverTime = message.MsgId;
                        TLUtils.WriteLine("Server time: " + TLUtils.MessageIdString(BitConverter.GetBytes(serverTime)));
                        var clientTime = _activeTransport.GenerateMessageId();
                        TLUtils.WriteLine("Client time: " + TLUtils.MessageIdString(BitConverter.GetBytes(clientTime)));

                        if (historyItem.ClientTicksDelta == _activeTransport.ClientTicksDelta)
                        {
                            saveConfig = true;
                            _activeTransport.ClientTicksDelta += serverTime - clientTime;
                        }

                        transportMessage.MsgId = _activeTransport.GenerateMessageId(true);
                        TLUtils.WriteLine("Corrected client time: " + TLUtils.MessageIdString(transportMessage.MsgId));
                    }

                    if (saveConfig && _config != null)
                    {
                        var dcOption = _config.DCOptions.FirstOrDefault(x => string.Equals(x.IpAddress.ToString(), _activeTransport.Host, StringComparison.OrdinalIgnoreCase));
                        if (dcOption != null)
                        {
                            dcOption.ClientTicksDelta = _activeTransport.ClientTicksDelta;
                            _cacheService.SetConfig(_config);
                        }
                    }

                    break;
                case 48:
                    break;
            }

            if (transportMessage == null) return;

            var authKey = _activeTransport.AuthKey;
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);
            lock (_historyRoot)
            {
                _history[historyItem.Hash] = historyItem;
            }
            var faultCallback = historyItem.FaultCallback;

            lock (_activeTransportRoot)
            {
                if (_activeTransport.Closed)
                {
                    var transportDCId = _activeTransport.DCId;
                    var transportKey = _activeTransport.AuthKey;
                    var transportSalt = _activeTransport.Salt;
                    var transportSessionId = _activeTransport.SessionId;
                    var transportSequenceNumber = _activeTransport.SequenceNumber;
                    var transportClientTicksDelta = _activeTransport.ClientTicksDelta;
                    bool isCreated;
                    _activeTransport = _transportService.GetTransport(_activeTransport.Host, _activeTransport.Port, Type, out isCreated);
                    if (isCreated)
                    {
                        _activeTransport.DCId = transportDCId;
                        _activeTransport.AuthKey = transportKey;
                        _activeTransport.Salt = transportSalt;
                        _activeTransport.SessionId = transportSessionId;
                        _activeTransport.SequenceNumber = transportSequenceNumber;
                        _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                        _activeTransport.PacketReceived += OnPacketReceived;
                    }
                }
            }

            var captionString = string.Format("{0} {1} {2}", historyItem.Caption, message.SessionId, transportMessage.MsgId);
            SendPacketAsync(_activeTransport, captionString, encryptedMessage, (result) =>
            {
                Debug.WriteLine("@{0} {1} result {2}", historyItem.Caption, transportMessage.MsgId, result);
            }, 
            (error) =>
            {
                if (faultCallback != null)
                {
                    faultCallback.Invoke(new TLRPCError());
                }
            });
        }


        private readonly IDisposable _statusSubscription;

        public event EventHandler<SendStatusEventArgs> SendStatus;

        public void RaiseSendStatus(SendStatusEventArgs e)
        {
            var handler = SendStatus;
            if (handler != null) handler(this, e);
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
                    NotifyOfPropertyChange(() => Message);
                }
            }
        }

        private DispatcherTimer _messageScheduler;

        public void SetMessageOnTimeAsync(double seconds, string message)
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
}
