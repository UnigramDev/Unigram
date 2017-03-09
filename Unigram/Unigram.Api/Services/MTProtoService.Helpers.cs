using System;
using System.Diagnostics;
using System.Linq;
#if WINDOWS_PHONE
using System.Globalization;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Methods.Help;
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

        private void SendNonInformativeMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null) where T : TLObject
        {
            PrintCaption(caption);

            int sequenceNumber;
            long? messageId;
            lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2;
                messageId = _activeTransport.GenerateMessageId(true);
            }
            var authKey = _activeTransport.AuthKey;
            var salt = _activeTransport.Salt;
            var sessionId = _activeTransport.SessionId;
            var clientsTicksDelta = _activeTransport.ClientTicksDelta;
            var transportMessage = CreateTLTransportMessage(salt ?? 0, sessionId ?? 0, sequenceNumber, messageId ?? 0, obj);
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
                    Object = obj,
                    Message = transportMessage,
                    Callback = t => callback((T)t),
                    AttemptFailed = null,
                    FaultCallback = faultCallback,
                    ClientTicksDelta = clientsTicksDelta,
                    Status = RequestStatus.Sent,
                };

                lock (_historyRoot)
                {
                    _history[historyItem.Hash] = historyItem;
                }
#if DEBUG
                RaisePropertyChanged(() => History);
