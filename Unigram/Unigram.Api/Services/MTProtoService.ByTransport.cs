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
using Telegram.Api.Transport;
using System.Threading.Tasks;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Methods.Auth;
using Telegram.Api.TL.Methods.Upload;
using Telegram.Api.TL.Methods.Messages;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        private Task<MTProtoResponse<TLResPQ>> ReqPQByTransportAsync(ITransport transport, TLInt128 nonce)
        {
            var obj = new TLReqPQ { Nonce = nonce };

            return SendNonEncryptedMessageByTransport<TLResPQ>(transport, "req_pq", obj);
        }

        private Task<MTProtoResponse<TLServerDHParamsBase>> ReqDHParamsByTransportAsync(ITransport transport, TLInt128 nonce, TLInt128 serverNonce, byte[] p, byte[] q, long publicKeyFingerprint, byte[] encryptedData)
        {
            var obj = new TLReqDHParams { Nonce = nonce, ServerNonce = serverNonce, P = p, Q = q, PublicKeyFingerprint = publicKeyFingerprint, EncryptedData = encryptedData };

            return SendNonEncryptedMessageByTransport<TLServerDHParamsBase>(transport, "req_DH_params", obj);
        }

        public Task<MTProtoResponse<TLSetClientDHParamsAnswerBase>> SetClientDHParamsByTransportAsync(ITransport transport, TLInt128 nonce, TLInt128 serverNonce, byte[] encryptedData)
        {
            var obj = new TLSetClientDHParams { Nonce = nonce, ServerNonce = serverNonce, EncryptedData = encryptedData };

            return SendNonEncryptedMessageByTransport<TLSetClientDHParamsAnswerBase>(transport, "set_client_DH_params", obj);
        }

        public async Task<MTProtoResponse<Tuple<byte[], long, long>>> InitializeTransportAsync(ITransport transport)
        {
            MTProtoResponse<Tuple<byte[], long, long>> result = null;
            var authTime = Stopwatch.StartNew();
            var newNonce = TLInt256.Random();

#if LOG_REGISTRATION
            TLUtils.WriteLog("Start ReqPQ");
#endif
            var nonce = TLInt128.Random();
            var request = await ReqPQByTransportAsync(transport, nonce);
            var resPQ = request.Value;
            if (request.Error == null)
            {
                var serverNonce = resPQ.ServerNonce;
                if (nonce != resPQ.Nonce)
                {
                    var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect nonce" };
#if LOG_REGISTRATION
                    TLUtils.WriteLog("Stop ReqPQ with error " + error);
#endif

                    result = new MTProtoResponse<Tuple<byte[], long, long>>(null, error);

                    TLUtils.WriteLine(error.ToString());
                }

#if LOG_REGISTRATION
                TLUtils.WriteLog("Stop ReqPQ");
#endif
                TimeSpan calcTime;
                Tuple<ulong, ulong> pqPair;
                var innerData = GetInnerData(resPQ, newNonce, out calcTime, out pqPair);
                var encryptedInnerData = GetEncryptedInnerData(innerData);

#if LOG_REGISTRATION
                TLUtils.WriteLog("Start ReqDHParams");
#endif
                var reqDHPResult = await ReqDHParamsByTransportAsync(
                     transport,
                     resPQ.Nonce,
                     resPQ.ServerNonce,
                     innerData.P,
                     innerData.Q,
                     resPQ.ServerPublicKeyFingerprints[0],
                     encryptedInnerData);
                if (reqDHPResult.Error == null)
                {
                    if (nonce != reqDHPResult.Value.Nonce)
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect nonce" };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                        result = new MTProtoResponse<Tuple<byte[], long, long>>(null, error);
                        TLUtils.WriteLine(error.ToString());
                    }
                    if (serverNonce != reqDHPResult.Value.ServerNonce)
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect server_nonce" };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                        result = new MTProtoResponse<Tuple<byte[], long, long>>(null, error);
                        TLUtils.WriteLine(error.ToString());
                    }

#if LOG_REGISTRATION
                    TLUtils.WriteLog("Stop ReqDHParams");
#endif
                    var random = new SecureRandom();

                    var serverDHParamsOk = reqDHPResult.Value as TLServerDHParamsOk;
                    if (serverDHParamsOk == null)
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "Incorrect serverDHParams" };
                        result = new MTProtoResponse<Tuple<byte[], long, long>>(null, error);
                        TLUtils.WriteLine(error.ToString());
#if LOG_REGISTRATION
                            
                        TLUtils.WriteLog("ServerDHParams " + serverDHParams);  
