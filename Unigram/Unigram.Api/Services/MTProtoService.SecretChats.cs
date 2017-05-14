using System;
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Messages;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void GetDHConfigAsync(int version, int randomLength, Action<TLMessagesDHConfig> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetDHConfig { Version = version, RandomLength = randomLength };

            const string caption = "messages.getDhConfig";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        // TODO: Encrypted 
        //public void RequestEncryptionAsync(TLInputUserBase userId, int randomId, byte[] ga, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null)
        //{
        //    var obj = new TLMessagesRequestEncryption { UserId = userId, RandomId = randomId, GA = ga };

        //    SendInformativeMessage<TLEncryptedChatBase>("messages.requestEncryption", obj,
        //        encryptedChat =>
        //        {
        //            _cacheService.SyncEncryptedChat(encryptedChat, callback.SafeInvoke);
        //        }, 
        //        faultCallback);
        //}

        // TODO: Encrypted 
        //public void AcceptEncryptionAsync(TLInputEncryptedChat peer, byte[] gb, long keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null)
        //{
        //    var obj = new TLMessagesAcceptEncryption { Peer = peer, GB = gb, KeyFingerprint = keyFingerprint };

        //    SendInformativeMessage<TLEncryptedChatBase>("messages.acceptEncryption", obj,
        //        encryptedChat =>
        //        {
        //            _cacheService.SyncEncryptedChat(encryptedChat, callback.SafeInvoke);
        //        },
        //        faultCallback);
        //}

        // TODO: Encrypted 
        //public void DiscardEncryptionAsync(int chatId, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        //{
        //    var obj = new TLMessagesDiscardEncryption { ChatId = chatId };

        //    SendInformativeMessage("messages.discardEncryption", obj, callback, faultCallback);
        //}
    }
}