#endif
            }

            //Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2, -4} SessionId {3}", caption, transportMessage.MessageId.Value, transportMessage.SeqNo.Value, transportMessage.SessionId.Value);
            
            var captionString = string.Format("{0} {1}", caption, transportMessage.MsgId);
            SendPacketAsync(_activeTransport, captionString, encryptedMessage,
                result =>
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
                            RaisePropertyChanged(() => History);
#endif
                        }
                        faultCallback?.Invoke(new TLRPCError { ErrorCode = 404, ErrorMessage = "FastCallback SocketError=" + result });
                    }
                },
                error =>
                {
                    if (historyItem != null)
                    {
                        lock (_historyRoot)
                        {
                            _history.Remove(historyItem.Hash);
                        }
#if DEBUG
                        RaisePropertyChanged(() => History);
#endif
                    }
                    faultCallback?.Invoke(new TLRPCError
                    {
                        ErrorCode = 404,
#if WINDOWS_PHONE
                        SocketError = error.Error,
#endif
                        // TODO: Exception = error.Exception
                    });
                });
        }

	    private void SendPacketAsync(ITransport transport, string caption, TLObject data, Action<bool> callback, Action<TcpTransportResult> faultCallback = null)
	    {
	        if (_deviceInfo != null && _deviceInfo.IsBackground)
	        {
	            
	        }

            transport.SendPacketAsync(
                caption,
                data.ToArray(),
                callback,
                faultCallback);
	    }

        private readonly object _historyRoot = new object();

	    public void SendInformativeMessageInternal<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null,
	        int? maxAttempt = null, // to send delayed items
	        Action<int> attemptFailed = null,
            Action fastCallback = null) // to send delayed items
	    {
            if (_activeTransport.AuthKey == null)
            {
                var delayedItem = new DelayedItem
                {
                    SendTime = DateTime.Now,
                    //SendBeforeTime = sendBeforeTime,
                    Caption = caption,
                    Object = obj,
                    Callback = t => callback((T)t),
                    AttemptFailed = attemptFailed,
                    FaultCallback = faultCallback,
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

                    // TODO?
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
            long? messageId;
            lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2 + 1;
                _activeTransport.SequenceNumber++;
                messageId = _activeTransport.GenerateMessageId(true);
            }
            var authKey = _activeTransport.AuthKey;
            var salt = _activeTransport.Salt;
            var sessionId = _activeTransport.SessionId;
            var clientsTicksDelta = _activeTransport.ClientTicksDelta;
            var transportMessage = CreateTLTransportMessage(salt ?? 0, sessionId ?? 0, sequenceNumber, messageId ?? 0, data);
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

            //save items to history
            var historyItem = new HistoryItem
            {
                SendTime = DateTime.Now,
                //SendBeforeTime = sendBeforeTime,
                Caption = caption,
                Object = obj,
                Message = transportMessage,
                Callback = t => callback((T)t),
                AttemptFailed = attemptFailed,
                FaultCallback = faultCallback,
                ClientTicksDelta = clientsTicksDelta,
                Status = RequestStatus.Sent,
            };

            lock (_historyRoot)
            {
#if DEBUG
                HistoryItem existingItem;
                if (_history.TryGetValue(historyItem.Hash, out existingItem))
                {
                    Execute.ShowDebugMessage(string.Format("Duplicated history item hash={0} existing={1} new={2}", historyItem.Hash, existingItem.Caption, historyItem.Caption));
                }
                _history[historyItem.Hash] = historyItem;
#else
                _history[historyItem.Hash] = historyItem;
#endif
            }

#if DEBUG
            ITransport transport;
            lock (_activeTransportRoot)
            {
                transport = _activeTransport;
            }
            var transportId = transport.Id;
            var lastReceiveTime = transport.LastReceiveTime;
            int historyCount;
            string historyDescription;
            lock (_historyRoot)
            {
                historyCount = _history.Count;
                historyDescription = string.Join("\n", _history.Values.Select(x => x.Caption + " " + x.Hash));
            }
            var currentPacketLength = transport.PacketLength;
            var lastPacketLength = transport.LastPacketLength;

            RaiseTransportChecked(new TransportCheckedEventArgs
            {
                TransportId = transportId,
                SessionId = sessionId,
                AuthKey = authKey,
                LastReceiveTime = lastReceiveTime,
                HistoryCount = historyCount,
                HistoryDescription = historyDescription,
                NextPacketLength = currentPacketLength,
                LastPacketLength = lastPacketLength
            });
#endif

#if LOG_REGISTRATION
            TLUtils.WriteLog(string.Format("Add history item {0} sendTime={1}", historyItem.Caption, historyItem.SendTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
#endif
#if DEBUG
	        if (historyItem.Caption != "account.updateStatus") // to avoid deadlock on deactivation
	        {
	            RaisePropertyChanged(() => History);
	        }
#endif

            //Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2, -4} SessionId {3} ClientTicksDelta {4}", caption, transportMessage.MessageId.Value, transportMessage.SeqNo.Value, transportMessage.SessionId.Value, clientsTicksDelta);

            var captionString = string.Format("{0} {1} {2}", caption, transportMessage.SessionId, transportMessage.MsgId);
            SendPacketAsync(_activeTransport, captionString, encryptedMessage,
                result =>
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
                            if (historyItem.Caption != "account.updateStatus") // to avoid deadlock on deactivation
                            {
                                RaisePropertyChanged(() => History);
                            }
#endif
                        }
                        faultCallback?.Invoke(new TLRPCError { ErrorCode = 404, ErrorMessage = "FastCallback SocketError=" + result });
                    }
                },
                error =>
                {
                    if (historyItem != null)
                    {
                        lock (_historyRoot)
                        {
                            _history.Remove(historyItem.Hash);
                        }
#if DEBUG
                        if (historyItem.Caption != "account.updateStatus") // to avoid deadlock on deactivation
                        {
                            RaisePropertyChanged(() => History);
                        }
#endif
                    }
                    faultCallback?.Invoke(new TLRPCError
                    {
                        ErrorCode = 404,
#if WINDOWS_PHONE
                        SocketError = error.Error,
#endif
                        // TODO: Exception = error.Exception
                    });
                });
	    }

	    private void SendInformativeMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null, 
            int? maxAttempt = null,                 // to send delayed items
            Action<int> attemptFailed = null)       // to send delayed items
	    {
            Execute.BeginOnThreadPool(() =>
            {
                SendInformativeMessageInternal(caption, obj, callback, faultCallback, maxAttempt, attemptFailed);
            });
        }

        private void SendNonEncryptedMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null)
            where T : TLObject
        {
            PrintCaption(caption);
            long messageId;
            lock (_activeTransportRoot)
            {
                messageId = _activeTransport.GenerateMessageId();
            }
            var message = CreateTLNonEncryptedMessage(messageId, obj);

            var historyItem = new HistoryItem
            {
                Caption = caption,
                Message = message,
                Callback = t => callback((T) t),
                FaultCallback = faultCallback,
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
            SendPacketAsync(_activeTransport, captionString, message,
                socketError =>
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog(string.Format("SendPacketAsync fastCallback {0} [{1}] socketError={2}", activeTransport.Id, caption, socketError));
#endif
                    if (!socketError)
                    {
                        var result = activeTransport.RemoveNonEncryptedItem(historyItem);

                        if (result)
                        {
                            faultCallback?.Invoke(new TLRPCError { ErrorCode = 404, ErrorMessage = "FastCallback SocketError=" + socketError });
                        }
                    }                  
                },
                error =>
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog(string.Format("SendPacketAsync error {0} [{1}] error={2}", activeTransport.Id, caption, error));
#endif
                    var result = activeTransport.RemoveNonEncryptedItem(historyItem);

                    // чтобы callback не вызвался два раза из CheckTimeouts и отсюда
                    if (result)
                    {
                        faultCallback?.Invoke(new TLRPCError { ErrorCode = 404, ErrorMessage = "FaltCallback" });
                    }                    
                });
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