#endif                        
                    }

                    var aesParams = GetAesKeyIV(resPQ.ServerNonce.ToArray(), newNonce.ToArray());

                    var decryptedAnswerWithHash = Utils.AesIge(serverDHParamsOk.EncryptedAnswer, aesParams.Item1, aesParams.Item2, false);     //NOTE: Remove reverse here

                    //var position = 0;
                    //var serverDHInnerData = (TLServerDHInnerData)new TLServerDHInnerData().FromBytes(decryptedAnswerWithHash.Skip(20).ToArray(), ref position);
                    var serverDHInnerData = TLFactory.From<TLServerDHInnerData>(decryptedAnswerWithHash.Skip(20).ToArray());

                    var sha1 = Utils.ComputeSHA1(serverDHInnerData.ToArray());
                    if (!TLUtils.ByteArraysEqual(sha1, decryptedAnswerWithHash.Take(20).ToArray()))
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect sha1 TLServerDHInnerData" };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                        result = new MTProtoResponse<Tuple<byte[], long, long>>(null, error);
                        TLUtils.WriteLine(error.ToString());
                    }

                    if (!TLUtils.CheckPrime(serverDHInnerData.DHPrime, serverDHInnerData.G))
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect (p, q) pair" };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                        result = new MTProtoResponse<Tuple<byte[], long, long>>(null, error);
                        TLUtils.WriteLine(error.ToString());
                    }

                    if (!TLUtils.CheckGaAndGb(serverDHInnerData.GA, serverDHInnerData.DHPrime))
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect g_a" };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                        result = new MTProtoResponse<Tuple<byte[], long, long>>(null, error);
                        TLUtils.WriteLine(error.ToString());
                    }

                    var bBytes = new byte[256]; //big endian B
                    random.NextBytes(bBytes);

                    var gbBytes = GetGB(bBytes, serverDHInnerData.G, serverDHInnerData.DHPrime);

                    var clientDHInnerData = new TLClientDHInnerData
                    {
                        Nonce = resPQ.Nonce,
                        ServerNonce = resPQ.ServerNonce,
                        RetryId = 0,
                        GB = gbBytes
                    };

                    var encryptedClientDHInnerData = GetEncryptedClientDHInnerData(clientDHInnerData, aesParams);
#if LOG_REGISTRATION
                    TLUtils.WriteLog("Start SetClientDHParams");  
