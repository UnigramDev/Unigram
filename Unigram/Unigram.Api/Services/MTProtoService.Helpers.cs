using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        private void PrintCaption(string caption)
        {
            TLUtils.WriteLine(" ");
            //TLUtils.WriteLine("------------------------");
            TLUtils.WriteLine(String.Format("-->>{0}", caption));
            TLUtils.WriteLine("------------------------");
        }

        private Task<MTProtoResponse<T>> SendNonInformativeMessage<T>(string caption, TLObject obj) where T : TLObject
        {
            var callback = new TaskCompletionSource<MTProtoResponse>();
            return SendNonInformativeMessage<T>(caption, obj, callback);
        }

        private async Task<MTProtoResponse<T>> SendNonInformativeMessage<T>(string caption, TLObject obj, TaskCompletionSource<MTProtoResponse> callback) where T : TLObject
        {
            PrintCaption(caption);

            int sequenceNumber;
            long messageId;
            lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2;
                messageId = _activeTransport.GenerateMessageId(true);
            }
            var authKey = _activeTransport.AuthKey;
            var salt = _activeTransport.Salt ?? 0;
            var sessionId = _activeTransport.SessionId ?? 0;
            var clientsTicksDelta = _activeTransport.ClientTicksDelta;
            var transportMessage = CreateTLTransportMessage(salt, sessionId, sequenceNumber, messageId, obj);
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

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
                        _activeTransport.SessionId = transportSessionId; //TLLong.Random(); 
                        _activeTransport.SequenceNumber = transportSequenceNumber;
                        _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                        _activeTransport.PacketReceived += OnPacketReceived;
                    }
                }
            }

            HistoryItem historyItem = null;
            if (string.Equals(caption, "ping", StringComparison.OrdinalIgnoreCase)
                || string.Equals(caption, "ping_delay_disconnect", StringComparison.OrdinalIgnoreCase)
                || string.Equals(caption, "messages.container", StringComparison.OrdinalIgnoreCase))
            {
                //save items to history
                historyItem = new HistoryItem
                {
                    SendTime = DateTime.Now,
                    //SendBeforeTime = sendBeforeTime,
                    Caption = caption,
                    Callback = callback,
                    Object = obj,
                    Message = transportMessage,
                    AttemptFailed = null,
                    ClientTicksDelta = clientsTicksDelta,
                    Status = RequestStatus.Sent,
                };

                lock (_historyRoot)
                {
                    _history[historyItem.Hash] = historyItem;
                }
#if DEBUG
                NotifyOfPropertyChange(() => History);
#endif
            }

            Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2, -4} SessionId {3}", caption, transportMessage.MsgId, transportMessage.SeqNo, transportMessage.SessionId);

            var captionString = string.Format("{0} {1}", caption, transportMessage.MsgId);
            SendPacketAsync(_activeTransport, captionString, encryptedMessage, (result) =>
            {
                if (!result)
                {
                    if (historyItem != null)
                    {
                        lock (_historyRoot)
                        {
                            _history.Remove(historyItem.Hash);
                        }

#if DEBUG
                        NotifyOfPropertyChange(() => History);
#endif
                    }
                    callback.TrySetResult(new MTProtoResponse(new TLRPCError { ErrorCode = 404, ErrorMessage = $"FastCallback SocketError={result}" }));
                }
            }, 
            (error) =>
            {
                if (historyItem != null)
                {
                    lock (_historyRoot)
                    {
                        _history.Remove(historyItem.Hash);
                    }
                }
                callback.TrySetResult(new MTProtoResponse(new TLRPCError { ErrorCode = 404 }));
                //{
                //    Exception = error.Exception
                //}));
            });

            return await callback.Task;
        }

        private void SendPacketAsync(ITransport transport, string caption, TLObject data, Action<bool> callback, Action<TcpTransportResult> faultCallback = null)
        {
            transport.SendPacketAsync(caption, data.ToArray(), callback, faultCallback);
        }

        private readonly object _historyRoot = new object();

        public void SendInformativeMessageInternal<T>(string caption, TLObject obj,
            TaskCompletionSource<MTProtoResponse> callback,
            int? maxAttempt = null, // to send delayed items
            Action<int> attemptFailed = null,
            Action fastCallback = null) // to send delayed items
        {
            MTProtoResponse<T> result = null;
            if (_activeTransport.AuthKey == null)
            {
                var delayedItem = new DelayedItem
                {
                    SendTime = DateTime.Now,
                    //SendBeforeTime = sendBeforeTime,
                    Caption = caption,
                    Callback = callback,
                    Object = obj,
                    AttemptFailed = attemptFailed,
                    MaxAttempt = maxAttempt
                };
                lock (_delayedItemsRoot)
                {
                    _delayedItems.Add(delayedItem);
                }

#if LOG_REGISTRATION
                TLUtils.WriteLog(string.Format("Add delayed item {0} sendTime={1}", delayedItem.Caption, delayedItem.SendTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
#endif

                return;
            }

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
                        _activeTransport.SessionId = transportSessionId; //TLLong.Random();
                        _activeTransport.SequenceNumber = transportSequenceNumber;
                        _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                        _activeTransport.PacketReceived += OnPacketReceived;
                    }
                }
            }

            PrintCaption(caption);

            TLObject data;
            lock (_activeTransportRoot)
            {
                if (!_activeTransport.Initiated || caption == "auth.sendCode")
                {
                    //var cultureinfo = CultureInfo.InvariantCulture
                    //SystemEvents.TimeChanged
                    var initConnection = new TLInitConnection
                    {
                        ApiId = Constants.ApiId,
                        AppVersion = _deviceInfo.AppVersion,
                        Query = obj,
                        DeviceModel = _deviceInfo.Model,
                        LangCode = Utils.CurrentUICulture(),
                        SystemVersion = _deviceInfo.SystemVersion
                    };

                    SaveInitConnectionAsync(initConnection);

                    var withLayerN = new TLInvokeWithLayer { Query = initConnection, Layer = Constants.SupportedLayer };
                    data = withLayerN;
                    _activeTransport.Initiated = true;
                }
                else
                {
                    data = obj;
                }
            }

            int sequenceNumber;
            long messageId;
            lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2 + 1;
                _activeTransport.SequenceNumber++;
                messageId = _activeTransport.GenerateMessageId(true);
            }
            var authKey = _activeTransport.AuthKey;
            var salt = _activeTransport.Salt ?? 0;
            var sessionId = _activeTransport.SessionId ?? 0;
            var clientsTicksDelta = _activeTransport.ClientTicksDelta;
            var transportMessage = CreateTLTransportMessage(salt, sessionId, sequenceNumber, messageId, data);
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

            //save items to history
            var historyItem = new HistoryItem
            {
                SendTime = DateTime.Now,
                //SendBeforeTime = sendBeforeTime,
                Caption = caption,
                Callback = callback,
                Object = obj,
                Message = transportMessage,
                AttemptFailed = attemptFailed,
                ClientTicksDelta = clientsTicksDelta,
                Status = RequestStatus.Sent,
            };

            lock (_historyRoot)
            {
                _history[historyItem.Hash] = historyItem;
            }
