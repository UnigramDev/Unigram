using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Org.BouncyCastle.Security;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Auth;
using Telegram.Api.TL.Functions.DHKeyExchange;
using Telegram.Api.TL.Functions.Help;
using Telegram.Api.TL.Functions.Messages;
using Telegram.Api.TL.Functions.Stuff;
using Telegram.Api.TL.Functions.Upload;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        private void ReqPQByTransportAsync(ITransport transport, TLInt128 nonce, Action<TLResPQ> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLReqPQ { Nonce = nonce };

            SendNonEncryptedMessageByTransport(transport, "req_pq", obj, callback, faultCallback);
        }

        private void ReqDHParamsByTransportAsync(ITransport transport, TLInt128 nonce, TLInt128 serverNonce, TLString p, TLString q, TLLong publicKeyFingerprint, TLString encryptedData, Action<TLServerDHParamsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLReqDHParams { Nonce = nonce, ServerNonce = serverNonce, P = p, Q = q, PublicKeyFingerprint = publicKeyFingerprint, EncryptedData = encryptedData };

            SendNonEncryptedMessageByTransport(transport, "req_DH_params", obj, callback, faultCallback);
        }

        public void SetClientDHParamsByTransportAsync(ITransport transport, TLInt128 nonce, TLInt128 serverNonce, TLString encryptedData, Action<TLDHGenBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSetClientDHParams { Nonce = nonce, ServerNonce = serverNonce, EncryptedData = encryptedData };

            SendNonEncryptedMessageByTransport(transport, "set_client_DH_params", obj, callback, faultCallback);
        }

        public void InitTransportAsync(ITransport transport, Action<WindowsPhone.Tuple<byte[], TLLong, TLLong>> callback, Action<TLRPCError> faultCallback = null)
        {
            var authTime = Stopwatch.StartNew();
            var newNonce = TLInt256.Random();

#if LOG_REGISTRATION
            TLUtils.WriteLog("Start ReqPQ");
#endif
            var nonce = TLInt128.Random();
            ReqPQByTransportAsync(
                transport, 
                nonce,
                resPQ =>
                {
                    var serverNonce = resPQ.ServerNonce;
                    if (!TLUtils.ByteArraysEqual(nonce.Value, resPQ.Nonce.Value))
                    {
                        var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect nonce") };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqPQ with error " + error);
#endif

                        if (faultCallback != null) faultCallback(error);
                        TLUtils.WriteLine(error.ToString());
                    }

#if LOG_REGISTRATION
                    TLUtils.WriteLog("Stop ReqPQ");
#endif
                    TimeSpan calcTime;
                    WindowsPhone.Tuple<ulong, ulong> pqPair;
                    var innerData = GetInnerData(resPQ, newNonce, out calcTime, out pqPair);
                    var encryptedInnerData = GetEncryptedInnerData(innerData);

#if LOG_REGISTRATION
                    TLUtils.WriteLog("Start ReqDHParams");
#endif
                    ReqDHParamsByTransportAsync(
                        transport,
                        resPQ.Nonce,
                        resPQ.ServerNonce,
                        innerData.P,
                        innerData.Q,
                        resPQ.ServerPublicKeyFingerprints[0],
                        encryptedInnerData,
                        serverDHParams =>
                        {
                            if (!TLUtils.ByteArraysEqual(nonce.Value, serverDHParams.Nonce.Value))
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect nonce") };
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());
                            }
                            if (!TLUtils.ByteArraysEqual(serverNonce.Value, serverDHParams.ServerNonce.Value))
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect server_nonce") };
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());
                            }

#if LOG_REGISTRATION
                            TLUtils.WriteLog("Stop ReqDHParams");
#endif
                            var random = new SecureRandom();

                            var serverDHParamsOk = serverDHParams as TLServerDHParamsOk;
                            if (serverDHParamsOk == null)
                            {
                                var error = new TLRPCError{Code = new TLInt(404), Message = new TLString("Incorrect serverDHParams")};
                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());
#if LOG_REGISTRATION
                            
                                TLUtils.WriteLog("ServerDHParams " + serverDHParams);  
#endif
                                return;
                            }

                            var aesParams = GetAesKeyIV(resPQ.ServerNonce.ToBytes(), newNonce.ToBytes());

                            var decryptedAnswerWithHash = Utils.AesIge(serverDHParamsOk.EncryptedAnswer.Data, aesParams.Item1, aesParams.Item2, false);     //NOTE: Remove reverse here

                            var position = 0;
                            var serverDHInnerData = (TLServerDHInnerData)new TLServerDHInnerData().FromBytes(decryptedAnswerWithHash.Skip(20).ToArray(), ref position);

                            var sha1 = Utils.ComputeSHA1(serverDHInnerData.ToBytes());
                            if (!TLUtils.ByteArraysEqual(sha1, decryptedAnswerWithHash.Take(20).ToArray()))
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect sha1 TLServerDHInnerData") };
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());
                            }

                            if (!TLUtils.CheckPrime(serverDHInnerData.DHPrime.Data, serverDHInnerData.G.Value))
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect (p, q) pair") };
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());
                            }

                            if (!TLUtils.CheckGaAndGb(serverDHInnerData.GA.Data, serverDHInnerData.DHPrime.Data))
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect g_a") };
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());
                            }

                            var bBytes = new byte[256]; //big endian B
                            random.NextBytes(bBytes);

                            var gbBytes = GetGB(bBytes, serverDHInnerData.G, serverDHInnerData.DHPrime);

                            var clientDHInnerData = new TLClientDHInnerData
                            {
                                Nonce = resPQ.Nonce,
                                ServerNonce = resPQ.ServerNonce,
                                RetryId = new TLLong(0),
                                GB = TLString.FromBigEndianData(gbBytes)
                            };

                            var encryptedClientDHInnerData = GetEncryptedClientDHInnerData(clientDHInnerData, aesParams);
#if LOG_REGISTRATION                 
                            TLUtils.WriteLog("Start SetClientDHParams");  
