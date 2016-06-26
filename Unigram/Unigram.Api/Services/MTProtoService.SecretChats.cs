using System;
using System.Threading.Tasks;
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Messages;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void RekeyAsync(TLEncryptedChatBase chat, Action<long> callback)
        {
            //GetGA()
        }

        public Task<MTProtoResponse<TLServerDHInnerData>> GetDHConfigAsync(int version, int randomLength)
        {
            return SendInformativeMessage<TLServerDHInnerData>("messages.getDhConfig", new TLMessagesGetDHConfig { Version = version, RandomLength = randomLength });
        }
        public async void GetDHConfigCallbackAsync(int version, int randomLength, Action<TLServerDHInnerData> callback, Action<TLRPCError> faultCallback = null)
        {
            var result = await SendInformativeMessage<TLServerDHInnerData>("messages.getDhConfig", new TLMessagesGetDHConfig { Version = version, RandomLength = randomLength });
            if (result?.IsSucceeded == true)
            {
                callback?.Invoke(result.Value);
            }
            else
            {
                faultCallback?.Invoke(result?.Error);
            }
        }

        public async Task<MTProtoResponse<TLEncryptedChatBase>> RequestEncryptionAsync(TLInputUserBase userId, int randomId, byte[] ga)
        {
            var obj = new TLMessagesRequestEncryption { UserId = userId, RandomId = randomId, GA = ga };

            var result = await SendInformativeMessage<TLEncryptedChatBase>("messages.requestEncryption", obj);
            if (result.Error == null)
            {
                // TODO: Secrets: 
                //var task = new TaskCompletionSource<MTProtoResponse<TLEncryptedChatBase>>();
                //_cacheService.SyncEncryptedChat(result.Value, (callback) =>
                //{
                //    task.TrySetResult(new MTProtoResponse<TLEncryptedChatBase>(callback));
                //});
                //return await task.Task;
            }

            return result;
        }

        public async Task<MTProtoResponse<TLEncryptedChatBase>> AcceptEncryptionAsync(TLInputEncryptedChat peer, byte[] gb, long keyFingerprint)
        {
            var obj = new TLMessagesAcceptEncryption { Peer = peer, GB = gb, KeyFingerprint = keyFingerprint };

            var result = await SendInformativeMessage<TLEncryptedChatBase>("messages.acceptEncryption", obj);
            if (result.Error == null)
            {
                // TODO: Secrets: 
                //var task = new TaskCompletionSource<MTProtoResponse<TLEncryptedChatBase>>();
                //_cacheService.SyncEncryptedChat(result.Value, (callback) =>
                //{
                //    task.TrySetResult(new MTProtoResponse<TLEncryptedChatBase>(callback));
                //});
                //return await task.Task;
            }

            return result;
        }
        public async void AcceptEncryptionCallbackAsync(TLInputEncryptedChat peer, byte[] gb, long keyFingerprint, Action<TLEncryptedChatBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesAcceptEncryption { Peer = peer, GB = gb, KeyFingerprint = keyFingerprint };

            var result = await SendInformativeMessage<TLEncryptedChatBase>("messages.acceptEncryption", obj);
            if (result.Error == null)
            {
                // TODO: Secrets: _cacheService.SyncEncryptedChat(result.Value, callback);
            }
            else
            {
                faultCallback?.Invoke(result?.Error);
            }

            //if (result?.IsSucceeded == true)
            //{
            //    callback?.Invoke(result.Value);
            //}
            //else
            //{
            //    faultCallback?.Invoke(result?.Error);
            //}
        }

        public Task<MTProtoResponse<bool>> DiscardEncryptionAsync(int chatId)
        {
            return SendInformativeMessage<bool>("messages.discardEncryption", new TLMessagesDiscardEncryption { ChatId = chatId });
        }
    }
}
