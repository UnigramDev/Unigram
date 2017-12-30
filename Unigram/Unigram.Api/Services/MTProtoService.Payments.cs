using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Native;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Payments;
using Telegram.Api.TL.Payments.Methods;

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
            }, faultCallback, flags: RequestFlag.FailOnServerError);
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
            }, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void GetSavedInfoAsync(Action<TLPaymentsSavedInfo> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPaymentsGetSavedInfo { };

            const string caption = "payments.getSavedInfo";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void SendPaymentFormAsync(int msgId, string infoId, string optionId, TLInputPaymentCredentialsBase credentials, Action<TLPaymentsPaymentResultBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPaymentsSendPaymentForm { MsgId = msgId, RequestedInfoId = infoId, ShippingOptionId = optionId, Credentials = credentials };

            const string caption = "payments.sendPaymentForm";
            SendInformativeMessage<TLPaymentsPaymentResultBase>(caption, obj,
                result =>
                {
                    if (result is TLPaymentsPaymentResult paymentResult)
                    {
                        var multiPts = paymentResult as ITLMultiPts;
                        if (multiPts != null)
                        {
                            _updatesService.SetState(multiPts, caption);
                        }
                        else
                        {
                            _updatesService.ProcessUpdates(paymentResult.Updates, true);
                        }
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.FailOnServerError);
        }
        
        public void ValidateRequestedInfoAsync(int msgId, TLPaymentRequestedInfo info, bool save, Action<TLPaymentsValidatedRequestedInfo> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPaymentsValidateRequestedInfo { IsSave = save, MsgId = msgId, Info = info };

            const string caption = "payments.validateRequestedInfo";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }
    }
}