#endif
                            SetClientDHParamsByTransportAsync(
                                transport,
                                resPQ.Nonce, 
                                resPQ.ServerNonce, 
                                encryptedClientDHInnerData,
                                dhGen =>
                                {
                                    if (!TLUtils.ByteArraysEqual(nonce.Value, dhGen.Nonce.Value))
                                    {
                                        var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect nonce") };
#if LOG_REGISTRATION
                                        TLUtils.WriteLog("Stop SetClientDHParams with error " + error);
#endif

                                        if (faultCallback != null) faultCallback(error);
                                        TLUtils.WriteLine(error.ToString());
                                    }
                                    if (!TLUtils.ByteArraysEqual(serverNonce.Value, dhGen.ServerNonce.Value))
                                    {
                                        var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect server_nonce") };
#if LOG_REGISTRATION
                                        TLUtils.WriteLog("Stop SetClientDHParams with error " + error);
#endif

                                        if (faultCallback != null) faultCallback(error);
                                        TLUtils.WriteLine(error.ToString());
                                    }

                                    var dhGenOk = dhGen as TLDHGenOk;
                                    if (dhGenOk == null)
                                    {
                                        var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("Incorrect dhGen " + dhGen.GetType()) };
                                        if (faultCallback != null) faultCallback(error);
                                        TLUtils.WriteLine(error.ToString());
#if LOG_REGISTRATION
                                        TLUtils.WriteLog("DHGen result " + serverDHParams);
#endif
                                        return;
                                    }

#if LOG_REGISTRATION
                                    TLUtils.WriteLog("Stop SetClientDHParams");
#endif
                                    var getKeyTimer = Stopwatch.StartNew();
                                    var authKey = GetAuthKey(bBytes, serverDHInnerData.GA.ToBytes(), serverDHInnerData.DHPrime.ToBytes());

#if LOG_REGISTRATION
                                    var logCountersString = new StringBuilder();

                                    logCountersString.AppendLine("Auth Counters");
                                    logCountersString.AppendLine();
                                    logCountersString.AppendLine("pq factorization time: " + calcTime);
                                    logCountersString.AppendLine("calc auth key time: " + getKeyTimer.Elapsed);
                                    logCountersString.AppendLine("auth time: " + authTime.Elapsed);

                                    TLUtils.WriteLog(logCountersString.ToString());
#endif
                                    //newNonce - little endian
                                    //authResponse.ServerNonce - little endian
                                    var salt = GetSalt(newNonce.ToBytes(), resPQ.ServerNonce.ToBytes());
                                    var sessionId = new byte[8];
                                    random.NextBytes(sessionId);

                                    // authKey, salt, sessionId
                                    callback(new WindowsPhone.Tuple<byte[], TLLong, TLLong>(authKey, new TLLong(BitConverter.ToInt64(salt, 0)), new TLLong(BitConverter.ToInt64(sessionId, 0))));
                                },
                                error =>
                                {
#if LOG_REGISTRATION
                                    TLUtils.WriteLog("Stop SetClientDHParams with error " + error.ToString());
#endif
                                    if (faultCallback != null) faultCallback(error);
                                    TLUtils.WriteLine(error.ToString());
                                });
                        },
                        error =>
                        {
#if LOG_REGISTRATION
                            TLUtils.WriteLog("Stop ReqDHParams with error " + error.ToString());
#endif
                            if (faultCallback != null) faultCallback(error);
                            TLUtils.WriteLine(error.ToString());
                        });
                },
                error =>
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog("Stop ReqPQ with error " + error.ToString());
#endif
                    if (faultCallback != null) faultCallback(error);
                    TLUtils.WriteLine(error.ToString());
                });
        }

        public void LogOutTransportsAsync(Action callback, Action<List<TLRPCError>> faultCallback = null)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var dcOptions = _config.DCOptions;
                var activeDCOptionIndex = _config.ActiveDCOptionIndex;

                if (dcOptions.Count > 1)
                {
                    var waitHandles = new List<WaitHandle>();
                    var errors = new List<TLRPCError>();
                    for (var i = 0; i < dcOptions.Count; i++)
                    {
                        if (activeDCOptionIndex == i)
                        {
                            continue;
                        }

                        var local = i; 
                        var handle = new ManualResetEvent(false);
                        waitHandles.Add(handle);
                        Execute.BeginOnThreadPool(() =>
                        {
                            var dcOption = dcOptions[local];
                            LogOutAsync(dcOption.Id,
                                result =>
                                {
                                    handle.Set();
                                },
                                error =>
                                {
                                    errors.Add(error);
                                    handle.Set();
                                });
                        });
                    }

                    var waitingResult = WaitHandle.WaitAll(waitHandles.ToArray(), TimeSpan.FromSeconds(25.0));
                    if (waitingResult)
                    {
                        if (errors.Count > 0)
                        {
                            faultCallback.SafeInvoke(errors);
                        }
                        else
                        {
                            callback.SafeInvoke();
                        }
                    }
                    else
                    {
                        faultCallback.SafeInvoke(errors);
                    }
                }
                else
                {
                    callback.SafeInvoke();
                }
            });
        }

        public void LogOutAsync(TLInt dcId, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            lock (_activeTransportRoot)
            {
                if (_activeTransport.DCId == dcId.Value)
                {
                    if (_activeTransport.DCId == 0)
                    {
                        TLUtils.WriteException(new Exception("_activeTransport.DCId==0"));
                    }

                    LogOutAsync(callback, faultCallback);
                    return;
                }
            }

            var transport = GetMediaTransportByDCId(dcId);

            if (transport == null)
            {
                faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404), Message = new TLString("LogOutAsync: Empty transport for dc id " + dcId) });

                return;
            }

            if (transport.AuthKey == null)
            {
                faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404), Message = new TLString("LogOutAsync: Empty authKey for dc id " + dcId) });

                return;
            }

            var obj = new TLLogOut();

            SendInformativeMessageByTransport<TLBool>(transport, "auth.logOut", obj,
                result =>
                {
                    lock (transport.SyncRoot)
                    {
                        transport.IsInitializing = false;
                        transport.IsAuthorizing = false;
                        transport.IsAuthorized = false;
                    }
                }, 
                faultCallback);
        }

        public void GetFileAsync(TLInt dcId, TLInputFileLocationBase location, TLInt offset, TLInt limit, Action<TLFile> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetFile { Location = location, Offset = offset, Limit = limit };

            var transport = GetMediaTransportByDCId(dcId);

            lock (_activeTransportRoot)
            {
                if (_activeTransport.DCId == dcId.Value)
                {
                    if (_activeTransport.DCId == 0)
                    {
                        TLUtils.WriteException(new Exception("_activeTransport.DCId==0"));
                    }

                    SendInformativeMessageByTransport(transport, string.Format("upload.getFile main dc_id={0} loc=[{5}] o={1} l={2}\ntransport_id={3} session_id={4}", dcId, offset, limit, transport.Id, transport.SessionId, location.GetLocationString()), obj, callback, faultCallback);
                    return;
                }
            }

            if (transport == null)
            {
                faultCallback.SafeInvoke(new TLRPCError{Code = new TLInt(404), Message = new TLString("GetFileAsync: Empty transport for dc id " + dcId)});

                return;
            }

            if (transport.AuthKey == null)
            {
                var cancelInitializing = false;
                lock (transport.SyncRoot)
                {
                    if (transport.IsInitializing)
                    {
                        cancelInitializing = true;
                    }
                    else
                    {
                        transport.IsInitializing = true;
                    }
                }

                if (cancelInitializing)
                {
                    faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404), Message = new TLString("DC " + dcId + " is already initializing") });
                    return;
                }

                InitTransportAsync(
                    transport,
                    tuple =>
                    {
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

                        ExportImportAuthorizationAsync(
                            transport,
                            () =>
                            {
                                foreach (var dcOption in _config.DCOptions)
                                {
                                    if (dcOption.Id.Value == transport.DCId)
                                    {
                                        dcOption.AuthKey = tuple.Item1;
                                        dcOption.Salt = tuple.Item2;
                                        dcOption.SessionId = tuple.Item3;
                                    }
                                }

                                _cacheService.SetConfig(_config);

                                SendInformativeMessageByTransport(transport, string.Format("upload.getFile dc_id={0} loc=[{3}] o={1} l={2}", dcId, offset, limit, location.GetLocationString()), obj, callback, faultCallback);
                            },
                            error =>
                            {
                                if (!error.CodeEquals(ErrorCode.NOT_FOUND) &&
                                    !error.Message.ToString().Contains("is already authorizing"))
                                {
                                    Execute.ShowDebugMessage("ExportImportAuthorization error " + error);
                                }

                                faultCallback.SafeInvoke(error);
                            });
                    },
                    error =>
                    {
                        lock (transport.SyncRoot)
                        {
                            transport.IsInitializing = false;
                        }

                        faultCallback.SafeInvoke(error);
                    });
            }
            else
            {
                ExportImportAuthorizationAsync(
                    transport,
                    () =>
                    {
                        SendInformativeMessageByTransport(transport, string.Format("upload.getFile dc_id={0} loc=[{3}] o={1} l={2}", dcId, offset, limit, location.GetLocationString()), obj, callback, faultCallback);
                    },
                    error =>
                    {
                        if (!error.CodeEquals(ErrorCode.NOT_FOUND) 
                            && !error.Message.ToString().Contains("is already authorizing"))
                        {
                            Execute.ShowDebugMessage("ExportImportAuthorization error " + error);
                        }

                        faultCallback.SafeInvoke(error);
                    });
            }
        }

        private void ExportImportAuthorizationAsync(ITransport toTransport, Action callback, Action<TLRPCError> faultCallback = null)
        {
            if (!toTransport.IsAuthorized)
            {
                bool authorizing = false;
                lock (toTransport.SyncRoot)
                {
                    if (toTransport.IsAuthorizing)
                    {
                        authorizing = true;
                    }

                    toTransport.IsAuthorizing = true;
                }

                if (authorizing)
                {
                    faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404), Message = new TLString("DC " + toTransport.DCId + " is already authorizing") });
                    return;
                }

                ExportAuthorizationAsync(
                    new TLInt(toTransport.DCId),
                    exportedAuthorization =>
                    {
                        ImportAuthorizationByTransportAsync(
                            toTransport,
                            exportedAuthorization.Id,
                            exportedAuthorization.Bytes,
                            authorization =>
                            {
                                lock (toTransport.SyncRoot)
                                {
                                    toTransport.IsAuthorized = true; 
                                    toTransport.IsAuthorizing = false;
                                }

                                foreach (var dcOption in _config.DCOptions)
                                {
                                    if (dcOption.Id.Value == toTransport.DCId)
                                    {
                                        dcOption.IsAuthorized = true;
                                    }
                                }

                                _cacheService.SetConfig(_config);

                                callback.SafeInvoke();
                            },
                            error =>
                            {
                                lock (toTransport.SyncRoot)
                                {
                                    toTransport.IsAuthorizing = false;
                                }
                                faultCallback.SafeInvoke(error);
                            });
                        ;
                    },
                    error =>
                    {
                        lock (toTransport.SyncRoot)
                        {
                            toTransport.IsAuthorizing = false;
                        }
                        faultCallback.SafeInvoke(error);
                    });
            }
            else
            {
                callback.SafeInvoke();
            }
        }

        private ITransport GetMediaTransportByDCId(TLInt dcId)
        {
            ITransport transport;
            lock (_activeTransportRoot)
            {
                var dcOption = _config.DCOptions.FirstOrDefault(x => x.IsValidIPv4Option(dcId));// && x.Media.Value);

                if (dcOption == null)
                {
                    dcOption = _config.DCOptions.FirstOrDefault(x => x.IsValidIPv4Option(dcId));
                }

                if (dcOption == null) return null;

                bool isCreated;
                transport = _transportService.GetFileTransport(dcOption.IpAddress.Value, dcOption.Port.Value, Type, out isCreated);
                if (isCreated)
                {
                    transport.DCId = dcId.Value;
                    transport.AuthKey = dcOption.AuthKey;
                    //transport.IsAuthorized = (_activeTransport != null && _activeTransport.DCId == dcOption.Id.Value) || dcOption.IsAuthorized;
                    transport.Salt = dcOption.Salt;
                    transport.SessionId = TLLong.Random();
                    transport.SequenceNumber = 0;
                    transport.ClientTicksDelta = dcOption.ClientTicksDelta;
                    transport.PacketReceived += OnPacketReceivedByTransport;
                }
            }

            return transport;
        }

        private readonly object _initConnectionSyncRoot = new object();

        private void SendInformativeMessageByTransport<T>(ITransport transport, string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null,
            int? maxAttempt = null,                 // to send delayed items
            Action<int> attemptFailed = null)       // to send delayed items
            where T : TLObject
        {
            bool isInitialized;
            lock (transport.SyncRoot)
            {
                isInitialized = transport.AuthKey != null;
            }

            if (!isInitialized)
            {
                var delayedItem = new DelayedItem
                {
                    SendTime = DateTime.Now,
                    Caption = caption,
                    Object = obj,
                    Callback = t => callback((T)t),
                    AttemptFailed = attemptFailed,
                    FaultCallback = faultCallback,
                    MaxAttempt = maxAttempt
                };
#if LOG_REGISTRATION
                    TLUtils.WriteLog(DateTime.Now.ToLocalTime() + ": Enqueue delayed item\n " + delayedItem); 
#endif
                lock (_delayedItemsRoot)
                {
                    _delayedItems.Add(delayedItem);
                }

                return;
            }

            lock (transport.SyncRoot)
            {
                if (transport.Closed)
                {
                    var transportDCId = transport.DCId;
                    var transportKey = transport.AuthKey;
                    var transportSalt = transport.Salt;
                    var transportSessionId = transport.SessionId;
                    var transportSequenceNumber = transport.SequenceNumber;
                    var transportClientTicksDelta = transport.ClientTicksDelta;
                    bool isCreated;
                    transport = _transportService.GetFileTransport(transport.Host, transport.Port, Type, out isCreated);
                    if (isCreated)
                    {
                        transport.DCId = transportDCId;
                        transport.AuthKey = transportKey;
                        transport.Salt = transportSalt;
                        transport.SessionId = transportSessionId;
                        transport.SequenceNumber = transportSequenceNumber;
                        transport.ClientTicksDelta = transportClientTicksDelta;
                        transport.PacketReceived += OnPacketReceivedByTransport;
                    }
                }
            }

            PrintCaption(caption);

            TLObject data;
            int sequenceNumber;
            TLLong messageId;
            lock (transport.SyncRoot)
            {
                if (!transport.Initiated || caption == "auth.sendCode")
                {
                    var initConnection = new TLInitConnection
                    {
                        AppId = new TLInt(Constants.ApiId),
                        AppVersion = new TLString(_deviceInfo.AppVersion),
                        Data = obj,
                        DeviceModel = new TLString(_deviceInfo.Model),
                        LangCode = new TLString(Utils.CurrentUICulture()),
                        SystemVersion = new TLString(_deviceInfo.SystemVersion)
                    };

                    var withLayerN = new TLInvokeWithLayerN { Data = initConnection };
                    data = withLayerN;
                    transport.Initiated = true;
                }
                else
                {
                    data = obj;
                }

                sequenceNumber = transport.SequenceNumber * 2 + 1;
                transport.SequenceNumber++;
                messageId = transport.GenerateMessageId(true);
            }

            var authKey = transport.AuthKey;
            var salt = transport.Salt;
            var sessionId = transport.SessionId;
            var clientsTicksDelta = transport.ClientTicksDelta;
            var dcId = transport.DCId;
            var transportMessage = CreateTLTransportMessage(salt, sessionId, new TLInt(sequenceNumber), messageId, data);
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

            var historyItem = new HistoryItem
            {
                SendTime = DateTime.Now,
                Caption = caption,
                Object = obj,
                Message = transportMessage,
                Callback = t => callback((T)t),
                FaultCallback = faultCallback,
                ClientTicksDelta = clientsTicksDelta,
                Status = RequestStatus.Sent,

                DCId = dcId
            };

            lock (_historyRoot)
            {
                _history[historyItem.Hash] = historyItem;
            }
#if DEBUG
            NotifyOfPropertyChange(() => History);
#endif

            //Debug.WriteLine(">> {4} {0, -30} MsgId {1} SeqNo {2, -4} SessionId {3}", caption, transportMessage.MessageId, transportMessage.SeqNo, transportMessage.SessionId, historyItem.DCId);
            var captionString = string.Format("{0} {1} {2}", caption, transportMessage.SessionId, transportMessage.MessageId);
            SendPacketAsync(transport, captionString,
                encryptedMessage,
                result =>
                {
                    if (!result)
                    {
                        lock (_historyRoot)
                        {
                            _history.Remove(historyItem.Hash);
                        }
#if DEBUG
                        NotifyOfPropertyChange(() => History);
#endif
                        faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404), Message = new TLString("FastCallback SocketError=" + result) });
                    }
                },
                error =>
                {
                    lock (_historyRoot)
                    {
                        _history.Remove(historyItem.Hash);
                    }
#if DEBUG
                    NotifyOfPropertyChange(() => History);
#endif
                    faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404) });
                });
        }

        private void SaveInitConnectionAsync(TLInitConnection initConnection)
        {
            Execute.BeginOnThreadPool(() => TLUtils.SaveObjectToMTProtoFile(_initConnectionSyncRoot, Constants.InitConnectionFileName, initConnection));
        }

        private void SendNonEncryptedMessageByTransport<T>(ITransport transport, string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null) where T : TLObject
        {
            PrintCaption(caption);

            TLLong messageId;
            lock (transport.SyncRoot)
            {
                messageId = transport.GenerateMessageId();
            }
            var message = CreateTLNonEncryptedMessage(messageId, obj);

            var historyItem = new HistoryItem
            {
                Caption = caption,
                Message = message,
                Callback = t => callback((T)t),
                FaultCallback = faultCallback,
                SendTime = DateTime.Now,
                Status = RequestStatus.Sent
            };

            var guid = message.MessageId;
            lock (transport.SyncRoot)
            {
                if (transport.Closed)
                {
                    var transportDCId = transport.DCId;
                    var transportKey = transport.AuthKey;
                    var transportSalt = transport.Salt;
                    var transportSessionId = transport.SessionId;
                    var transportSequenceNumber = transport.SequenceNumber;
                    var transportClientTicksDelta = transport.ClientTicksDelta;
                    bool isCreated;
                    lock (_activeTransportRoot)
                    {
                        transport = _transportService.GetTransport(transport.Host, transport.Port, Type, out isCreated);
                    }
                    if (isCreated)
                    {
                        transport.DCId = transportDCId;
                        transport.AuthKey = transportKey;
                        transport.Salt = transportSalt;
                        transport.SessionId = transportSessionId;
                        transport.SequenceNumber = transportSequenceNumber;
                        transport.ClientTicksDelta = transportClientTicksDelta;
                        transport.PacketReceived += OnPacketReceivedByTransport;
                    }
                }
            }

            transport.EnqueueNonEncryptedItem(historyItem);

            var captionString = string.Format("{0} {1}", caption, guid);
            SendPacketAsync(transport, captionString, message,
                socketError =>
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog(caption + " SocketError=" + socketError);
#endif
                    if (!socketError)
                    {
                        transport.RemoveNonEncryptedItem(historyItem);

                        // connection is unsuccessfully
                        faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404), Message = new TLString("FastCallback SocketError=" + socketError) });
                    }
                },
                error =>
                {
                    transport.RemoveNonEncryptedItem(historyItem);

                    faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404) });
                });
        }

        public void MessageAcknowledgmentsByTransport(ITransport transport, TLVector<TLLong> ids)
        {
            PrintCaption("msgs_ack");
            TLUtils.WriteLine("ids");
            foreach (var id in ids)
            {
                TLUtils.WriteLine(TLUtils.MessageIdString(id));
            }
            var obj = new TLMessageAcknowledgments { MsgIds = ids };

            var authKey = transport.AuthKey;
            var sesseionId = transport.SessionId;
            var salt = transport.Salt;

            int sequenceNumber;
            TLLong messageId;
            lock (transport.SyncRoot)
            {
                sequenceNumber = transport.SequenceNumber * 2;
                messageId = transport.GenerateMessageId(true);
            }
            var transportMessage = CreateTLTransportMessage(salt, sesseionId, new TLInt(sequenceNumber), messageId, obj);
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

            lock (transport.SyncRoot)
            {
                if (transport.Closed)
                {
                    var transportDCId = transport.DCId;
                    var transportKey = transport.AuthKey;
                    var transportSalt = transport.Salt;
                    var transportSessionId = transport.SessionId;
                    var transportSequenceNumber = transport.SequenceNumber;
                    var transportClientTicksDelta = transport.ClientTicksDelta;
                    bool isCreated;
                    lock (_activeTransport)
                    {
                        transport = _transportService.GetTransport(transport.Host, transport.Port, Type, out isCreated);
                    }
                    if (isCreated)
                    {
                        transport.DCId = transportDCId;
                        transport.AuthKey = transportKey;
                        transport.Salt = transportSalt;
                        transport.SessionId = transportSessionId;
                        transport.SequenceNumber = transportSequenceNumber;
                        transport.ClientTicksDelta = transportClientTicksDelta;
                        transport.PacketReceived += OnPacketReceivedByTransport;
                    }
                }
            }

            lock (_debugRoot)
            {
                //Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2, -4} SessionId {3}\nids:", "msgs_ack", transportMessage.MessageId.Value, transportMessage.SeqNo.Value, transportMessage.SessionId.Value);
                foreach (var id in ids)
                {
                    Debug.WriteLine(id.Value);
                }
            }

            var captionString = string.Format("msgs_ack {0}", transportMessage.MessageId);
            SendPacketAsync(transport, captionString, encryptedMessage,
                result =>
                {
                    //Debug.WriteLine("@msgs_ack {0} result {1}", transportMessage.MessageId, result);
                    //ReceiveBytesAsync(result, authKey);
                },
                error =>
                {
                    //Debug.WriteLine("<<msgs_ack failed " + transportMessage.MessageId);
                });
        }

        private void ProcessBadMessageByTransport(ITransport transport, TLTransportMessage message, TLBadMessageNotification badMessage, HistoryItem historyItem)
        {
            if (historyItem == null) return;

            switch (badMessage.ErrorCode.Value)
            {
                case 16:
                case 17:
                    var errorInfo = new StringBuilder();
                    errorInfo.AppendLine("0. CORRECT TIME DELTA by Transport " + transport.DCId);
                    errorInfo.AppendLine(historyItem.Caption);

                    lock (_historyRoot)
                    {
                        _history.Remove(historyItem.Hash);
                    }
#if DEBUG
                    NotifyOfPropertyChange(() => History);
#endif
                    
                    var saveConfig = false;
                    lock (transport.SyncRoot)
                    {
                        var serverTime = message.MessageId.Value;
                        var clientTime = transport.GenerateMessageId().Value;

                        var serverDateTime = Utils.UnixTimestampToDateTime(serverTime >> 32);
                        var clientDateTime = Utils.UnixTimestampToDateTime(clientTime >> 32);

                        errorInfo.AppendLine("Server time: " + serverDateTime);
                        errorInfo.AppendLine("Client time: " + clientDateTime);

                        if (historyItem.ClientTicksDelta == transport.ClientTicksDelta)
                        {
                            transport.ClientTicksDelta += serverTime - clientTime;
                            saveConfig = true;
                            errorInfo.AppendLine("Set ticks delta: " + transport.ClientTicksDelta + "(" + (serverDateTime-clientDateTime).TotalSeconds + " seconds)");
                        }
                    }

                    if (saveConfig && _config != null)
                    {
                        var dcOption = _config.DCOptions.FirstOrDefault(x => string.Equals(x.IpAddress.ToString(), transport.Host, StringComparison.OrdinalIgnoreCase));
                        if (dcOption != null)
                        {
                            dcOption.ClientTicksDelta = transport.ClientTicksDelta;
                            _cacheService.SetConfig(_config);
                        }
                    }

                    TLUtils.WriteLine(errorInfo.ToString(), LogSeverity.Error);
                    
                
                    // TODO: replace with SendInformativeMessage
                    var transportMessage = (TLContainerTransportMessage)historyItem.Message;
                    int sequenceNumber;
                    lock (transport.SyncRoot)
                    {
                        if (transportMessage.SeqNo.Value % 2 == 0)
                        {
                            sequenceNumber = 2 * transport.SequenceNumber;
                        }
                        else
                        {
                            sequenceNumber = 2 * transport.SequenceNumber + 1;
                            transport.SequenceNumber++;
                        }

                        transportMessage.SeqNo = new TLInt(sequenceNumber);
                        transportMessage.MessageId = transport.GenerateMessageId(true);
                    }
                    TLUtils.WriteLine("Corrected client time: " + TLUtils.MessageIdString(transportMessage.MessageId));
                    var authKey = transport.AuthKey;
                    var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

                    lock (_historyRoot)
                    {
                        _history[historyItem.Hash] = historyItem;
                    }

                    var faultCallback = historyItem.FaultCallback;

                    lock (transport.SyncRoot)
                    {
                        if (transport.Closed)
                        {
                            var transportDCId = transport.DCId;
                            var transportKey = transport.AuthKey;
                            var transportSalt = transport.Salt;
                            var transportSessionId = transport.SessionId;
                            var transportSequenceNumber = transport.SequenceNumber;
                            var transportClientTicksDelta = transport.ClientTicksDelta;
                            bool isCreated;
                            lock (_activeTransportRoot)
                            {
                                transport = _transportService.GetTransport(transport.Host, transport.Port, Type, out isCreated);
                            }
                            if (isCreated)
                            {
                                transport.DCId = transportDCId;
                                transport.AuthKey = transportKey;
                                transport.Salt = transportSalt;
                                transport.SessionId = transportSessionId;
                                transport.SequenceNumber = transportSequenceNumber;
                                transport.ClientTicksDelta = transportClientTicksDelta;
                                transport.PacketReceived += OnPacketReceivedByTransport;
                            }
                        }
                    }
                    //Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2,-4} SessionId {3} BadMsgId {4}", string.Format("{0}: {1}", historyItem.Caption, "time"), transportMessage.MessageId.Value, transportMessage.SeqNo.Value, message.SessionId.Value, badMessage.BadMessageId.Value);
                    var captionString = string.Format("{0} {1} {2}", historyItem.Caption, message.SessionId, transportMessage.MessageId);
                    SendPacketAsync(transport, captionString,
                        encryptedMessage,
                        result =>
                        {
                            Debug.WriteLine("@{0} {1} result {2}", string.Format("{0}: {1}", historyItem.Caption, "time"), transportMessage.MessageId.Value, result);

                        },//ReceiveBytesAsync(result, authKey),
                    error =>
                    {
                        lock (_historyRoot)
                        {
                            _history.Remove(historyItem.Hash);
                        }
#if DEBUG
                        NotifyOfPropertyChange(() => History);
#endif
                        faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404) });
                    });

                    //_activeTransport.SendPacketAsync(historyItem.Caption + " " + transportMessage.MessageId, 
                    //    encryptedMessage.ToBytes(), result => ReceiveBytesAsync(result, authKey), 
                    //    () => { if (faultCallback != null) faultCallback(null); });

                    break;

                case 32:
                case 33:
                    TLUtils.WriteLine(string.Format("ErrorCode={0} INCORRECT MSGSEQNO BY TRANSPORT TO DCID={2}, CREATE NEW SESSION {1}", badMessage.ErrorCode.Value, historyItem.Caption, transport.DCId), LogSeverity.Error);
                    Execute.ShowDebugMessage(string.Format("ErrorCode={0} INCORRECT MSGSEQNO BY TRANSPORT TO DCID={2}, CREATE NEW SESSION {1}", badMessage.ErrorCode.Value, historyItem.Caption, transport.DCId));

                    var previousMessageId = historyItem.Hash;

                    // fix seqNo with creating new Session
                    lock (transport.SyncRoot)
                    {
                        transport.SessionId = TLLong.Random();
                        transport.SequenceNumber = 0;
                        transportMessage = (TLTransportMessage)historyItem.Message;
                        if (transportMessage.SeqNo.Value % 2 == 0)
                        {
                            sequenceNumber = 2 * transport.SequenceNumber;
                        }
                        else
                        {
                            sequenceNumber = 2 * transport.SequenceNumber + 1;
                            transport.SequenceNumber++;
                        }

                        transportMessage.SeqNo = new TLInt(sequenceNumber);
                        transportMessage.MessageId = transport.GenerateMessageId(true);
                    }
                    ((TLTransportMessage)transportMessage).SessionId = transport.SessionId;


                    // TODO: replace with SendInformativeMessage
                    TLUtils.WriteLine("Corrected client time: " + TLUtils.MessageIdString(transportMessage.MessageId));
                    authKey = transport.AuthKey;
                    encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

                    lock (_historyRoot)
                    {
                        _history.Remove(previousMessageId);
                        _history[historyItem.Hash] = historyItem;
                    }

                    faultCallback = historyItem.FaultCallback;

                    lock (transport.SyncRoot)
                    {
                        if (transport.Closed)
                        {
                            var transportDCId = transport.DCId;
                            var transportKey = transport.AuthKey;
                            var transportSalt = transport.Salt;
                            var transportSessionId = transport.SessionId;
                            var transportSequenceNumber = transport.SequenceNumber;
                            var transportClientTicksDelta = transport.ClientTicksDelta;
                            bool isCreated;
                            lock (_activeTransportRoot)
                            {
                                transport = _transportService.GetTransport(transport.Host, transport.Port, Type, out isCreated);
                            }
                            if (isCreated)
                            {
                                transport.DCId = transportDCId;
                                transport.AuthKey = transportKey;
                                transport.Salt = transportSalt;
                                transport.SessionId = transportSessionId;
                                transport.SequenceNumber = transportSequenceNumber;
                                transport.ClientTicksDelta = transportClientTicksDelta;
                                transport.PacketReceived += OnPacketReceivedByTransport;
                            }
                        }
                    }
                    //Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2,-4} SessionId {3} BadMsgId {4}", string.Format("{0}: {1}", historyItem.Caption, "seqNo"), transportMessage.MessageId.Value, transportMessage.SeqNo.Value, message.SessionId.Value, badMessage.BadMessageId.Value);
                    captionString = string.Format("{0} {1} {2}", historyItem.Caption, message.SessionId, transportMessage.MessageId);
                    SendPacketAsync(transport, captionString,
                        encryptedMessage,
                        result =>
                        {
                            Debug.WriteLine("@{0} {1} result {2}", string.Format("{0}: {1}", historyItem.Caption, "seqNo"), transportMessage.MessageId.Value, result);

                        },//ReceiveBytesAsync(result, authKey)}, 
                        error => { if (faultCallback != null) faultCallback(null); });

                    break;
            }
        }

        private void ProcessBadServerSaltByTransport(ITransport transport, TLTransportMessage message, TLBadServerSalt badServerSalt, HistoryItem historyItem)
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
            TLUtils.WriteLine("New salt: " + transport.Salt);

            switch (badServerSalt.ErrorCode.Value)
            {
                case 16:
                case 17:
                    TLUtils.WriteLine("1. CORRECT TIME DELTA with salt by transport " + transport.DCId);

                    var saveConfig = false;
                    long serverTime;
                    long clientTime;
                    lock (transport.SyncRoot)
                    {
                        serverTime = message.MessageId.Value;
                        clientTime = transport.GenerateMessageId().Value;

                        TLUtils.WriteLine("Server time: " + TLUtils.MessageIdString(BitConverter.GetBytes(serverTime)));
                        TLUtils.WriteLine("Client time: " + TLUtils.MessageIdString(BitConverter.GetBytes(clientTime)));

                        if (historyItem.ClientTicksDelta == transport.ClientTicksDelta)
                        {
                            saveConfig = true;
                            transport.ClientTicksDelta += serverTime - clientTime;
                        }

                        transportMessage.MessageId = transport.GenerateMessageId(true);
                        TLUtils.WriteLine("Corrected client time: " + TLUtils.MessageIdString(transportMessage.MessageId));
                    }

                    if (saveConfig && _config != null)
                    {
                        var dcOption = _config.DCOptions.FirstOrDefault(x => string.Equals(x.IpAddress.ToString(), transport.Host, StringComparison.OrdinalIgnoreCase));
                        if (dcOption != null)
                        {
                            dcOption.ClientTicksDelta += serverTime - clientTime;
                            _cacheService.SetConfig(_config);
                        }
                    }

                    break;
                case 48:
                    break;
            }

            if (transportMessage == null) return;

            var authKey = transport.AuthKey;
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);
            lock (_historyRoot)
            {
                _history[historyItem.Hash] = historyItem;
            }
            var faultCallback = historyItem.FaultCallback;

            lock (transport.SyncRoot)
            {
                if (transport.Closed)
                {
                    var transportDCId = transport.DCId;
                    var transportKey = transport.AuthKey;
                    var transportSalt = transport.Salt;
                    var transportSessionId = transport.SessionId;
                    var transportSequenceNumber = transport.SequenceNumber;
                    var transportClientTicksDelta = transport.ClientTicksDelta;
                    bool isCreated;
                    lock (_activeTransportRoot)
                    {
                        transport = _transportService.GetTransport(transport.Host, transport.Port, Type, out isCreated);
                    }
                    if (isCreated)
                    {
                        transport.DCId = transportDCId;
                        transport.AuthKey = transportKey;
                        transport.Salt = transportSalt;
                        transport.SessionId = transportSessionId;
                        transport.SequenceNumber = transportSequenceNumber;
                        transport.ClientTicksDelta = transportClientTicksDelta;
                        transport.PacketReceived += OnPacketReceivedByTransport;
                    }
                }
            }

            var captionString = string.Format("{0} {1}", historyItem.Caption, transportMessage.MessageId);
            SendPacketAsync(transport, captionString,
                encryptedMessage,
                result =>
                {
                    Debug.WriteLine("@{0} {1} result {2}", historyItem.Caption, transportMessage.MessageId.Value, result);

                },//ReceiveBytesAsync(result, authKey)}, 
                error => { if (faultCallback != null) faultCallback(new TLRPCError()); });
        }

        private void ProcessRPCErrorByTransport(ITransport transport, TLRPCError error, HistoryItem historyItem, long keyId)
        {
            if (error.CodeEquals(ErrorCode.UNAUTHORIZED))
            {
                Execute.ShowDebugMessage(string.Format("RPCError ByTransport {2} {0} {1}", historyItem.Caption, error, transport.DCId));

                if (historyItem != null
                    && historyItem.Caption != "account.updateStatus"
                    && historyItem.Caption != "account.registerDevice"
                    && historyItem.Caption != "auth.signIn")
                {
                    if (error.TypeEquals(ErrorType.SESSION_REVOKED))
                    {

                    }
                    else
                    {
                        RaiseAuthorizationRequired(new AuthorizationRequiredEventArgs{MethodName = "ByTransport " + transport.DCId + " " + historyItem.Caption, Error = error, AuthKeyId = keyId});
                    }
                }
                else if (historyItem != null && historyItem.FaultCallback != null)
                {
                    historyItem.FaultCallback(error);
                }
            }
            else if (error.CodeEquals(ErrorCode.ERROR_SEE_OTHER)
                && (error.TypeStarsWith(ErrorType.NETWORK_MIGRATE)
                    || error.TypeStarsWith(ErrorType.PHONE_MIGRATE)
                //|| error.TypeStarsWith(ErrorType.FILE_MIGRATE)
                    ))
            {
                var serverNumber = Convert.ToInt32(
                    error.GetErrorTypeString()
                    .Replace(ErrorType.NETWORK_MIGRATE.ToString(), string.Empty)
                    .Replace(ErrorType.PHONE_MIGRATE.ToString(), string.Empty)
                    //.Replace(ErrorType.FILE_MIGRATE.ToString(), string.Empty)
                    .Replace("_", string.Empty));

                if (_config == null
                    || _config.DCOptions.FirstOrDefault(x => x.IsValidIPv4Option(new TLInt(serverNumber))) == null)
                {
                    GetConfigAsync(config =>
                    {
                        _config = TLConfig.Merge(_config, config);
                        SaveConfig();
                        if (historyItem.Object.GetType() == typeof(TLSendCode))
                        {
                            var dcOption = _config.DCOptions.First(x => x.IsValidIPv4Option(new TLInt(serverNumber)));

                            lock (transport.SyncRoot)
                            {
                                var transportDCId = dcOption.Id.Value;
                                var transportKey = dcOption.AuthKey;
                                var transportSalt = dcOption.Salt;
                                var transportSessionId = TLLong.Random();
                                var transportSequenceNumber = 0;
                                var transportClientTicksDelta = dcOption.ClientTicksDelta;
                                bool isCreated;
                                lock (_activeTransportRoot)
                                {
                                    transport = _transportService.GetTransport(dcOption.IpAddress.ToString(), dcOption.Port.Value, Type, out isCreated);
                                }
                                if (isCreated)
                                {
                                    transport.DCId = transportDCId;
                                    transport.AuthKey = transportKey;
                                    transport.Salt = transportSalt;
                                    transport.SessionId = transportSessionId;
                                    transport.SequenceNumber = transportSequenceNumber;
                                    transport.ClientTicksDelta = transportClientTicksDelta;
                                    transport.PacketReceived += OnPacketReceivedByTransport;
                                }
                            }
                            lock (transport.SyncRoot)
                            {
                                transport.Initialized = false;
                            }
                            InitTransportAsync(transport, tuple =>
                            {
                                lock (transport.SyncRoot)
                                {
                                    transport.DCId = serverNumber;
                                    transport.AuthKey = tuple.Item1;
                                    transport.Salt = tuple.Item2;
                                    transport.SessionId = tuple.Item3;
                                }
                                var authKeyId = TLUtils.GenerateLongAuthKeyId(tuple.Item1);

                                lock (_authKeysRoot)
                                {
                                    if (!_authKeys.ContainsKey(authKeyId))
                                    {
                                        _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = tuple.Item1, AutkKeyId = authKeyId });
                                    }
                                }

                                dcOption.AuthKey = tuple.Item1;
                                dcOption.Salt = tuple.Item2;
                                dcOption.SessionId = tuple.Item3;

                                _config.ActiveDCOptionIndex = _config.DCOptions.IndexOf(dcOption);
                                _cacheService.SetConfig(_config);

                                lock (transport.SyncRoot)
                                {
                                    transport.Initialized = true;
                                }
                                RaiseInitialized();

                                SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                            },
                            er =>
                            {
                                lock (transport.SyncRoot)
                                {
                                    transport.Initialized = false;
                                }
                                historyItem.FaultCallback.SafeInvoke(er);
                            });
                        }
                        else
                        {
                            MigrateAsync(serverNumber, auth => SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback));
                        }
                    });

                }
                else
                {
                    if (historyItem.Object.GetType() == typeof(TLSendCode)
                        || historyItem.Object.GetType() == typeof(TLGetFile))
                    {
                        var activeDCOption = _config.DCOptions.First(x => x.IsValidIPv4Option(new TLInt(serverNumber)));

                        lock (transport.SyncRoot)
                        {
                            var transportDCId = activeDCOption.Id.Value;
                            var transportKey = activeDCOption.AuthKey;
                            var transportSalt = activeDCOption.Salt;
                            var transportSessionId = TLLong.Random();
                            var transportSequenceNumber = 0;
                            var transportClientTicksDelta = activeDCOption.ClientTicksDelta;
                            bool isCreated;
                            lock (_activeTransportRoot)
                            {
                                _activeTransport = _transportService.GetTransport(activeDCOption.IpAddress.ToString(), activeDCOption.Port.Value, Type, out isCreated);
                            }
                            if (isCreated)
                            {
                                transport.DCId = transportDCId;
                                transport.AuthKey = transportKey;
                                transport.Salt = transportSalt;
                                transport.SessionId = transportSessionId;
                                transport.SequenceNumber = transportSequenceNumber;
                                transport.ClientTicksDelta = transportClientTicksDelta;
                                transport.PacketReceived += OnPacketReceivedByTransport;
                            }
                        }

                        if (activeDCOption.AuthKey == null)
                        {
                            lock (transport.SyncRoot)
                            {
                                transport.Initialized = false;
                            }
                            InitTransportAsync(transport, tuple =>
                            {
                                lock (transport.SyncRoot)
                                {
                                    transport.DCId = serverNumber;
                                    transport.AuthKey = tuple.Item1;
                                    transport.Salt = tuple.Item2;
                                    transport.SessionId = tuple.Item3;
                                }

                                var authKeyId = TLUtils.GenerateLongAuthKeyId(tuple.Item1);

                                lock (_authKeysRoot)
                                {
                                    if (!_authKeys.ContainsKey(authKeyId))
                                    {
                                        _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = tuple.Item1, AutkKeyId = authKeyId });
                                    }
                                }

                                activeDCOption.AuthKey = tuple.Item1;
                                activeDCOption.Salt = tuple.Item2;
                                activeDCOption.SessionId = tuple.Item3;

                                _config.ActiveDCOptionIndex = _config.DCOptions.IndexOf(activeDCOption);
                                _cacheService.SetConfig(_config);

                                lock (transport.SyncRoot)
                                {
                                    transport.Initialized = true;
                                }

                                RaiseInitialized();
                                SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                            },
                            er =>
                            {
                                lock (transport.SyncRoot)
                                {
                                    transport.Initialized = false;
                                }
                                historyItem.FaultCallback.SafeInvoke(er);
                            });
                        }
                        else
                        {
                            lock (transport.SyncRoot)
                            {
                                transport.AuthKey = activeDCOption.AuthKey;
                                transport.Salt = activeDCOption.Salt;
                                transport.SessionId = TLLong.Random();
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

                            lock (transport.SyncRoot)
                            {
                                transport.Initialized = true;
                            }
                            RaiseInitialized();

                            SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                        }
                    }
                    else
                    {
                        MigrateAsync(serverNumber, auth => SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback));
                    }
                }
            }
            else if (historyItem.FaultCallback != null)
            {
                historyItem.FaultCallback(error);
            }
        }

        private void OnPacketReceivedByTransport(object sender, DataEventArgs e)
        {
            var transport = (ITransport)sender;
            bool isInitialized;
            lock (transport.SyncRoot)
            {
                isInitialized = transport.AuthKey != null;
            }

            var position = 0;
            var handled = false;

            if (!isInitialized)
            {
                try
                {
                    var message = TLObject.GetObject<TLNonEncryptedMessage>(e.Data, ref position);

                    var item = transport.DequeueFirstNonEncryptedItem();
                    if (item != null)
                    {
#if LOG_REGISTRATION
                            TLUtils.WriteLog("OnReceivedBytes !IsInitialized try historyItem " + item.Caption);
#endif
                        item.Callback.SafeInvoke(message.Data);
                    }
                    else
                    {
#if LOG_REGISTRATION
                        TLUtils.WriteLog("OnReceivedBytes !IsInitialized cannot try historyItem ");
#endif
                    }

                    handled = true;
                }
                catch (Exception ex)
                {
#if LOG_REGISTRATION

                    var sb = new StringBuilder();
                    sb.AppendLine("OnPacketReceived !IsInitialized catch Exception: \n" + ex);
                    sb.AppendLine(transport.PrintNonEncryptedHistory());                   
                    TLUtils.WriteLog(sb.ToString());
#endif
                }

                if (!handled)
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog("OnPacketReceived !IsInitialized !handled invoke ReceiveBytesAsync");
#endif
                    ReceiveBytesByTransportAsync(transport, e.Data);
                }
            }
            else
            {
#if LOG_REGISTRATION
                TLUtils.WriteLog("OnPacketReceived IsInitialized invoke ReceiveBytesAsync");
#endif
                ReceiveBytesByTransportAsync(transport, e.Data);
            }
        }

        private void ReceiveBytesByTransportAsync(ITransport transport, byte[] bytes)
        {
            try
            {
                var position = 0;
                var encryptedMessage = (TLEncryptedTransportMessage)new TLEncryptedTransportMessage().FromBytes(bytes, ref position);

                encryptedMessage.Decrypt(transport.AuthKey);

                //check msg_key
                //var padding = encryptedMessage.Data.Length

                //msg_id


                position = 0;
                TLTransportMessage transportMessage;
                transportMessage = TLObject.GetObject<TLTransportMessage>(encryptedMessage.Data, ref position);
                //if (transportMessage.SessionId.Value != transport.SessionId.Value)
                //{
                //    throw new Exception("Incorrect session_id");
                //}
                if ((transportMessage.MessageId.Value % 2) == 0)
                {
                    throw new Exception("Incorrect message_id");
                }

                // get acknowledgments
                foreach (var acknowledgment in TLUtils.FindInnerObjects<TLMessagesAcknowledgment>(transportMessage))
                {
                    var ids = acknowledgment.MessageIds.Items;
                    lock (_historyRoot)
                    {
                        foreach (var id in ids)
                        {
                            if (_history.ContainsKey(id.Value))
                            {
                                _history[id.Value].Status = RequestStatus.Confirmed;
                            }
                        }
                    }

                }

                // send acknowledgments
                SendAcknowledgmentsByTransport(transport, transportMessage);

                // updates
                _updatesService.ProcessTransportMessage(transportMessage);

                // bad messages
                foreach (var badMessage in TLUtils.FindInnerObjects<TLBadMessageNotification>(transportMessage))
                {

                    HistoryItem item = null;
                    lock (_historyRoot)
                    {
                        if (_history.ContainsKey(badMessage.BadMessageId.Value))
                        {
                            item = _history[badMessage.BadMessageId.Value];
                        }
                    }
                    Logs.Log.Write(string.Format("{0} {1} transport={2}", badMessage, item, transport.DCId));

                    ProcessBadMessageByTransport(transport, transportMessage, badMessage, item);
                }

                // bad server salts
                foreach (var badServerSalt in TLUtils.FindInnerObjects<TLBadServerSalt>(transportMessage))
                {

                    lock (transport.SyncRoot)
                    {
                        transport.Salt = badServerSalt.NewServerSalt;
                    }
                    HistoryItem item = null;
                    lock (_historyRoot)
                    {
                        if (_history.ContainsKey(badServerSalt.BadMessageId.Value))
                        {
                            item = _history[badServerSalt.BadMessageId.Value];
                        }
                    }
                    Logs.Log.Write(string.Format("{0} {1} transport={2}", badServerSalt, item, transport.DCId));

                    ProcessBadServerSaltByTransport(transport, transportMessage, badServerSalt, item);
                }

                // new session created
                foreach (var newSessionCreated in TLUtils.FindInnerObjects<TLNewSessionCreated>(transportMessage))
                {
                    TLUtils.WritePerformance(string.Format("NEW SESSION CREATED: {0} (old {1})", transportMessage.SessionId, _activeTransport.SessionId));
                    lock (transport.SyncRoot)
                    {
                        transport.SessionId = transportMessage.SessionId;
                        transport.Salt = newSessionCreated.ServerSalt;
                    }
                }

                // rpcresults
                foreach (var result in TLUtils.FindInnerObjects<TLRPCResult>(transportMessage))
                {
                    HistoryItem historyItem = null;

                    lock (_historyRoot)
                    {
                        if (_history.ContainsKey(result.RequestMessageId.Value))
                        {
                            historyItem = _history[result.RequestMessageId.Value];
                            _history.Remove(result.RequestMessageId.Value);
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

                    var error = result.Object as TLRPCError;
                    if (error != null)
                    {
                        Debug.WriteLine("RPCError: " + error.Code + " " + error.Message + " MsgId " + result.RequestMessageId.Value);
                        TLUtils.WriteLine("RPCError: " + error.Code + " " + error.Message);

                        string errorString;
                        var reqError = error as TLRPCReqError;
                        if (reqError != null)
                        {
                            errorString = string.Format("RPCReqError {1} {2} (query_id={0}) transport=[dc_id={3}]", reqError.QueryId, reqError.Code, reqError.Message, transport.DCId);
                        }
                        else
                        {
                            errorString = string.Format("RPCError {0} {1} transport=[dc_id={2}]", error.Code, error.Message, transport.DCId);
                        }

                        Execute.ShowDebugMessage(historyItem + Environment.NewLine + errorString);
                        ProcessRPCErrorByTransport(transport, error, historyItem, encryptedMessage.AuthKeyId.Value);
                        Debug.WriteLine(errorString + " msg_id=" + result.RequestMessageId.Value);
                        TLUtils.WriteLine(errorString);
                    }
                    else
                    {
                        var messageData = result.Object;
                        if (messageData is TLGzipPacked)
                        {
                            messageData = ((TLGzipPacked)messageData).Data;
                        }

                        if (messageData is TLSentMessageBase
                            || messageData is TLStatedMessageBase
                            || messageData is TLUpdatesBase
                            || messageData is TLSentEncryptedMessage
                            || messageData is TLSentEncryptedFile
                            || messageData is TLAffectedHistory
                            || messageData is TLAffectedMessages
                            || historyItem.Object is TLReadEncryptedHistory)
                        {
                            RemoveFromQueue(historyItem);
                        }

                        try
                        {
                            historyItem.Callback(messageData);
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
            catch (Exception e)
            {
#if LOG_REGISTRATION
                TLUtils.WriteLog("ReceiveBytesAsyncException:\n" + e);
#endif
                TLUtils.WriteException(e);
                ClearHistoryByTransport(transport);

                lock (transport.SyncRoot)
                {
                    // continue listening on fault
                    var transportDCId = transport.DCId;
                    var transportKey = transport.AuthKey;
                    var transportSalt = transport.Salt;
                    var transportSessionId = transport.SessionId;
                    var transportSequenceNumber = transport.SequenceNumber;
                    var transportClientTicksDelta = transport.ClientTicksDelta;
                    bool isCreated;
                    lock (_activeTransportRoot)
                    {
                        transport = _transportService.GetTransport(transport.Host, transport.Port, Type, out isCreated);
                    }
                    if (isCreated)
                    {
                        transport.DCId = transportDCId;
                        transport.AuthKey = transportKey;
                        transport.Salt = transportSalt;
                        transport.SessionId = transportSessionId;
                        transport.SequenceNumber = transportSequenceNumber;
                        transport.ClientTicksDelta = transportClientTicksDelta;
                        transport.PacketReceived += OnPacketReceivedByTransport;
                    }
                }
                // to bind authKey to current TCPTransport
                UpdateStatusAsync(new TLBool(false), result => { });
            }
        }

        public void ClearHistoryByTransport(ITransport transport)
        {
            _transportService.CloseTransport(transport);

            lock (_historyRoot)
            {
                var keysToRemove = new List<long>();
                foreach (var keyValue in _history)
                {
                    if (keyValue.Value.Caption.StartsWith("msgs_ack"))
                    {
                        TLUtils.WriteLine("!!!!!!MSGS_ACK FAULT!!!!!!!", LogSeverity.Error);
                        Debug.WriteLine("!!!!!!MSGS_ACK FAULT!!!!!!!");
                    }
                    if (transport.DCId == keyValue.Value.DCId)
                    {
                        keyValue.Value.FaultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404), Message = new TLString("Clear History") });
                        keysToRemove.Add(keyValue.Key);
                    }
                }
                foreach (var key in keysToRemove)
                {
                    _history.Remove(key);
                }
            }

            transport.ClearNonEncryptedHistory();
        }
    }
}
