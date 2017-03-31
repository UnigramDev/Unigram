using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Payments;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void ClearSavedInfoAsync(bool info, bool credentials, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPaymentsClearSavedInfo { IsInfo = info, IsCredentials = credentials };

            const string caption = "payments.clearSavedInfo";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetPaymentFormAsync(int msgId, Action<TLPaymentsPaymentForm> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPaymentsGetPaymentForm { MsgId = msgId };

            const string caption = "payments.getPaymentForm";
            SendInformativeMessage<TLPaymentsPaymentForm>(caption, obj, result =>
            {
                _cacheService.SyncUsersAndChats(result.Users, new TLVector<TLChatBase>(), tuple =>
                {
                    result.Users = tuple.Item1;
                    callback?.Invoke(result);
                });
            }, faultCallback);
        }

        public void GetPaymentReceiptAsync(int msgId, Action<TLPaymentsPaymentReceipt> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPaymentsGetPaymentReceipt { MsgId = msgId };

            const string caption = "payments.getPaymentReceipt";
            SendInformativeMessage<TLPaymentsPaymentReceipt>(caption, obj, result =>
            {
                _cacheService.SyncUsersAndChats(result.Users, new TLVector<TLChatBase>(), tuple =>
                {
                    result.Users = tuple.Item1;
                    callback?.Invoke(result);
                });
            }, faultCallback);
        }

        public void GetSavedInfoAsync(Action<TLPaymentsSavedInfo> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPaymentsGetSavedInfo { };

            const string caption = "payments.getSavedInfo";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        // TODO: public void SendPaymentFormCallback()
        
        public void ValidateRequestedInfoAsync(bool save, int msgId, TLPaymentRequestedInfo info, Action<TLPaymentsValidatedRequestedInfo> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPaymentsValidateRequestedInfo { IsSave = save, MsgId = msgId, Info = info };

            const string caption = "payments.validateRequestedInfo";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }
    }
}
