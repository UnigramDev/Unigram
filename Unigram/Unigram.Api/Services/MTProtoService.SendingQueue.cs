using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Telegram.Api.Extensions;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Messages.Methods;
using Telegram.Api.TL.Messages;
using Telegram.Api.Native.TL;
using Telegram.Api.Native;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        private void ReadEncryptedHistoryAsyncInternal(TLMessagesReadEncryptedHistory message, Action<bool> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.readEncryptedHistory", int.MaxValue, message, callback, fastCallback, faultCallback);
        }

        private void ReadHistoryAsyncInternal(TLMessagesReadHistory message, Action<TLMessagesAffectedMessages> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.readHistory", int.MaxValue, message, callback, fastCallback, faultCallback);
        }

        private void ReadMessageContentsAsyncInternal(TLMessagesReadMessageContents message, Action<TLMessagesAffectedMessages> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.readMessageContents", int.MaxValue, message, callback, fastCallback, faultCallback);
        }

        private void SendEncryptedAsyncInternal(TLMessagesSendEncrypted message, Action<TLMessagesSentEncryptedMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback) 
        {
            SendAsyncInternal("messages.sendEncrypted", Constants.MessageSendingInterval, message, callback, fastCallback, faultCallback, RequestFlag.InvokeAfter);
        }

        private void SendEncryptedFileAsyncInternal(TLMessagesSendEncryptedFile message, Action<TLMessagesSentEncryptedFile> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.sendEncryptedFile", Constants.MessageSendingInterval, message, callback, fastCallback, faultCallback, RequestFlag.InvokeAfter);
        }

        private void SendEncryptedServiceAsyncInternal(TLMessagesSendEncryptedService message, Action<TLMessagesSentEncryptedMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.sendEncryptedService", Constants.MessageSendingInterval, message, callback, fastCallback, faultCallback, RequestFlag.InvokeAfter);
        }

        private void SendMessageAsyncInternal(TLMessagesSendMessage message, Action<TLUpdatesBase> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.sendMessage", Constants.MessageSendingInterval, message, callback, fastCallback, faultCallback, RequestFlag.CanCompress | RequestFlag.InvokeAfter | RequestFlag.RequiresQuickAck);
        }

        private void SendInlineBotResultAsyncInternal(TLMessagesSendInlineBotResult message, Action<TLUpdatesBase> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.sendInlineBotResult", Constants.MessageSendingInterval, message, callback, fastCallback, faultCallback, RequestFlag.CanCompress | RequestFlag.InvokeAfter);
        }

        private void SendMediaAsyncInternal(TLMessagesSendMedia message, Action<TLUpdatesBase> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.sendMedia", Constants.MessageSendingInterval, message, callback, fastCallback, faultCallback, RequestFlag.CanCompress | RequestFlag.InvokeAfter);
        }

        private void StartBotAsyncInternal(TLMessagesStartBot message, Action<TLUpdatesBase> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.startBot", Constants.MessageSendingInterval, message, callback, fastCallback, faultCallback);
        }

        private void ForwardMessageAsyncInternal(TLMessagesForwardMessage message, Action<TLUpdatesBase> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.forwardMessage", Constants.MessageSendingInterval, message, callback, fastCallback, faultCallback, RequestFlag.CanCompress | RequestFlag.InvokeAfter);
        }

        private void ForwardMessagesAsyncInternal(TLMessagesForwardMessages message, Action<TLUpdatesBase> callback, Action fastCallback, Action<TLRPCError> faultCallback)
        {
            SendAsyncInternal("messages.forwardMessages", Constants.MessageSendingInterval, message, callback, fastCallback, faultCallback, RequestFlag.CanCompress | RequestFlag.InvokeAfter);
        }

        private void SendAsyncInternal<T>(string caption, double timeout, TLObject obj, Action<T> callback, Action fastCallback, Action<TLRPCError> faultCallback, RequestFlag flags = 0)
        {
            SendInformativeMessage<T>(caption, obj, callback, faultCallback, flags, fastCallback);
        }
    }
}
