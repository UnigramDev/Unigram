﻿using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Connection;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Messages;
using Telegram.Api.TL.Methods.Phone;
using Telegram.Api.Transport;
using Unigram.Core;
using Unigram.Core.Services;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Calls;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Unigram.Tasks
{
    public sealed class VoIPCallTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        private static VoIPCallMediator _mediator;

        internal static VoIPCallMediator Mediator
        {
            get
            {
                if (_mediator == null)
                    _mediator = new VoIPCallMediator();

                return _mediator;
            }
        }

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();

            TLPushUtils.AddToast("VoIPCallTask started", "VoIPCallTask started", "default", "started", null, "voip1", "voip");

            Mediator.Initialize(_deferral);
            taskInstance.Canceled += OnCanceled;


            //var coordinator = VoipCallCoordinator.GetDefault();
            //var call = coordinator.RequestNewIncomingCall("Unigram", "Lumia 435", "Lumia 435", null, "Unigram", null, "Unigram", null, VoipPhoneCallMedia.Audio, TimeSpan.FromSeconds(128));

            //_systemCall = call;
            //_systemCall.AnswerRequested += _systemCall_AnswerRequested;
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _deferral.Complete();
        }
    }

    internal class VoIPCallMediator : IHandle<TLUpdatePhoneCall>, IHandle
    {
        private readonly Queue<TLUpdatePhoneCall> _queue = new Queue<TLUpdatePhoneCall>();

        private AppServiceConnection _connection;
        private MTProtoService _protoService;
        private TransportService _transportService;

        private VoipPhoneCall _systemCall;
        private TLPhoneCallBase _phoneCall;
        private TLUserBase _user;

        private BackgroundTaskDeferral _deferral;
        private bool _initialized;

        public async void Initialize(AppServiceConnection connection)
        {
            if (_connection != null)
            {
                _connection = connection;
                _connection.RequestReceived += OnRequestReceived;
            }
            else
            {
                _connection.RequestReceived -= OnRequestReceived;
                _connection = null;
            }

            if (_protoService != null)
            {
                _protoService.Dispose();
                _transportService.Close();
            }

            if (_phoneCall != null && _connection != null)
            {
                await _connection.SendMessageAsync(new ValueSet { { "caption", "voip.callInfo" }, { "request", TLSerializationService.Current.Serialize(_phoneCall) } });
            }
        }

        public void Initialize(BackgroundTaskDeferral deferral)
        {
            if (_connection == null & _protoService == null)
            {
                TLPushUtils.AddToast("Mediator initialized", "Creating proto service", "default", "started", null, "voip3", "voip");

                var deviceInfoService = new DeviceInfoService();
                var eventAggregator = new TelegramEventAggregator();
                var cacheService = new InMemoryCacheService(eventAggregator);
                var updatesService = new UpdatesService(cacheService, eventAggregator);
                var transportService = new TransportService();
                var connectionService = new ConnectionService(deviceInfoService);
                var statsService = new StatsService();
                var protoService = new MTProtoService(deviceInfoService, updatesService, cacheService, transportService, connectionService, statsService);

                protoService.Initialized += (s, args) =>
                {
                    TLPushUtils.AddToast("ProtoService initialized", "waiting for updates", "default", "started", null, "voip3", "voip");
                };

                eventAggregator.Subscribe(this);
                protoService.Initialize();
                updatesService.LoadStateAndUpdate(() => { });

                _protoService = protoService;
                _transportService = transportService;
            }
            else
            {
                TLPushUtils.AddToast("Mediator initialized", "_connection is null: " + (_connection == null), "default", "started", null, "voip3", "voip");
            }

            _deferral = deferral;
            _initialized = true;

            ProcessUpdates();
        }

        private async void ProcessUpdates()
        {
            while (_queue.Count > 0)
            {
                var update = _queue.Dequeue();
                if (update.PhoneCall is TLPhoneCallRequested requested)
                {
                    _phoneCall = requested;

                    var req = new TLPhoneReceivedCall { Peer = new TLInputPhoneCall { Id = requested.Id, AccessHash = requested.AccessHash } };

                    const string caption = "phone.receivedCall";
                    var response = await SendRequestAsync<bool>(caption, req);

                    var responseUser = await SendRequestAsync<TLUser>("voip.getUser", new TLPeerUser { UserId = requested.AdminId });
                    if (responseUser.Result == null)
                    {
                        return;
                    }

                    var user = responseUser.Result;

                    var coordinator = VoipCallCoordinator.GetDefault();
                    var call = coordinator.RequestNewIncomingCall("Unigram", user.FullName, user.DisplayName, null, "Unigram", null, "Unigram", null, VoipPhoneCallMedia.Audio, TimeSpan.FromSeconds(128));

                    _user = user;
                    _systemCall = call;
                    _systemCall.AnswerRequested += OnAnswerRequested;
                    _systemCall.RejectRequested += OnRejectRequested;
                }
                else if (update.PhoneCall is TLPhoneCallDiscarded)
                {
                    _deferral.Complete();
                }
                else if (update.PhoneCall is TLPhoneCall call)
                {
                    var auth_key = computeAuthKey(call);
                    var g_a = call.GAOrB;

                    var buffer = TLUtils.Combine(auth_key, g_a);
                    var sha256 = Utils.ComputeSHA256(buffer);

                    var emoji = EncryptionKeyEmojifier.EmojifyForCall(sha256);

                    await UpdateCallAsync(string.Join(" ", emoji));
                    await Task.Delay(50000);

                    var req = new TLPhoneDiscardCall { Peer = new TLInputPhoneCall { Id = call.Id, AccessHash = call.AccessHash }, Reason = new TLPhoneCallDiscardReasonHangup() };

                    const string caption = "phone.discardCall";
                    await SendRequestAsync<TLUpdatesBase>(caption, req);

                    _systemCall.NotifyCallEnded();
                }
            }
        }

        private async Task UpdateCallAsync(string emoji)
        {
            var data = TLTuple.Create(_phoneCall, _user, emoji);
            await _connection.SendMessageAsync(new ValueSet { { "caption", "voip.callInfo" }, { "request", TLSerializationService.Current.Serialize(data) } });
        }

        private byte[] secretP;
        private byte[] a_or_b;

        private async void OnAnswerRequested(VoipPhoneCall sender, CallAnswerEventArgs args)
        {
            if (_phoneCall is TLPhoneCallRequested requested)
            {
                var reqConfig = new TLMessagesGetDHConfig { Version = 0, RandomLength = 256 };

                var config = await SendRequestAsync<TLMessagesDHConfig>("messages.getDhConfig", reqConfig);
                if (config.IsSucceeded)
                {
                    var dh = config.Result;
                    if (!TLUtils.CheckPrime(dh.P, dh.G))
                    {
                        return;
                    }

                    secretP = dh.P;

                    var salt = new byte[256];
                    var secureRandom = new SecureRandom();
                    secureRandom.NextBytes(salt);

                    a_or_b = salt;

                    var g_b = MTProtoService.GetGB(salt, dh.G, dh.P);

                    var request = new TLPhoneAcceptCall
                    {
                        GB = g_b,
                        Peer = new TLInputPhoneCall
                        {
                            Id = requested.Id,
                            AccessHash = requested.AccessHash
                        },
                        Protocol = new TLPhoneCallProtocol
                        {
                            IsUdpP2p = true,
                            IsUdpReflector = true,
                            MinLayer = 65,
                            MaxLayer = 65,
                        }
                    };

                    var response = await SendRequestAsync<TLPhonePhoneCall>("phone.acceptCall", request);
                    if (response.IsSucceeded)
                    {
                    }
                }

                _systemCall.NotifyCallActive();
            }
        }

        private byte[] computeAuthKey(TLPhoneCall call)
        {
            BigInteger g_a = new BigInteger(1, call.GAOrB);
            BigInteger p = new BigInteger(1, secretP);

            g_a = g_a.ModPow(new BigInteger(1, a_or_b), p);

            byte[] authKey = g_a.ToByteArray();
            if (authKey.Length > 256)
            {
                byte[] correctedAuth = new byte[256];
                Buffer.BlockCopy(authKey, authKey.Length - 256, correctedAuth, 0, 256);
                authKey = correctedAuth;
            }
            else if (authKey.Length < 256)
            {
                byte[] correctedAuth = new byte[256];
                Buffer.BlockCopy(authKey, 0, correctedAuth, 256 - authKey.Length, authKey.Length);
                for (int a = 0; a < 256 - authKey.Length; a++)
                {
                    authKey[a] = 0;
                }
                authKey = correctedAuth;
            }
            byte[] authKeyHash = Utils.ComputeSHA1(authKey);
            byte[] authKeyId = new byte[8];
            Buffer.BlockCopy(authKeyHash, authKeyHash.Length - 8, authKeyId, 0, 8);

            return authKey;
        }

        private async void OnRejectRequested(VoipPhoneCall sender, CallRejectEventArgs args)
        {
            if (_phoneCall is TLPhoneCallRequested requested)
            {
                var req = new TLPhoneDiscardCall { Peer = new TLInputPhoneCall { Id = requested.Id, AccessHash = requested.AccessHash }, Reason = new TLPhoneCallDiscardReasonHangup() };

                const string caption = "phone.discardCall";
                await SendRequestAsync<TLUpdatesBase>(caption, req);

                _systemCall.NotifyCallEnded();
            }
        }

        public async Task<MTProtoResponse<T>> SendRequestAsync<T>(string caption, TLObject request)
        {
            if (_protoService != null)
            {
                if (caption.Equals("voip.getUser"))
                {
                    return new MTProtoResponse<T>(InMemoryCacheService.Current.GetUser(((TLPeerUser)request).UserId));
                }

                return await _protoService.SendRequestAsync<T>(caption, request);
            }
            else
            {

                var response = await _connection.SendMessageAsync(new ValueSet { { nameof(caption), caption }, { nameof(request), TLSerializationService.Current.Serialize(request) } });
                if (response.Status == AppServiceResponseStatus.Success)
                {
                    if (response.Message.ContainsKey("result"))
                    {
                        return new MTProtoResponse<T>(TLSerializationService.Current.Deserialize(response.Message["result"] as string));
                    }
                    else if (response.Message.ContainsKey("error"))
                    {
                        return new MTProtoResponse<T>(TLSerializationService.Current.Deserialize<TLRPCError>(response.Message["error"] as string));
                    }
                }

                return new MTProtoResponse<T>(new TLRPCError { ErrorMessage = "UNKNOWN", ErrorCode = (int)response.Status });
            }
        }

        private async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var deferral = args.GetDeferral();
            var message = args.Request.Message;

            if (message.ContainsKey("update"))
            {
                var buffer = message["update"] as string;
                var update = TLSerializationService.Current.Deserialize(buffer) as TLUpdatePhoneCall;
                if (update != null)
                {
                    Debug.WriteLine(update.PhoneCall);
                    _queue.Enqueue(update);

                    if (_initialized)
                    {
                        ProcessUpdates();
                    }
                }
            }
            else if (message.ContainsKey("caption"))
            {
                var caption = message["caption"] as string;
                if (caption.Equals("phone.discardCall"))
                {
                    if (_phoneCall is TLPhoneCallRequested requested)
                    {
                        var req = new TLPhoneDiscardCall { Peer = new TLInputPhoneCall { Id = requested.Id, AccessHash = requested.AccessHash }, Reason = new TLPhoneCallDiscardReasonHangup() };

                        const string caption2 = "phone.discardCall";
                        await SendRequestAsync<TLUpdatesBase>(caption2, req);

                        _systemCall.NotifyCallEnded();
                    }
                }
            }
            else if (message.ContainsKey("voip.callInfo"))
            {
                if (_phoneCall != null)
                {
                    await args.Request.SendResponseAsync(new ValueSet { { "result", TLSerializationService.Current.Serialize(_phoneCall) } });
                }
                else
                {
                    await args.Request.SendResponseAsync(new ValueSet { { "error", false } });
                }
            }

            deferral.Complete();
        }

        public void Handle(TLUpdatePhoneCall update)
        {
            Debug.WriteLine(update.PhoneCall);
            _queue.Enqueue(update);

            if (_initialized)
            {
                ProcessUpdates();
            }
        }
    }
}