#endif
                    var dhGen = await SetClientDHParamsByTransportAsync(transport, resPQ.Nonce, resPQ.ServerNonce, encryptedClientDHInnerData);
                    if (dhGen.Error == null)
                    {

                        if (nonce != dhGen.Value.Nonce)
                        {
                            var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect nonce" };
#if LOG_REGISTRATION
                            TLUtils.WriteLog("Stop SetClientDHParams with error " + error);
#endif

                            result = new MTProtoResponse<Tuple<byte[], long, long>>(null, error);
                            TLUtils.WriteLine(error.ToString());
                        }
                        if (serverNonce != dhGen.Value.ServerNonce)
                        {
                            var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect server_nonce" };
#if LOG_REGISTRATION
                            TLUtils.WriteLog("Stop SetClientDHParams with error " + error);
#endif

                            result = new MTProtoResponse<Tuple<byte[], long, long>>(null, error);
                            TLUtils.WriteLine(error.ToString());
                        }

                        var dhGenOk = dhGen.Value as TLDHGenOk;
                        if (dhGenOk == null)
                        {
                            var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "Incorrect dhGen " + dhGen.GetType() };
                            result = new MTProtoResponse<Tuple<byte[], long, long>>(null, error);
                            TLUtils.WriteLine(error.ToString());
#if LOG_REGISTRATION
                            TLUtils.WriteLog("DHGen result " + serverDHParams);
#endif

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
                        var salt = GetSalt(newNonce.ToArray(), resPQ.ServerNonce.ToArray());
                        var sessionId = new byte[8];
                        random.NextBytes(sessionId);

                        // authKey, salt, sessionId
                        result = new MTProtoResponse<Tuple<byte[], long, long>>(
                            new Tuple<byte[], long, long>(
                                authKey,
                                BitConverter.ToInt64(salt, 0),
                                BitConverter.ToInt64(sessionId, 0)));
                    }
                    else
                    {

#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop SetClientDHParams with error " + error.ToString());
#endif
                        result = new MTProtoResponse<Tuple<byte[], long, long>>(null, dhGen.Error);

                        TLUtils.WriteLine(dhGen.Error.ToString());

                    }
                }
                else
                {


#if LOG_REGISTRATION
                    TLUtils.WriteLog("Stop ReqDHParams with error " + error.ToString());
#endif

                    result = new MTProtoResponse<Tuple<byte[], long, long>>(null, reqDHPResult.Error);
                    TLUtils.WriteLine(reqDHPResult.Error.ToString());
                }
            }

            else
            {

#if LOG_REGISTRATION
                TLUtils.WriteLog("Stop ReqPQ with error " + error.ToString());
#endif
                result = new MTProtoResponse<Tuple<byte[], long, long>>(null, result.Error);
                TLUtils.WriteLine(result.Error.ToString());

            }

            return result;
        }

        public void LogOutTransportsAsync()
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
                            var request = LogOutAsync().Result;
                            if (request.Error == null)
                            {
                                handle.Set();
                            }
                            else
                            {
                                errors.Add(request.Error);
                                handle.Set();
                            }
                        });
                    }

                    var waitingResult = WaitHandle.WaitAll(waitHandles.ToArray(), TimeSpan.FromSeconds(25.0));
                    if (waitingResult)
                    {
                        if (errors.Count > 0)
                        {
                            //faultCallback.SafeInvoke(errors);
                        }
                        else
                        {
                            // callback.SafeInvoke();
                        }
                    }
                    else
                    {
                        // faultCallback.SafeInvoke(errors);
                    }
                }
                else
                {
                    // callback.SafeInvoke();
                }
            });
        }

        public async Task<MTProtoResponse<bool>> LogOutAsync(int dcId)
        {
            lock (_activeTransportRoot)
            {
                if (_activeTransport.DCId == dcId)
                {
                    if (_activeTransport.DCId == 0)
                    {
                        TLUtils.WriteException(new Exception("_activeTransport.DCId==0"));
                    }

                    LogOutAsync().Wait();
                    return new MTProtoResponse<bool>(false);
                }
            }

            var transport = GetTransportByDCId(dcId);

            if (transport == null)
            {
                return new MTProtoResponse<bool>(true,
                    new TLRPCError
                    {
                        ErrorCode = 404,
                        ErrorMessage = "LogOutAsync: Empty transport for dc id " + dcId
                    });
            }

            if (transport.AuthKey == null)
            {
                return new MTProtoResponse<bool>(false,
                    new TLRPCError
                    {
                        ErrorCode = 404,
                        ErrorMessage = "LogOutAsync: Empty authKey for dc id " + dcId
                    });
            }

            var obj = new TLAuthLogOut();

            var result = await SendInformativeMessageByTransport<bool>(transport, "auth.logOut", obj);
            if (result.IsSucceeded)
            {
                lock (transport.SyncRoot)
                {
                    transport.IsInitializing = false;
                    transport.IsAuthorizing = false;
                    transport.IsAuthorized = false;
                }

                return new MTProtoResponse<bool>(true);
            }
            else
            {
                return new MTProtoResponse<bool>(false);
            }
        }

        public async Task<MTProtoResponse<TLUploadFile>> GetFileAsync(int dcId, TLInputFileLocationBase location, int offset, int limit)
        {
            MTProtoResponse<TLUploadFile> result = null;
            var obj = new TLUploadGetFile { Location = location, Offset = offset, Limit = limit };
            var download = false;

            lock (_activeTransportRoot)
            {
                if (_activeTransport.DCId == dcId)
                {
                    if (_activeTransport.DCId == 0)
                    {
                        TLUtils.WriteException(new Exception("_activeTransport.DCId==0"));
                    }

                    download = true;

                    //return SendInformativeMessage<TLUploadFile>("upload.getFile", obj).Result;
                }
            }

            if (download)
            {
                return await SendInformativeMessage<TLUploadFile>("upload.getFile", obj);
            }

            var transport = GetTransportByDCId(dcId);
            if (transport == null)
            {
                return new MTProtoResponse<TLUploadFile>(new TLRPCError { ErrorCode = 404, ErrorMessage = "GetFileAsync: Empty transport for dc id " + dcId });
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
                    return new MTProtoResponse<TLUploadFile>(new TLRPCError { ErrorCode = 404, ErrorMessage = "DC " + dcId + " is already initializing" });
                }

                var tuple = await InitializeTransportAsync(transport);
                if (tuple.Error == null)
                {
                    lock (transport.SyncRoot)
                    {
                        transport.AuthKey = tuple.Value.Item1;
                        transport.Salt = tuple.Value.Item2;
                        transport.SessionId = tuple.Value.Item3;

                        transport.IsInitializing = false;
                    }
                    var authKeyId = TLUtils.GenerateLongAuthKeyId(tuple.Value.Item1);

                    lock (_authKeysRoot)
                    {
                        if (!_authKeys.ContainsKey(authKeyId))
                        {
                            _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = tuple.Value.Item1, AutkKeyId = authKeyId });
                        }
                    }

                    var transportExport = await ExportImportAuthorizationAsync(transport);

                    if (transportExport.Error == null)
                    {
                        foreach (var dcOption in _config.DCOptions)
                        {
                            if (dcOption.Id == transport.DCId)
                            {
                                dcOption.AuthKey = tuple.Value.Item1;
                                dcOption.Salt = tuple.Value.Item2;
                                dcOption.SessionId = tuple.Value.Item3;
                            }
                        }

                        _cacheService.SetConfig(_config);

                        var innerRequest = await SendInformativeMessageByTransport<TLUploadFile>(transport, "upload.getFile", obj);
                        result = new MTProtoResponse<TLUploadFile>(innerRequest.Value);
                    }
                    else
                    {
                        if (!transportExport.Error.CodeEquals(TLErrorCode.NOT_FOUND) &&
                            !transportExport.Error.ErrorMessage.ToString().Contains("is already authorizing"))
                        {
                            Execute.ShowDebugMessage("ExportImportAuthorization error " + transportExport.Error);
                        }

                        result = new MTProtoResponse<TLUploadFile>(transportExport.Error);
                    }

                }
                else
                {
                    lock (transport.SyncRoot)
                    {
                        transport.IsInitializing = false;
                    }

                    result = new MTProtoResponse<TLUploadFile>(tuple.Error);
                }
            }
            else
            {
                var innerRequest = await ExportImportAuthorizationAsync(transport);
                if (innerRequest.Error == null)
                {
                    var file = await SendInformativeMessageByTransport<TLUploadFile>(transport, "upload.getFile", obj);
                    result = new MTProtoResponse<TLUploadFile>(file.Value);
                }
                else
                {
                    if (!innerRequest.Error.CodeEquals(TLErrorCode.NOT_FOUND) &&
                        !innerRequest.Error.ErrorMessage.ToString().Contains("is already authorizing"))
                    {
                        Execute.ShowDebugMessage("ExportImportAuthorization error " + innerRequest.Error);
                    }

                    result = new MTProtoResponse<TLUploadFile>(innerRequest.Error);
                }
            }

            return result;
        }

        private async Task<MTProtoResponse<TLObject>> ExportImportAuthorizationAsync(ITransport toTransport)
        {
            MTProtoResponse<TLObject> result = null;
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
                    return new MTProtoResponse<TLObject>(new TLRPCError { ErrorCode = 404, ErrorMessage = $"DC {toTransport.DCId} is already authorizing" });
                }

                var exportedAuthorization = await ExportAuthorizationAsync(toTransport.DCId);
                if (exportedAuthorization.Error == null)
                {
                    var authorization = await ImportAuthorizationByTransportAsync(
                        toTransport,
                        exportedAuthorization.Value.Id,
                        exportedAuthorization.Value.Bytes);
                    if (authorization.Error == null)
                    {

                        lock (toTransport.SyncRoot)
                        {
                            toTransport.IsAuthorized = true;
                            toTransport.IsAuthorizing = false;
                        }

                        result = new MTProtoResponse<TLObject>(null);
                    }
                    else
                    {

                        lock (toTransport.SyncRoot)
                        {
                            toTransport.IsAuthorizing = false;
                        }

                        result = new MTProtoResponse<TLObject>(null, authorization.Error);
                    }
                }
                else
                {
                    lock (toTransport.SyncRoot)
                    {
                        toTransport.IsAuthorizing = false;
                    }

                    result = new MTProtoResponse<TLObject>(null, exportedAuthorization.Error);
                }
            }

            return result = new MTProtoResponse<TLObject>(null);
        }

        private ITransport GetTransportByDCId(int dcId)
        {
            ITransport transport;
            lock (_activeTransportRoot)
            {
                var dcOption = _config.DCOptions.FirstOrDefault(x => x.IsValidIPv4Option(dcId));

                if (dcOption == null) return null;

                bool isCreated;
                transport = _transportService.GetFileTransport(dcOption.IpAddress, dcOption.Port, Type, out isCreated);
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
            }

            return transport;
        }

        private readonly object _initConnectionSyncRoot = new object();

        private async Task<MTProtoResponse<T>> SendInformativeMessageByTransport<T>(ITransport transport, string caption, TLObject obj,
            int? maxAttempt = null,                 // to send delayed items
            Action<int> attemptFailed = null)       // to send delayed items
        {
            var callback = new TaskCompletionSource<MTProtoResponse>();

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
                    Callback = callback,
                    Object = obj,
                    AttemptFailed = attemptFailed,
                    MaxAttempt = maxAttempt
                };
