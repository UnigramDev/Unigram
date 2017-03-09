using System;
using System.Diagnostics;
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
	    private object _debugRoot = new object();

	    public void MessageAcknowledgments(TLVector<long> ids)
	    {
            PrintCaption("msgs_ack");
            TLUtils.WriteLine("ids");
	        foreach (var id in ids)
	        {
	            TLUtils.WriteLine(TLUtils.MessageIdString(id));
            }
            var obj = new TLMsgsAck { MsgIds = ids };

            var authKey = _activeTransport.AuthKey;
	        var sesseionId = _activeTransport.SessionId;
	        var salt = _activeTransport.Salt;

	        int sequenceNumber;
	        long messageId;
	        lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2;
                messageId = _activeTransport.GenerateMessageId(true);
	        }
            var transportMessage = CreateTLTransportMessage(salt ?? 0, sesseionId ?? 0, sequenceNumber, messageId, obj);
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
                        _activeTransport.SessionId = transportSessionId;
                        _activeTransport.SequenceNumber = transportSequenceNumber;
                        _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                        _activeTransport.PacketReceived += OnPacketReceived;
                    }
                }
	        }

	        lock (_debugRoot)
	        {
                //Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2, -4} SessionId {3}\nids:", "msgs_ack", transportMessage.MessageId.Value, transportMessage.SeqNo.Value, transportMessage.SessionId.Value);
                foreach (var id in ids)
	            {
	                //Debug.WriteLine(id.Value);
	            }
	        }

	        var captionString = string.Format("msgs_ack {0}", transportMessage.MsgId);
            SendPacketAsync(_activeTransport, captionString, encryptedMessage,
	            result =>
	            {
                    //Debug.WriteLine("@msgs_ack {0} result {1}", transportMessage.MessageId, result);
	            },
	            error =>
	            {
                    //Debug.WriteLine("<<msgs_ack failed " + transportMessage.MessageId);
	            });
	    }


        public void PingCallback(long pingId, Action<TLPong> callback, Action<TLRPCError> faultCallback = null)
	    {
	        var obj = new TLPing{ PingId = pingId };

            SendNonInformativeMessage<TLPong>("ping", obj,
                result =>
                {
                    callback?.Invoke(result);
                },
                faultCallback);
	    }

        public void PingDelayDisconnectCallback(long pingId, int disconnectDelay, Action<TLPong> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPingDelayDisconnect { PingId = pingId, DisconnectDelay = disconnectDelay };

            SendNonInformativeMessage<TLPong>("ping_delay_disconnect", obj,
                result =>
                {
                    callback?.Invoke(result);
                },
                faultCallback);
        }

	    public void HttpWaitAsync(int maxDelay, int waitAfter, int maxWait, Action callback, Action faultCallback)
	    {
            PrintCaption("http_wait");

            var obj = new TLHttpWait { MaxDelay = maxDelay, WaitAfter = waitAfter, MaxWait = maxWait };
            
            var authKey = _activeTransport.AuthKey;
            var salt = _activeTransport.Salt;
            var sessionId = _activeTransport.SessionId;

	        int sequenceNumber;
	        long messageId;
	        lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2;
                messageId = _activeTransport.GenerateMessageId(true);
	        }
            var transportMessage = CreateTLTransportMessage(salt ?? 0, sessionId ?? 0, sequenceNumber, messageId, obj);
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
                        _activeTransport.SessionId = transportSessionId;
                        _activeTransport.SequenceNumber = transportSequenceNumber;
                        _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                        _activeTransport.PacketReceived += OnPacketReceived;
                    }
                }
	        }

            SendPacketAsync(_activeTransport, "http_wait " + transportMessage.MsgId, encryptedMessage, 
                result =>
	            {
                    //try
                    //{
                    //    ReceiveBytesAsync(result, authKey);
                    //}
                    //catch (Exception e)
                    //{
                    //    TLUtils.WriteException(e);
                    //}
                    //finally
                    //{
                    //    callback();
                    //}
	            },
	            error => faultCallback?.Invoke());
	    }
	}
}