#if LOG_REGISTRATION
            TLUtils.WriteLog(string.Format("Add history item {0} sendTime={1}", historyItem.Caption, historyItem.SendTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
#endif
#if DEBUG
            NotifyOfPropertyChange(() => History);
#endif

            Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2, -4} SessionId {3} ClientTicksDelta {4}", caption, transportMessage.MsgId, transportMessage.SeqNo, transportMessage.SessionId, clientsTicksDelta);

            var captionString = string.Format("{0} {1} {2}", caption, transportMessage.SessionId, transportMessage.MsgId);
            SendPacketAsync(_activeTransport, captionString, encryptedMessage, (boolResult) =>
            {
                if (!boolResult)
                {
                    if (historyItem != null)
                    {
                        lock (_historyRoot)
                        {
                            _history.Remove(historyItem.Hash);
                        }
                    }
                    //faultCallback.SafeInvoke(new TLRPCError(404)
                    //{
                    //    Message = new string("FastCallback SocketError=" + result)
                    //});

                    callback?.TrySetResult(new MTProtoResponse(new TLRPCError { ErrorCode = 404, ErrorMessage = $"FastCallback SocketError={result}" }));
                }
            }, 
            (error) =>
            {
                if (historyItem != null)
                {
                    lock (_historyRoot)
                    {
                        _history.Remove(historyItem.Hash);
                    }
                }
                //faultCallback.SafeInvoke(new TLRPCError(404)
                //{
                //    Exception = error.Exception
                //});

                callback?.TrySetResult(new MTProtoResponse(new TLRPCError { ErrorCode = 404, /* Exception = error.Exception */ }));
            });
        }

        private async Task<MTProtoResponse<T>> SendInformativeMessage<T>(string caption, TLObject obj,
            int? maxAttempt = null,                 // to send delayed items
            Action<int> attemptFailed = null)       // to send delayed items

        {
            var callback = new TaskCompletionSource<MTProtoResponse>();
            SendInformativeMessageInternal<T>(caption, obj, callback, maxAttempt, attemptFailed);
            return await callback.Task;
        }

        private async Task<MTProtoResponse<T>> SendNonEncryptedMessage<T>(string caption, TLObject obj)
        {
            PrintCaption(caption);
            long messageId;
            lock (_activeTransportRoot)
            {
                messageId = _activeTransport.GenerateMessageId();
            }
            var message = CreateTLNonEncryptedMessage(messageId, obj);

            var callback = new TaskCompletionSource<MTProtoResponse>();

            var historyItem = new HistoryItem
            {
                Caption = caption,
                Callback = callback,
                Message = message,
                SendTime = DateTime.Now,
                Status = RequestStatus.Sent
            };

            var guid = message.MsgId;
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

            var activeTransport = _activeTransport; // до вызова callback _activeTransport может уже поменяться

            // Сначала создаем или получаем транспорт, а потом добавляем в его историю.
            // Если сначала добавить в историю транспорта, то потом можем получить новый и не найдем запрос
            _activeTransport.EnqueueNonEncryptedItem(historyItem);

            var bytes = message.ToArray();
#if LOG_REGISTRATION
            TLUtils.WriteLog(string.Format("SendPacketAsync {0} [{1}](data length={2})", _activeTransport.Id, caption, bytes.Length));
#endif
            var captionString = string.Format("{0} {1}", caption, guid);
            SendPacketAsync(_activeTransport, captionString, message, (socketError) =>
            {
                if (!socketError)
                {
                    var flag = activeTransport.RemoveNonEncryptedItem(historyItem);
                    if (flag)
                    {
                        callback.TrySetResult(new MTProtoResponse<T>(new TLRPCError { ErrorCode = 404, ErrorMessage = $"FastCallback SocketError={socketError}" }));
                    }
                }
            }, 
            (error) =>
            {
#if LOG_REGISTRATION
                TLUtils.WriteLog(string.Format("SendPacketAsync error {0} [{1}] error={2}", activeTransport.Id, caption, error));
#endif
                var flag = activeTransport.RemoveNonEncryptedItem(historyItem);
                if (flag)
                {
                    callback.TrySetResult(new MTProtoResponse<T>(new TLRPCError { ErrorCode = 404, ErrorMessage = "FaltCallback" }));
                }
            });

            var result = await callback.Task;
            return result;
        }

        private static TLEncryptedTransportMessage CreateTLEncryptedMessage(byte[] authKey, TLContainerTransportMessage containerTransportMessage)
        {
            var message = new TLEncryptedTransportMessage { Query = containerTransportMessage };

            return message.Encrypt(authKey);
        }

        private TLTransportMessage CreateTLTransportMessage(long salt, long sessionId, int seqNo, long messageId, TLObject obj)
        {
            var message = new TLTransportMessage();
            message.Salt = salt;
            message.SessionId = sessionId;
            message.MsgId = messageId;
            message.SeqNo = seqNo;
            message.Query = obj;

            return message;
        }

        public static TLNonEncryptedTransportMessage CreateTLNonEncryptedMessage(long messageId, TLObject obj)
        {
            var message = new TLNonEncryptedTransportMessage();
            message.AuthKeyId = 0;
            message.MsgId = messageId;
            message.Query = obj;

            return message;
        }
    }
}
