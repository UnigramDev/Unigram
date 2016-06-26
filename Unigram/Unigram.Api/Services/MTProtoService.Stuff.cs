using System;
using System.Diagnostics;
using System.Threading.Tasks;
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
            var sessionId = _activeTransport.SessionId ?? 0;
            var salt = _activeTransport.Salt ?? 0;

            int sequenceNumber;
            long messageId;
            lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2;
                messageId = _activeTransport.GenerateMessageId(true);
            }
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
                        _activeTransport.SessionId = transportSessionId;
                        _activeTransport.SequenceNumber = transportSequenceNumber;
                        _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                        _activeTransport.PacketReceived += OnPacketReceived;
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
            SendPacketAsync(_activeTransport, captionString, encryptedMessage, (result) =>
            {
                Debug.WriteLine("@msgs_ack {0} result {1}", transportMessage.MsgId, result);
            }, 
            (error) =>
            {
                Debug.WriteLine("<<msgs_ack failed " + transportMessage.MsgId);
            });
        }


        public Task<MTProtoResponse<TLPong>> PingAsync(long pingId)
        {
            return SendNonInformativeMessage<TLPong>("ping", new TLPing { PingId = pingId });
        }

        public Task<MTProtoResponse<TLPong>> PingDelayDisconnectAsync(long pingId, int disconnectDelay)
        {
            return SendNonInformativeMessage<TLPong>("ping_delay_disconnect", new TLPingDelayDisconnect { PingId = pingId, DisconnectDelay = disconnectDelay });
        }

        public void HttpWaitAsync(int maxDelay, int waitAfter, int maxWait, Action callback, Action faultCallback)
        {
            PrintCaption("http_wait");

            var obj = new TLHttpWait { MaxDelay = maxDelay, WaitAfter = waitAfter, MaxWait = maxWait };

            var authKey = _activeTransport.AuthKey;
            var salt = _activeTransport.Salt ?? 0;
            var sessionId = _activeTransport.SessionId ?? 0;

            int sequenceNumber;
            long messageId;
            lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2;
                messageId = _activeTransport.GenerateMessageId(true);
            }
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
                        _activeTransport.SessionId = transportSessionId;
                        _activeTransport.SequenceNumber = transportSequenceNumber;
                        _activeTransport.ClientTicksDelta = transportClientTicksDelta;
                        _activeTransport.PacketReceived += OnPacketReceived;
                    }
                }
            }

            SendPacketAsync(_activeTransport, "http_wait " + transportMessage.MsgId, encryptedMessage, (result) => { }, (error) =>
            {
                faultCallback.SafeInvoke();
            });
        }
    }
}
