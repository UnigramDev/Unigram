using System;
using Telegram.Api.Extensions;
using Telegram.Api.Native;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Telegram.Api.TL.Messages.Methods;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void GetDHConfigAsync(int version, int randomLength, Action<TLMessagesDHConfig> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetDHConfig { Version = version, RandomLength = randomLength };

            const string caption = "messages.getDhConfig";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        // TODO: Encrypted 
        //public void RequestEncryptionAsync(TLInputUserBase userId, int randomId, byte[] ga, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null)
        //{
        //    var obj = new TLMessagesRequestEncryption { UserId = userId, RandomId = randomId, GA = ga };

        //    const string caption = "messages.requestEncryption";
        //    SendInformativeMessage<TLEncryptedChatBase>(caption, obj,
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

        //    const string caption = "messages.acceptEncryption";
        //    SendInformativeMessage<TLEncryptedChatBase>(caption, obj,
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

        //    const string caption = "messages.discardEncryption";
        //    SendInformativeMessage(caption, obj, callback, faultCallback);
        //}
    }
}
