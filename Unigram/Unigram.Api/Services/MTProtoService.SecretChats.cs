using System;
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Messages;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void RekeyAsync(TLEncryptedChatBase chat, Action<TLLong> callback)
        {
            //GetGA()
        }

        public void GetDHConfigAsync(TLInt version, TLInt randomLength, Action<TLDHConfigBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetDHConfig { Version = version, RandomLength = randomLength };

            SendInformativeMessage("messages.getDhConfig", obj, callback, faultCallback);
        }

        public void RequestEncryptionAsync(TLInputUserBase userId, TLInt randomId, TLString ga, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLRequestEncryption { UserId = userId, RandomId = randomId, G_A = ga };

            SendInformativeMessage<TLEncryptedChatBase>("messages.requestEncryption", obj,
                encryptedChat =>
                {
                    _cacheService.SyncEncryptedChat(encryptedChat, callback.SafeInvoke);
                }, 
                faultCallback);
        }

        public void AcceptEncryptionAsync(TLInputEncryptedChat peer, TLString gb, TLLong keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAcceptEncryption { Peer = peer, GB = gb, KeyFingerprint = keyFingerprint };

            SendInformativeMessage<TLEncryptedChatBase>("messages.acceptEncryption", obj,
                encryptedChat =>
                {
                    _cacheService.SyncEncryptedChat(encryptedChat, callback.SafeInvoke);
                },
                faultCallback);
        }

        public void DiscardEncryptionAsync(TLInt chatId, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLDiscardEncryption { ChatId = chatId };

            SendInformativeMessage("messages.discardEncryption", obj, callback, faultCallback);
        }
    }
}
