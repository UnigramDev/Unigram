using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Native;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Phone;
using Telegram.Api.TL.Phone.Methods;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void AcceptCallAsync(TLInputPhoneCall peer, byte[] gb, Action<TLPhonePhoneCall> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhoneAcceptCall { Peer = peer, GB = gb, Protocol = GetPhoneCallProtocol() };

            const string caption = "phone.acceptCall";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void ConfirmCallAsync(TLInputPhoneCall peer, byte[] ga, long fingerprint, Action<TLPhonePhoneCall> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhoneConfirmCall { Peer = peer, GA = ga, KeyFingerprint = fingerprint, Protocol = GetPhoneCallProtocol() };

            const string caption = "phone.confirmCall";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void DiscardCallAsync(TLInputPhoneCall peer, int duration, TLPhoneCallDiscardReasonBase reason, long connectionId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhoneDiscardCall { Peer = peer, Duration = duration, Reason = reason, ConnectionId = connectionId };

            const string caption = "phone.discardCall";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        _updatesService.ProcessUpdates(result, true);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void GetCallConfigAsync(Action<TLDataJSON> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhoneGetCallConfig();

            const string caption = "phone.getCallConfig";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ReceivedCallAsync(TLInputPhoneCall peer, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhoneReceivedCall { Peer = peer };

            const string caption = "phone.receivedCall";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void RequestCallAsync(TLInputUserBase userId, int randomId, byte[] gaHash, Action<TLPhonePhoneCall> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhoneRequestCall { UserId = userId, RandomId = randomId, GAHash = gaHash, Protocol = GetPhoneCallProtocol() };

            const string caption = "phone.requestCall";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void SaveCallDebugAsync(TLInputPhoneCall peer, TLDataJSON debug, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhoneSaveCallDebug { Peer = peer, Debug = debug };

            const string caption = "phone.saveCallDebug";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void SetCallRatingAsync(TLInputPhoneCall peer, int rating, string comment, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhoneSetCallRating { Peer = peer, Rating = rating, Comment = comment };

            const string caption = "phone.setCallRating";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        _updatesService.ProcessUpdates(result, true);
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        private TLPhoneCallProtocol GetPhoneCallProtocol()
        {
            return new TLPhoneCallProtocol
            {
                MaxLayer = Constants.CallsMaxLayer,
                MinLayer = Constants.CallsMinLayer,
                IsUdpP2p = true,
                IsUdpReflector = true
            };
        }
    }
}