#if LOG_REGISTRATION
                    TLUtils.WriteLog(DateTime.Now.ToLocalTime() + ": Enqueue delayed item\n " + delayedItem); 
#endif
                lock (_delayedItemsRoot)
                {
                    _delayedItems.Add(delayedItem);
                }

                return await callback.Task;
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
                    transport = _transportService.GetTransport(transport.Host, transport.Port, Type, out isCreated);
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
            long messageId;
            lock (transport.SyncRoot)
            {
                if (!transport.Initiated || caption == "auth.sendCode")
                {
                    var initConnection = new TLInitConnection
                    {
                        ApiId = Constants.ApiId,
                        AppVersion = _deviceInfo.AppVersion,
                        Query = obj,
                        DeviceModel = _deviceInfo.Model,
                        LangCode = Utils.CurrentUICulture(),
                        SystemVersion = _deviceInfo.SystemVersion
                    };

                    data = new TLInvokeWithLayer { Query = initConnection, Layer = Constants.SupportedLayer };
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
            var salt = transport.Salt ?? 0;
            var sessionId = transport.SessionId ?? 0;
            var clientsTicksDelta = transport.ClientTicksDelta;
            var dcId = transport.DCId;
            var transportMessage = CreateTLTransportMessage(salt, sessionId, sequenceNumber, messageId, data);
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

            var historyItem = new HistoryItem
            {
                SendTime = DateTime.Now,
                Caption = caption,
                Callback = callback,
                Object = obj,
                Message = transportMessage,
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

            Debug.WriteLine(">> {4} {0, -30} MsgId {1} SeqNo {2, -4} SessionId {3}", caption, transportMessage.MsgId, transportMessage.SeqNo, transportMessage.SessionId, historyItem.DCId);
            var captionString = string.Format("{0} {1} {2}", caption, transportMessage.SessionId, transportMessage.MsgId);
            SendPacketAsync(transport, captionString, encryptedMessage, (result) =>
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

                    callback.TrySetResult(new MTProtoResponse<T>(new TLRPCError { ErrorCode = 404, ErrorMessage = $"FastCallback SocketError={result}" }));
                }
            }, 
            (error) =>
            {
                lock (_historyRoot)
                {
                    _history.Remove(historyItem.Hash);
                }
                callback.TrySetResult(new MTProtoResponse<T>(new TLRPCError { ErrorCode = 404 }));
            });

            return await callback.Task;
        }

        private void SaveInitConnectionAsync(TLInitConnection initConnection)
        {
            Execute.BeginOnThreadPool(() => TLUtils.SaveObjectToMTProtoFile(_initConnectionSyncRoot, Constants.InitConnectionFileName, initConnection));
        }

        private async Task<MTProtoResponse<T>> SendNonEncryptedMessageByTransport<T>(ITransport transport, string caption, TLObject obj) where T : TLObject
        {
            PrintCaption(caption);

            long messageId;
            lock (transport.SyncRoot)
            {
                messageId = transport.GenerateMessageId();
            }
            var message = CreateTLNonEncryptedMessage(messageId, obj);

            var callback = new TaskCompletionSource<MTProtoResponse>();

            var historyItem = new HistoryItem
            {
                Caption = caption,
                Message = message,
                Callback = callback,
                SendTime = DateTime.Now,
                Status = RequestStatus.Sent
            };

            var guid = message.MsgId;
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
            SendPacketAsync(transport, captionString, message, (socketError) =>
            {
                if (!socketError)
                {
                    transport.RemoveNonEncryptedItem(historyItem);
                    callback.TrySetResult(new MTProtoResponse<T>(new TLRPCError { ErrorCode = 404, ErrorMessage = $"FastCallback SocketError={socketError}" }));
                }
            }, 
            (error) =>
            {
                transport.RemoveNonEncryptedItem(historyItem);
                callback.TrySetResult(new MTProtoResponse<T>(new TLRPCError { ErrorCode = 404 }));
            });

            var taskResult = await callback.Task;
            return taskResult;
        }

        public void MessageAcknowledgmentsByTransport(ITransport transport, TLVector<long> ids)
        {
            PrintCaption("msgs_ack");
            TLUtils.WriteLine("ids");
            foreach (var id in ids)
            {
                TLUtils.WriteLine(TLUtils.MessageIdString(id));
            }
            var obj = new TLMsgsAck { MsgIds = ids };

            var authKey = transport.AuthKey;
            var sesseionId = transport.SessionId ?? 0;
            var salt = transport.Salt ?? 0;

            int sequenceNumber;
            long messageId;
            lock (transport.SyncRoot)
            {
                sequenceNumber = transport.SequenceNumber * 2;
                messageId = transport.GenerateMessageId(true);
            }
            var transportMessage = CreateTLTransportMessage(salt, sesseionId, sequenceNumber, messageId, obj);
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
                Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2, -4} SessionId {3}\nids:", "msgs_ack", transportMessage.MsgId, transportMessage.SeqNo, transportMessage.SessionId);
                foreach (var id in ids)
                {
                    Debug.WriteLine(id);
                }
            }

            var captionString = string.Format("msgs_ack {0}", transportMessage.MsgId);
            SendPacketAsync(transport, captionString, encryptedMessage, (result) =>
            {
                Debug.WriteLine("@msgs_ack {0} result {1}", transportMessage.MsgId, result);
            }, (error) => { });
        }

        private void ProcessBadMessageByTransport(ITransport transport, TLTransportMessage message, TLBadMsgNotification badMessage, HistoryItem historyItem)
        {
            if (historyItem == null) return;

            switch (badMessage.ErrorCode)
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
                        var serverTime = message.MsgId;
                        var clientTime = transport.GenerateMessageId();

                        var serverDateTime = Utils.UnixTimestampToDateTime(serverTime >> 32);
                        var clientDateTime = Utils.UnixTimestampToDateTime(clientTime >> 32);

                        errorInfo.AppendLine("Server time: " + serverDateTime);
                        errorInfo.AppendLine("Client time: " + clientDateTime);

                        if (historyItem.ClientTicksDelta == transport.ClientTicksDelta)
                        {
                            transport.ClientTicksDelta += serverTime - clientTime;
                            saveConfig = true;
                            errorInfo.AppendLine("Set ticks delta: " + transport.ClientTicksDelta + "(" + (serverDateTime - clientDateTime).TotalSeconds + " seconds)");
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
                        if (transportMessage.SeqNo % 2 == 0)
                        {
                            sequenceNumber = 2 * transport.SequenceNumber;
                        }
                        else
                        {
                            sequenceNumber = 2 * transport.SequenceNumber + 1;
                            transport.SequenceNumber++;
                        }

                        transportMessage.SeqNo = sequenceNumber;
                        transportMessage.MsgId = transport.GenerateMessageId(true);
                    }
                    TLUtils.WriteLine("Corrected client time: " + TLUtils.MessageIdString(transportMessage.MsgId));
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
                    Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2,-4} SessionId {3} BadMsgId {4}", string.Format("{0}: {1}", historyItem.Caption, "time"), transportMessage.MsgId, transportMessage.SeqNo, message.SessionId, badMessage.BadMsgId);
                    var captionString = string.Format("{0} {1} {2}", historyItem.Caption, message.SessionId, transportMessage.MsgId);

                    SendPacketAsync(transport, captionString, encryptedMessage, (resultApi) =>
                    {
                        Debug.WriteLine("@{0} {1} result {2}", string.Format("{0}: {1}", historyItem.Caption, "time"), transportMessage.MsgId, resultApi);
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
                    TLUtils.WriteLine(string.Format("ErrorCode={0} INCORRECT MSGSEQNO BY TRANSPORT TO DCID={2}, CREATE NEW SESSION {1}", badMessage.ErrorCode, historyItem.Caption, transport.DCId), LogSeverity.Error);
                    Execute.ShowDebugMessage(string.Format("ErrorCode={0} INCORRECT MSGSEQNO BY TRANSPORT TO DCID={2}, CREATE NEW SESSION {1}", badMessage.ErrorCode, historyItem.Caption, transport.DCId));

                    var previousMessageId = historyItem.Hash;

                    // fix seqNo with creating new Session
                    lock (transport.SyncRoot)
                    {
                        transport.SessionId = TLLong.Random();
                        transport.SequenceNumber = 0;
                        transportMessage = (TLTransportMessage)historyItem.Message;
                        if (transportMessage.SeqNo % 2 == 0)
                        {
                            sequenceNumber = 2 * transport.SequenceNumber;
                        }
                        else
                        {
                            sequenceNumber = 2 * transport.SequenceNumber + 1;
                            transport.SequenceNumber++;
                        }

                        transportMessage.SeqNo = sequenceNumber;
                        transportMessage.MsgId = transport.GenerateMessageId(true);
                    }
                    ((TLTransportMessage)transportMessage).SessionId = transport.SessionId ?? 0;


                    // TODO: replace with SendInformativeMessage
                    TLUtils.WriteLine("Corrected client time: " + TLUtils.MessageIdString(transportMessage.MsgId));
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
                    Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2,-4} SessionId {3} BadMsgId {4}", string.Format("{0}: {1}", historyItem.Caption, "seqNo"), transportMessage.MsgId, transportMessage.SeqNo, message.SessionId, badMessage.BadMsgId);
                    captionString = string.Format("{0} {1} {2}", historyItem.Caption, message.SessionId, transportMessage.MsgId);
                    SendPacketAsync(transport, captionString, encryptedMessage, (result) =>
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

            switch (badServerSalt.ErrorCode)
            {
                case 16:
                case 17:
                    TLUtils.WriteLine("1. CORRECT TIME DELTA with salt by transport " + transport.DCId);

                    var saveConfig = false;
                    long serverTime;
                    long clientTime;
                    lock (transport.SyncRoot)
                    {
                        serverTime = message.MsgId;
                        clientTime = transport.GenerateMessageId();

                        TLUtils.WriteLine("Server time: " + TLUtils.MessageIdString(BitConverter.GetBytes(serverTime)));
                        TLUtils.WriteLine("Client time: " + TLUtils.MessageIdString(BitConverter.GetBytes(clientTime)));

                        if (historyItem.ClientTicksDelta == transport.ClientTicksDelta)
                        {
                            saveConfig = true;
                            transport.ClientTicksDelta += serverTime - clientTime;
                        }

                        transportMessage.MsgId = transport.GenerateMessageId(true);
                        TLUtils.WriteLine("Corrected client time: " + TLUtils.MessageIdString(transportMessage.MsgId));
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

            var captionString = string.Format("{0} {1}", historyItem.Caption, transportMessage.MsgId);


            SendPacketAsync(transport, captionString, encryptedMessage, (result) =>
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

        private void ProcessRPCErrorByTransport(ITransport transport, TLRPCError error, HistoryItem historyItem, long keyId)
        {
            if (error.CodeEquals(TLErrorCode.UNAUTHORIZED))
            {
                Execute.ShowDebugMessage(string.Format("RPCError ByTransport {2} {0} {1}", historyItem.Caption, error, transport.DCId));

                if (historyItem != null && 
                    historyItem.Caption != "account.updateStatus" && 
                    historyItem.Caption != "account.registerDevice" && 
                    historyItem.Caption != "auth.signIn")
                {
                    if (error.TypeEquals(TLErrorType.SESSION_REVOKED))
                    {

                    }
                    else
                    {
                        RaiseAuthorizationRequired(new AuthorizationRequiredEventArgs { MethodName = "ByTransport " + transport.DCId + " " + historyItem.Caption, Error = error, AuthKeyId = keyId });
                    }
                }
                else if (historyItem != null && historyItem.FaultCallback != null)
                {
                    historyItem.FaultCallback(error);
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
                    var config = GetConfigAsync().Result;
                    {
                        _config = TLExtensions.Merge(_config, config.Value);
                        SaveConfig();
                        if (historyItem.Object.GetType() == typeof(TLAuthSendCode))
                        {
                            var dcOption = _config.DCOptions.First(x => x.IsValidIPv4Option(serverNumber));

                            lock (transport.SyncRoot)
                            {
                                var transportDCId = dcOption.Id;
                                var transportKey = dcOption.AuthKey;
                                var transportSalt = dcOption.Salt;
                                var transportSessionId = TLLong.Random();
                                var transportSequenceNumber = 0;
                                var transportClientTicksDelta = dcOption.ClientTicksDelta;
                                bool isCreated;
                                lock (_activeTransportRoot)
                                {
                                    transport = _transportService.GetTransport(dcOption.IpAddress.ToString(), dcOption.Port, Type, out isCreated);
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
                            //InitTransportAsync(transport, tuple =>
                            //{
                            //    lock (transport.SyncRoot)
                            //    {
                            //        transport.DCId = serverNumber;
                            //        transport.AuthKey = tuple.Item1;
                            //        transport.Salt = tuple.Item2;
                            //        transport.SessionId = tuple.Item3;
                            //    }
                            //    var authKeyId = TLUtils.GenerateLongAuthKeyId(tuple.Item1);

                            //    lock (_authKeysRoot)
                            //    {
                            //        if (!_authKeys.ContainsKey(authKeyId))
                            //        {
                            //            _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = tuple.Item1, AutkKeyId = authKeyId });
                            //        }
                            //    }

                            //    dcOption.AuthKey = tuple.Item1;
                            //    dcOption.Salt = tuple.Item2;
                            //    dcOption.SessionId = tuple.Item3;

                            //    _config.ActiveDCOptionIndex = _config.DCOptions.IndexOf(dcOption);
                            //    _cacheService.SetConfig(_config);

                            //    lock (transport.SyncRoot)
                            //    {
                            //        transport.Initialized = true;
                            //    }
                            //    RaiseInitialized();

                            //    SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                            //},
                            //er =>
                            //{
                            //    lock (transport.SyncRoot)
                            //    {
                            //        transport.Initialized = false;
                            //    }
                            //    historyItem.FaultCallback.SafeInvoke(er);
                            //});
                        }
                        else
                        {
                            //MigrateAsync(serverNumber, auth => SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback));
                        }
                    }
                }
                else
                {
                    if (historyItem.Object.GetType() == typeof(TLAuthSendCode) || historyItem.Object.GetType() == typeof(TLUploadGetFile))
                    {
                        var activeDCOption = _config.DCOptions.First(x => x.IsValidIPv4Option(serverNumber));

                        lock (transport.SyncRoot)
                        {
                            var transportDCId = activeDCOption.Id;
                            var transportKey = activeDCOption.AuthKey;
                            var transportSalt = activeDCOption.Salt;
                            var transportSessionId = TLLong.Random();
                            var transportSequenceNumber = 0;
                            var transportClientTicksDelta = activeDCOption.ClientTicksDelta;
                            bool isCreated;
                            lock (_activeTransportRoot)
                            {
                                _activeTransport = _transportService.GetTransport(activeDCOption.IpAddress.ToString(), activeDCOption.Port, Type, out isCreated);
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
                            //InitTransportAsync(transport, tuple =>
                            //{
                            //    lock (transport.SyncRoot)
                            //    {
                            //        transport.DCId = serverNumber;
                            //        transport.AuthKey = tuple.Item1;
                            //        transport.Salt = tuple.Item2;
                            //        transport.SessionId = tuple.Item3;
                            //    }

                            //    var authKeyId = TLUtils.GenerateLongAuthKeyId(tuple.Item1);

                            //    lock (_authKeysRoot)
                            //    {
                            //        if (!_authKeys.ContainsKey(authKeyId))
                            //        {
                            //            _authKeys.Add(authKeyId, new AuthKeyItem { AuthKey = tuple.Item1, AutkKeyId = authKeyId });
                            //        }
                            //    }

                            //    activeDCOption.AuthKey = tuple.Item1;
                            //    activeDCOption.Salt = tuple.Item2;
                            //    activeDCOption.SessionId = tuple.Item3;

                            //    _config.ActiveDCOptionIndex = _config.DCOptions.IndexOf(activeDCOption);
                            //    _cacheService.SetConfig(_config);

                            //    lock (transport.SyncRoot)
                            //    {
                            //        transport.Initialized = true;
                            //    }

                            //    RaiseInitialized();
                            //    SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                            //},
                            //er =>
                            //{
                            //    lock (transport.SyncRoot)
                            //    {
                            //        transport.Initialized = false;
                            //    }
                            //    historyItem.FaultCallback.SafeInvoke(er);
                            //});
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

                            //SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback);
                        }
                    }
                    else
                    {
                        //MigrateAsync(serverNumber, auth => SendInformativeMessage(historyItem.Caption, historyItem.Object, historyItem.Callback, historyItem.FaultCallback));
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

            var handled = false;

            if (!isInitialized)
            {
                try
                {
                    var message = TLFactory.From<TLNonEncryptedTransportMessage>(e.Data);

                    var item = transport.DequeueFirstNonEncryptedItem();
                    if (item != null)
                    {
#if LOG_REGISTRATION
                        TLUtils.WriteLog("OnReceivedBytes !IsInitialized try historyItem " + item.Caption);
#endif
                        item.Callback.TrySetResult(new MTProtoResponse(message.Query));
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
                //var position = 0;
                var encryptedMessage = new TLEncryptedTransportMessage();

                using (var reader = new TLBinaryReader(bytes))
                {
                    encryptedMessage.Read(reader, transport.AuthKey);
                }

                //check msg_key
                //var padding = encryptedMessage.Data.Length

                //msg_id


                //position = 0;
                //TLTransportMessage transportMessage;
                //transportMessage = TLObject.GetObject<TLTransportMessage>(encryptedMessage.Data, ref position);
                var transportMessage = encryptedMessage.Query as TLTransportMessage;
                if (transportMessage.SessionId != transport.SessionId.Value)
                {
                    throw new Exception("Incorrect session_id");
                }
                if ((transportMessage.MsgId % 2) == 0)
                {
                    throw new Exception("Incorrect message_id");
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
                SendAcknowledgmentsByTransport(transport, transportMessage);

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

                    ProcessBadMessageByTransport(transport, transportMessage, badMessage, item);
                }

                // bad server salts
                foreach (var badServerSalt in transportMessage.FindInnerObjects<TLBadServerSalt>())
                {
                    lock (transport.SyncRoot)
                    {
                        transport.Salt = badServerSalt.NewServerSalt;
                    }
                    HistoryItem item = null;
                    lock (_historyRoot)
                    {
                        if (_history.ContainsKey(badServerSalt.BadMsgId))
                        {
                            item = _history[badServerSalt.BadMsgId];
                        }
                    }

                    ProcessBadServerSaltByTransport(transport, transportMessage, badServerSalt, item);
                }

                // new session created
                foreach (var newSessionCreated in transportMessage.FindInnerObjects<TLNewSessionCreated>())
                {
                    TLUtils.WritePerformance(string.Format("NEW SESSION CREATED: {0} (old {1})", transportMessage.SessionId, _activeTransport.SessionId));
                    lock (transport.SyncRoot)
                    {
                        transport.SessionId = transportMessage.SessionId;
                        transport.Salt = newSessionCreated.ServerSalt;
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
                        Debug.WriteLine("RPCError: " + error.ErrorCode + " " + error.ErrorMessage + " MsgId " + result.RequestMsgId);
                        TLUtils.WriteLine("RPCError: " + error.ErrorCode + " " + error.ErrorMessage);

                        string errorString;
                        var reqError = error as TLRPCReqError;
                        if (reqError != null)
                        {
                            errorString = string.Format("RPCReqError {1} {2} (query_id={0}) transport=[dc_id={3}]", reqError.QueryId, reqError.ErrorCode, reqError.ErrorMessage, transport.DCId);
                        }
                        else
                        {
                            errorString = string.Format("RPCError {0} {1} transport=[dc_id={2}]", error.ErrorCode, error.ErrorMessage, transport.DCId);
                        }

                        Execute.ShowDebugMessage(historyItem + Environment.NewLine + errorString);
                        ProcessRPCErrorByTransport(transport, error, historyItem, encryptedMessage.AuthKeyId);
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
                            messageData is TLStatedMessageBase || */
                            messageData is TLUpdatesBase || 
                            messageData is TLMessagesSentEncryptedMessage || 
                            messageData is TLMessagesSentEncryptedFile ||
                            messageData is TLMessagesAffectedHistory ||
                            messageData is TLMessagesAffectedMessages ||
                            historyItem.Object is TLMessagesReadEncryptedHistory)
                        {
                            RemoveFromQueue(historyItem);
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
                UpdateStatusAsync(false);
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
                        keyValue.Value.FaultCallback.SafeInvoke(new TLRPCError { ErrorCode = 404, ErrorMessage = "Clear History" });
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
