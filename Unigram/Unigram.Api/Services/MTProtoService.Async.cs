using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Account;
using Telegram.Api.TL.Auth;
using Telegram.Api.TL.Channels;
using Telegram.Api.TL.Channels.Methods;
using Telegram.Api.TL.Contacts;
using Telegram.Api.TL.Contacts.Methods;
using Telegram.Api.TL.Help;
using Telegram.Api.TL.Messages;
using Telegram.Api.TL.Payments;
using Telegram.Api.TL.Phone;
using Telegram.Api.TL.Photos;
using Telegram.Api.TL.Updates;
using Telegram.Api.TL.Upload;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLLangPackDifference>> GetDifferenceAsync(int fromVersion)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLLangPackDifference>>();
            GetDifferenceAsync(fromVersion, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLLangPackDifference>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLLangPackDifference>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLLangPackDifference>> GetLangPackAsync(string langCode)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLLangPackDifference>>();
            GetLangPackAsync(langCode, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLLangPackDifference>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLLangPackDifference>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLVector<TLLangPackLanguage>>> GetLanguagesAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<TLLangPackLanguage>>>();
            GetLanguagesAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLLangPackLanguage>>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLLangPackLanguage>>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLVector<TLLangPackStringBase>>> GetStringsAsync(string langCode, TLVector<string> keys)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<TLLangPackStringBase>>>();
            GetStringsAsync(langCode, keys, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLLangPackStringBase>>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLLangPackStringBase>>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ResetSavedAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ResetSavedAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> SendMultiMediaAsync(TLInputPeerBase inputPeer, TLVector<TLInputSingleMedia> multiMedia, IList<TLMessage> messages)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            SendMultiMediaAsync(inputPeer, multiMedia, messages, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessageMediaBase>> UploadMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessageMediaBase>>();
            UploadMediaAsync(inputPeer, inputMedia, message, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessageMediaBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessageMediaBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesAffectedHistory>> ReadMentionsAsync(TLInputPeerBase inputPeer)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedHistory>>();
            ReadMentionsAsync(inputPeer, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedHistory>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedHistory>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> TogglePreHistoryHiddenAsync(TLInputChannelBase channel, bool enabled)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            TogglePreHistoryHiddenAsync(channel, enabled, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> DeleteHistoryAsync(TLInputChannelBase inputChannel, int maxId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            DeleteHistoryAsync(inputChannel, maxId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesMessagesBase>> GetUnreadMentionsAsync(TLInputPeerBase inputPeer, int offsetId, int addOffset, int limit, int maxId, int minId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
            GetUnreadMentionsAsync(inputPeer, offsetId, addOffset, limit, maxId, minId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLChannelsAdminLogResults>> GetAdminLogAsync(TLInputChannelBase inputChannel, string query, TLChannelAdminLogEventsFilter filter, TLVector<TLInputUserBase> admins, long maxId, long minId, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLChannelsAdminLogResults>>();
            GetAdminLogAsync(inputChannel, query, filter, admins, maxId, minId, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLChannelsAdminLogResults>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLChannelsAdminLogResults>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPhonePhoneCall>> AcceptCallAsync(TLInputPhoneCall peer, byte[] gb)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPhonePhoneCall>>();
            AcceptCallAsync(peer, gb, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPhonePhoneCall>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPhonePhoneCall>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPhonePhoneCall>> ConfirmCallAsync(TLInputPhoneCall peer, byte[] ga, long fingerprint)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPhonePhoneCall>>();
            ConfirmCallAsync(peer, ga, fingerprint, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPhonePhoneCall>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPhonePhoneCall>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> DiscardCallAsync(TLInputPhoneCall peer, int duration, TLPhoneCallDiscardReasonBase reason, long connectionId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            DiscardCallAsync(peer, duration, reason, connectionId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLDataJSON>> GetCallConfigAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLDataJSON>>();
            GetCallConfigAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLDataJSON>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLDataJSON>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ReceivedCallAsync(TLInputPhoneCall peer)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ReceivedCallAsync(peer, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPhonePhoneCall>> RequestCallAsync(TLInputUserBase userId, int randomId, byte[] gaHash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPhonePhoneCall>>();
            RequestCallAsync(userId, randomId, gaHash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPhonePhoneCall>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPhonePhoneCall>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> SaveCallDebugAsync(TLInputPhoneCall peer, TLDataJSON debug)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            SaveCallDebugAsync(peer, debug, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> SetCallRatingAsync(TLInputPhoneCall peer, int rating, string comment)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            SetCallRatingAsync(peer, rating, comment, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }









        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLVector<TLStickerSetCoveredBase>>> GetAttachedStickersAsync(TLInputStickeredMediaBase media)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<TLStickerSetCoveredBase>>>();
            GetAttachedStickersAsync(media, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLStickerSetCoveredBase>>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLStickerSetCoveredBase>>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<T>> SendRequestAsync<T>(string caption, TLObject obj)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<T>>();
            SendInformativeMessage<T>(caption, obj, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<T>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<T>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ClearSavedInfoAsync(bool info, bool credentials)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ClearSavedInfoAsync(info, credentials, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPaymentsPaymentForm>> GetPaymentFormAsync(int msgId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPaymentsPaymentForm>>();
            GetPaymentFormAsync(msgId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsPaymentForm>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsPaymentForm>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPaymentsPaymentReceipt>> GetPaymentReceiptAsync(int msgId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPaymentsPaymentReceipt>>();
            GetPaymentReceiptAsync(msgId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsPaymentReceipt>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsPaymentReceipt>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPaymentsSavedInfo>> GetSavedInfoAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPaymentsSavedInfo>>();
            GetSavedInfoAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsSavedInfo>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsSavedInfo>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPaymentsPaymentResultBase>> SendPaymentFormAsync(int msgId, string infoId, string optionId, TLInputPaymentCredentialsBase credentials)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPaymentsPaymentResultBase>>();
            SendPaymentFormAsync(msgId, infoId, optionId, credentials, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsPaymentResultBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsPaymentResultBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAccountTmpPassword>> GetTmpPasswordAsync(byte[] hash, int period)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountTmpPassword>>();
            GetTmpPasswordAsync(hash, period, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountTmpPassword>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountTmpPassword>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPaymentsValidatedRequestedInfo>> ValidateRequestedInfoAsync(int msgId, TLPaymentRequestedInfo info, bool save)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPaymentsValidatedRequestedInfo>>();
            ValidateRequestedInfoAsync(msgId, info, save, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsValidatedRequestedInfo>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPaymentsValidatedRequestedInfo>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesChatsBase>> GetCommonChatsAsync(TLInputUserBase id, int maxId, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesChatsBase>>();
            GetCommonChatsAsync(id, maxId, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesChatsBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesChatsBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ReorderPinnedDialogsAsync(TLVector<TLInputPeerBase> order, bool force)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ReorderPinnedDialogsAsync(order, force, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ToggleDialogPinAsync(TLInputPeerBase peer, bool pin)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ToggleDialogPinAsync(peer, pin, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthSentCode>> SendCodeAsync(string phoneNumber, bool? currentNumber, Action<int> attemptFailed = null)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
            SendCodeAsync(phoneNumber, currentNumber, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
            }, attemptFailed, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesRecentStickersBase>> GetRecentStickersAsync(bool attached, int hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesRecentStickersBase>>();
            GetRecentStickersAsync(attached, hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesRecentStickersBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesRecentStickersBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesAffectedMessages>> ReadMessageContentsAsync(TLVector<int> id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedMessages>>();
            ReadMessageContentsAsync(id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> SetStickersAsync(TLInputChannelBase inputChannel, TLInputStickerSetBase stickerset)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            SetStickersAsync(inputChannel, stickerset, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ReadMessageContentsAsync(TLInputChannelBase inputChannel, TLVector<int> id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ReadMessageContentsAsync(inputChannel, id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> JoinChannelAsync(TLChannel channel)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            JoinChannelAsync(channel, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesBotCallbackAnswer>> GetBotCallbackAnswerAsync(TLInputPeerBase peer, int messageId, byte[] data, bool game)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesBotCallbackAnswer>>();
            GetBotCallbackAnswerAsync(peer, messageId, data, game, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesBotCallbackAnswer>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesBotCallbackAnswer>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesAffectedMessages>> DeleteMessagesAsync(TLVector<int> id, bool revoke)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedMessages>>();
            DeleteMessagesAsync(id, revoke, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLHelpTermsOfService>> GetTermsOfServiceAsync(string langCode)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLHelpTermsOfService>>();
            GetTermsOfServiceAsync(langCode, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLHelpTermsOfService>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLHelpTermsOfService>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLChannelsChannelParticipant>> GetParticipantAsync(TLInputChannelBase inputChannel, TLInputUserBase userId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLChannelsChannelParticipant>>();
            GetParticipantAsync(inputChannel, userId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLChannelsChannelParticipant>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLChannelsChannelParticipant>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesMessagesBase>> GetMessagesAsync(TLInputChannelBase inputChannel, TLVector<int> id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
            GetMessagesAsync(inputChannel, id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> AddChatUserAsync(int chatId, TLInputUserBase userId, int fwdLimit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            AddChatUserAsync(chatId, userId, fwdLimit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> ForwardMessagesAsync(TLInputPeerBase toPeer, TLInputPeerBase fromPeer, TLVector<int> id, IList<TLMessage> messages, bool withMyScore, bool grouped)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            ForwardMessagesAsync(toPeer, fromPeer, id, messages, withMyScore, grouped, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ReorderStickerSetsAsync(bool masks, TLVector<long> order)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ReorderStickerSetsAsync(masks, order, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessage>> SendInlineBotResultAsync(TLMessage message, Action fastCallback)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessage>>();
            SendInlineBotResultAsync(message, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessage>(callback));
            }, fastCallback, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessage>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> GetAllDraftsAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            GetAllDraftsAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAccountPrivacyRules>> GetPrivacyAsync(TLInputPrivacyKeyBase key)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountPrivacyRules>>();
            GetPrivacyAsync(key, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountPrivacyRules>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountPrivacyRules>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLNearestDC>> GetNearestDCAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLNearestDC>>();
            GetNearestDCAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLNearestDC>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLNearestDC>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesAffectedMessages>> ReadHistoryAsync(TLInputPeerBase peer, int maxId, int offset)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedMessages>>();
            ReadHistoryAsync(peer, maxId, offset, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAccountPasswordSettings>> GetPasswordSettingsAsync(byte[] currentPasswordHash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountPasswordSettings>>();
            GetPasswordSettingsAsync(currentPasswordHash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountPasswordSettings>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountPasswordSettings>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesAffectedHistory>> DeleteUserHistoryAsync(TLChannel channel, TLInputUserBase userId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedHistory>>();
            DeleteUserHistoryAsync(channel, userId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedHistory>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedHistory>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLExportedMessageLink>> ExportMessageLinkAsync(TLInputChannelBase channel, int id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLExportedMessageLink>>();
            ExportMessageLinkAsync(channel, id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLExportedMessageLink>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLExportedMessageLink>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> EditAdminAsync(TLChannel channel, TLInputUserBase userId, TLChannelAdminRights rights)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            EditAdminAsync(channel, userId, rights, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPeerSettings>> GetPeerSettingsAsync(TLInputPeerBase peer)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPeerSettings>>();
            GetPeerSettingsAsync(peer, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPeerSettings>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPeerSettings>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesStickerSet>> GetStickerSetAsync(TLInputStickerSetBase stickerset)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesStickerSet>>();
            GetStickerSetAsync(stickerset, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesStickerSet>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesStickerSet>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> SaveGifAsync(TLInputDocumentBase id, bool unsave)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            SaveGifAsync(id, unsave, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLHelpSupport>> GetSupportAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLHelpSupport>>();
            GetSupportAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLHelpSupport>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLHelpSupport>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesDHConfig>> GetDHConfigAsync(int version, int randomLength)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesDHConfig>>();
            GetDHConfigAsync(version, randomLength, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesDHConfig>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesDHConfig>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ResetNotifySettingsAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ResetNotifySettingsAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> UnblockAsync(TLInputUserBase id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            UnblockAsync(id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> SetTypingAsync(TLInputPeerBase peer, TLSendMessageActionBase action)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            SetTypingAsync(peer, action, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesDifferenceBase>> GetDifferenceWithoutUpdatesAsync(int pts, int date, int qts)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesDifferenceBase>>();
            GetDifferenceWithoutUpdatesAsync(pts, date, qts, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesDifferenceBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesDifferenceBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> UpdatePasswordSettingsAsync(byte[] currentPasswordHash, TLAccountPasswordInputSettings newSettings)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            UpdatePasswordSettingsAsync(currentPasswordHash, newSettings, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ReadHistoryAsync(TLChannel channel, int maxId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ReadHistoryAsync(channel, maxId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLContactsTopPeersBase>> GetTopPeersAsync(TLContactsGetTopPeers.Flag flags, int offset, int limit, int hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsTopPeersBase>>();
            GetTopPeersAsync(flags, offset, limit, hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsTopPeersBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsTopPeersBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> EditChatTitleAsync(int chatId, string title)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            EditChatTitleAsync(chatId, title, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> CheckUsernameAsync(string username)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            CheckUsernameAsync(username, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ResetAuthorizationsAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ResetAuthorizationsAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPhotosPhotosBase>> GetUserPhotosAsync(TLInputUserBase userId, int offset, long maxId, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPhotosPhotosBase>>();
            GetUserPhotosAsync(userId, offset, maxId, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPhotosPhotosBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPhotosPhotosBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPhotosPhoto>> UploadProfilePhotoAsync(TLInputFile file)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPhotosPhoto>>();
            UploadProfilePhotoAsync(file, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPhotosPhoto>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPhotosPhoto>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> UpdateNotifySettingsAsync(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            UpdateNotifySettingsAsync(peer, settings, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUploadFileBase>> GetFileAsync(int dcId, TLInputFileLocationBase location, int offset, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUploadFileBase>>();
            GetFileAsync(dcId, location, offset, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUploadFileBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUploadFileBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUserBase>> UpdateProfileAsync(string firstName, string lastName, string about)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUserBase>>();
            UpdateProfileAsync(firstName, lastName, about, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLContactsImportedContacts>> ImportContactsAsync(TLVector<TLInputContactBase> contacts)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsImportedContacts>>();
            ImportContactsAsync(contacts, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsImportedContacts>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsImportedContacts>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> SetTypingAsync(TLInputPeerBase peer, bool typing)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            SetTypingAsync(peer, typing, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> RegisterDeviceAsync(int tokenType, string token)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            RegisterDeviceAsync(tokenType, token, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> LogOutAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            LogOutAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> ToggleSignaturesAsync(TLInputChannelBase channel, bool enabled)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            ToggleSignaturesAsync(channel, enabled, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLChannelsChannelParticipantsBase>> GetParticipantsAsync(TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, int offset, int limit, int hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLChannelsChannelParticipantsBase>>();
            GetParticipantsAsync(inputChannel, filter, offset, limit, hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLChannelsChannelParticipantsBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLChannelsChannelParticipantsBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> DeleteChatUserAsync(int chatId, TLInputUserBase userId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            DeleteChatUserAsync(chatId, userId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> ForwardMessageAsync(TLInputPeerBase peer, int fwdMessageId, TLMessage message)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            ForwardMessageAsync(peer, fwdMessageId, message, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesMessagesBase>> SearchGlobalAsync(string query, int offsetDate, TLInputPeerBase offsetPeer, int offsetId, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
            SearchGlobalAsync(query, offsetDate, offsetPeer, offsetId, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesFoundGifs>> SearchGifsAsync(string q, int offset)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesFoundGifs>>();
            SearchGifsAsync(q, offset, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesFoundGifs>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesFoundGifs>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesBotResults>> GetInlineBotResultsAsync(TLInputUserBase bot, TLInputPeerBase peer, TLInputGeoPointBase geoPoint, string query, string offset)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesBotResults>>();
            GetInlineBotResultsAsync(bot, peer, geoPoint, query, offset, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesBotResults>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesBotResults>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesFeaturedStickersBase>> GetFeaturedStickersAsync(int hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesFeaturedStickersBase>>();
            GetFeaturedStickersAsync(hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesFeaturedStickersBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesFeaturedStickersBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLVector<TLUserBase>>> GetUsersAsync(TLVector<TLInputUserBase> id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<TLUserBase>>>();
            GetUsersAsync(id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLUserBase>>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLUserBase>>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> UnregisterDeviceAsync(int tokenType, string token)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            UnregisterDeviceAsync(tokenType, token, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ConfirmPhoneAsync(string phoneCodeHash, string phoneCode)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ConfirmPhoneAsync(phoneCodeHash, phoneCode, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> UpdateUsernameAsync(TLInputChannelBase channel, string username)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            UpdateUsernameAsync(channel, username, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesStickerSetInstallResultBase>> InstallStickerSetAsync(TLInputStickerSetBase stickerset, bool archived)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesStickerSetInstallResultBase>>();
            InstallStickerSetAsync(stickerset, archived, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesStickerSetInstallResultBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesStickerSetInstallResultBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLChatInviteBase>> CheckChatInviteAsync(string hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLChatInviteBase>>();
            CheckChatInviteAsync(hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLChatInviteBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLChatInviteBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLDocumentBase>> GetDocumentByHashAsync(byte[] sha256, int size, string mimeType)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLDocumentBase>>();
            GetDocumentByHashAsync(sha256, size, mimeType, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLDocumentBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLDocumentBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> SaveDraftAsync(TLInputPeerBase peer, TLDraftMessageBase draft)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            SaveDraftAsync(peer, draft, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUserProfilePhotoBase>> UpdateProfilePhotoAsync(TLInputPhotoBase id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUserProfilePhotoBase>>();
            UpdateProfilePhotoAsync(id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserProfilePhotoBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserProfilePhotoBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLVector<long>>> DeletePhotosAsync(TLVector<TLInputPhotoBase> id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<long>>>();
            DeletePhotosAsync(id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<long>>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<long>>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLContactsBlockedBase>> GetBlockedAsync(int offset, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsBlockedBase>>();
            GetBlockedAsync(offset, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsBlockedBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsBlockedBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLContactsContactsBase>> GetContactsAsync(int hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsContactsBase>>();
            GetContactsAsync(hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsContactsBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsContactsBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUserFull>> GetFullUserAsync(TLInputUserBase id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUserFull>>();
            GetFullUserAsync(id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserFull>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserFull>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesDifferenceBase>> GetDifferenceAsync(int pts, int date, int qts)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesDifferenceBase>>();
            GetDifferenceAsync(pts, date, qts, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesDifferenceBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesDifferenceBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthAuthorization>> CheckPasswordAsync(byte[] passwordHash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthAuthorization>>();
            CheckPasswordAsync(passwordHash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ResetTopPeerRatingAsync(TLTopPeerCategoryBase category, TLInputPeerBase peer)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ResetTopPeerRatingAsync(category, peer, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessageMediaBase>> GetWebPagePreviewAsync(string message)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessageMediaBase>>();
            GetWebPagePreviewAsync(message, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessageMediaBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessageMediaBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLWebPageBase>> GetWebPageAsync(string url, int hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLWebPageBase>>();
            GetWebPageAsync(url, hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLWebPageBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLWebPageBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> EditChatPhotoAsync(int chatId, TLInputChatPhotoBase photo)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            EditChatPhotoAsync(chatId, photo, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUserBase>> UpdateUsernameAsync(string username)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUserBase>>();
            UpdateUsernameAsync(username, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthSentCode>> SendConfirmPhoneCodeAsync(string hash, bool currentNumber)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
            SendConfirmPhoneCodeAsync(hash, currentNumber, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> EditAboutAsync(TLChannel channel, string about)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            EditAboutAsync(channel, about, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ClearRecentStickersAsync(bool attached)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ClearRecentStickersAsync(attached, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> HideReportSpamAsync(TLInputPeerBase peer)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            HideReportSpamAsync(peer, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> ImportChatInviteAsync(string hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            ImportChatInviteAsync(hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthSentCode>> SendChangePhoneCodeAsync(string phoneNumber, bool? currentNumber)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
            SendChangePhoneCodeAsync(phoneNumber, currentNumber, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAccountPrivacyRules>> SetPrivacyAsync(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountPrivacyRules>>();
            SetPrivacyAsync(key, rules, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountPrivacyRules>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountPrivacyRules>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> SetAccountTTLAsync(TLAccountDaysTTL ttl)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            SetAccountTTLAsync(ttl, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLContactsFound>> SearchAsync(string q, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsFound>>();
            SearchAsync(q, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsFound>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsFound>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesChatFull>> GetFullChatAsync(int chatId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesChatFull>>();
            GetFullChatAsync(chatId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesChatFull>> UpdateChannelAsync(int? channelId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesChatFull>>();
            UpdateChannelAsync(channelId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLExportedChatInviteBase>> ExportChatInviteAsync(int chatId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLExportedChatInviteBase>>();
            ExportChatInviteAsync(chatId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLExportedChatInviteBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLExportedChatInviteBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ReportSpamAsync(TLInputPeerBase peer)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ReportSpamAsync(peer, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesState>> GetStateAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesState>>();
            GetStateAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesState>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesState>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> GetAppChangelogAsync(string prevAppVersion)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            GetAppChangelogAsync(prevAppVersion, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthPasswordRecovery>> RequestPasswordRecoveryAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthPasswordRecovery>>();
            RequestPasswordRecoveryAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthPasswordRecovery>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthPasswordRecovery>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAccountPasswordBase>> GetPasswordAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountPasswordBase>>();
            GetPasswordAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountPasswordBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountPasswordBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> UpdatePinnedMessageAsync(bool silent, TLInputChannelBase channel, int id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            UpdatePinnedMessageAsync(silent, channel, id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> EditPhotoAsync(TLChannel channel, TLInputChatPhotoBase photo)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            EditPhotoAsync(channel, photo, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> EditBannedAsync(TLChannel channel, TLInputUserBase userId, TLChannelBannedRights rights)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            EditBannedAsync(channel, userId, rights, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessage>> SendMessageAsync(TLMessage message, Action fastCallback)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessage>>();
            SendMessageAsync(message, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessage>(callback));
            }, fastCallback, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessage>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesMessagesBase>> GetHistoryAsync(TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int offset, int offsetDate, int maxId, int limit, int hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
            GetHistoryAsync(inputPeer, peer, sync, offset, offsetDate, maxId, limit, hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ResetAuthorizationAsync(long hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ResetAuthorizationAsync(hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> MigrateChatAsync(int chatId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            MigrateChatAsync(chatId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> EditMessageAsync(TLInputPeerBase peer, int id, string message, TLVector<TLMessageEntityBase> entities, TLReplyMarkupBase replyMarkup, TLInputGeoPointBase geoPoint, bool noWebPage, bool stop)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            EditMessageAsync(peer, id, message, entities, replyMarkup, geoPoint, noWebPage, stop, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesAffectedMessages>> DeleteMessagesAsync(TLInputChannelBase channel, TLVector<int> id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedMessages>>();
            DeleteMessagesAsync(channel, id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> CreateChannelAsync(TLChannelsCreateChannel.Flag flags, string title, string about)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            CreateChannelAsync(flags, title, about, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesMessagesBase>> GetMessagesAsync(TLVector<int> id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
            GetMessagesAsync(id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> CancelCodeAsync(string phoneNumber, string phoneCodeHash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            CancelCodeAsync(phoneNumber, phoneCodeHash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> EditTitleAsync(TLChannel channel, string title)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            EditTitleAsync(channel, title, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> UninstallStickerSetAsync(TLInputStickerSetBase stickerset)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            UninstallStickerSetAsync(stickerset, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> CreateChatAsync(TLVector<TLInputUserBase> users, string title)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            CreateChatAsync(users, title, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> StartBotAsync(TLInputUserBase bot, string startParam, TLMessage message)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            StartBotAsync(bot, startParam, message, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesAffectedHistory>> DeleteHistoryAsync(bool justClear, TLInputPeerBase peer, int maxId)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedHistory>>();
            DeleteHistoryAsync(justClear, peer, maxId, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedHistory>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedHistory>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAccountAuthorizations>> GetAuthorizationsAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountAuthorizations>>();
            GetAuthorizationsAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountAuthorizations>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountAuthorizations>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> EditChatAdminAsync(int chatId, TLInputUserBase userId, bool isAdmin)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            EditChatAdminAsync(chatId, userId, isAdmin, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> InviteToChannelAsync(TLInputChannelBase channel, TLVector<TLInputUserBase> users)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            InviteToChannelAsync(channel, users, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesArchivedStickers>> GetArchivedStickersAsync(long offsetId, int limit, bool masks)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesArchivedStickers>>();
            GetArchivedStickersAsync(offsetId, limit, masks, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesArchivedStickers>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesArchivedStickers>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> UpdateDeviceLockedAsync(int period)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            UpdateDeviceLockedAsync(period, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLContactsLink>> DeleteContactAsync(TLInputUserBase id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsLink>>();
            DeleteContactAsync(id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsLink>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsLink>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesDialogsBase>> GetDialogsAsync(int offsetDate, int offsetId, TLInputPeerBase offsetPeer, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesDialogsBase>>();
            GetDialogsAsync(offsetDate, offsetId, offsetPeer, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesDialogsBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesDialogsBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ReportPeerAsync(TLInputPeerBase peer, TLReportReasonBase reason)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ReportPeerAsync(peer, reason, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ReportSpamAsync(TLInputChannelBase channel, TLInputUserBase userId, TLVector<int> id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ReportSpamAsync(channel, userId, id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> ToggleInvitesAsync(TLInputChannelBase channel, bool enabled)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            ToggleInvitesAsync(channel, enabled, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLExportedChatInviteBase>> ExportInviteAsync(TLInputChannelBase channel)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLExportedChatInviteBase>>();
            ExportInviteAsync(channel, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLExportedChatInviteBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLExportedChatInviteBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> SaveBigFilePartAsync(long fileId, int filePart, int fileTotalParts, byte[] bytes)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            SaveBigFilePartAsync(fileId, filePart, fileTotalParts, bytes, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> UpdateStatusAsync(bool offline)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            UpdateStatusAsync(offline, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> BlockAsync(TLInputUserBase id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            BlockAsync(id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthAuthorization>> SignUpAsync(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthAuthorization>>();
            SignUpAsync(phoneNumber, phoneCodeHash, phoneCode, firstName, lastName, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> ToggleChatAdminsAsync(int chatId, bool enabled)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            ToggleChatAdminsAsync(chatId, enabled, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesMessageEditData>> GetMessageEditDataAsync(TLInputPeerBase peer, int id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessageEditData>>();
            GetMessageEditDataAsync(peer, id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessageEditData>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessageEditData>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> CheckUsernameAsync(TLInputChannelBase channel, string username)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            CheckUsernameAsync(channel, username, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesChatFull>> GetFullChannelAsync(TLInputChannelBase channel)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesChatFull>>();
            GetFullChannelAsync(channel, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> DeleteChannelAsync(TLChannel channel)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            DeleteChannelAsync(channel, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesPeerDialogs>> GetPeerDialogsAsync(TLVector<TLInputPeerBase> peers)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesPeerDialogs>>();
            GetPeerDialogsAsync(peers, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesPeerDialogs>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesPeerDialogs>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> SendMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            SendMediaAsync(inputPeer, inputMedia, message, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesSavedGifsBase>> GetSavedGifsAsync(int hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesSavedGifsBase>>();
            GetSavedGifsAsync(hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesSavedGifsBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesSavedGifsBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> SetInlineBotResultsAsync(bool gallery, bool pr, long queryId, TLVector<TLInputBotInlineResultBase> results, int cacheTime, string nextOffset, TLInlineBotSwitchPM switchPM)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            SetInlineBotResultsAsync(gallery, pr, queryId, results, cacheTime, nextOffset, switchPM, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> ReadFeaturedStickersAsync(TLVector<long> id)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            ReadFeaturedStickersAsync(id, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesAllStickersBase>> GetAllStickersAsync(int hash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAllStickersBase>>();
            GetAllStickersAsync(hash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAllStickersBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesAllStickersBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLVector<TLWallPaperBase>>> GetWallpapersAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<TLWallPaperBase>>>();
            GetWallpapersAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLWallPaperBase>>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLWallPaperBase>>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLContactsResolvedPeer>> ResolveUsernameAsync(string username)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsResolvedPeer>>();
            ResolveUsernameAsync(username, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsResolvedPeer>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLContactsResolvedPeer>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAccountDaysTTL>> GetAccountTTLAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountDaysTTL>>();
            GetAccountTTLAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountDaysTTL>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAccountDaysTTL>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesChatsBase>> GetAdminedPublicChannelsAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesChatsBase>>();
            GetAdminedPublicChannelsAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesChatsBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesChatsBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUploadFileBase>> GetFileAsync(TLInputFileLocationBase location, int offset, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUploadFileBase>>();
            GetFileAsync(location, offset, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUploadFileBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUploadFileBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLMessagesMessagesBase>> SearchAsync(TLInputPeerBase peer, string query, TLInputUserBase from, TLMessagesFilterBase filter, int minDate, int maxDate, int offset, int maxId, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
            SearchAsync(peer, query, from, filter, minDate, maxDate, offset, maxId, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> DeleteAccountAsync(string reason)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            DeleteAccountAsync(reason, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesChannelDifferenceBase>> GetChannelDifferenceAsync(TLInputChannelBase inputChannel, TLChannelMessagesFilterBase filter, int pts, int limit)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesChannelDifferenceBase>>();
            GetChannelDifferenceAsync(inputChannel, filter, pts, limit, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesChannelDifferenceBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesChannelDifferenceBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUpdatesBase>> LeaveChannelAsync(TLChannel channel)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
            LeaveChannelAsync(channel, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLUserBase>> ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLUserBase>>();
            ChangePhoneAsync(phoneNumber, phoneCodeHash, phoneCode, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLUserBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLVector<TLContactStatus>>> GetStatusesAsync()
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<TLContactStatus>>>();
            GetStatusesAsync((callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLContactStatus>>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLVector<TLContactStatus>>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> DeleteAccountTTLAsync(string reason)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            DeleteAccountTTLAsync(reason, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLPeerNotifySettingsBase>> GetNotifySettingsAsync(TLInputNotifyPeerBase peer)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLPeerNotifySettingsBase>>();
            GetNotifySettingsAsync(peer, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPeerNotifySettingsBase>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLPeerNotifySettingsBase>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<bool>> SaveFilePartAsync(long fileId, int filePart, byte[] bytes)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
            SaveFilePartAsync(fileId, filePart, bytes, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthAuthorization>> SignInAsync(string phoneNumber, string phoneCodeHash, string phoneCode)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthAuthorization>>();
            SignInAsync(phoneNumber, phoneCodeHash, phoneCode, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthAuthorization>> RecoverPasswordAsync(string code)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthAuthorization>>();
            RecoverPasswordAsync(code, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(faultCallback));
            });
            return tsc.Task;
        }

        [DebuggerStepThrough]
        public Task<MTProtoResponse<TLAuthSentCode>> ResendCodeAsync(string phoneNumber, string phoneCodeHash)
        {
            var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
            ResendCodeAsync(phoneNumber, phoneCodeHash, (callback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
            }, (faultCallback) =>
            {
                tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
            });
            return tsc.Task;
        }





        //public Task<MTProtoResponse<bool>> ResetNotifySettingsAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ResetNotifySettingsCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> EditChatAdminAsync(int chatId, TLInputUserBase userId, bool isAdmin)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    EditChatAdminCallback(chatId, userId, isAdmin, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> ReportSpamAsync(TLInputPeerBase peer)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ReportSpamCallback(peer, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesAffectedMessages>> DeleteMessagesAsync(TLVector<int> id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedMessages>>();
        //    DeleteMessagesCallback(id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUserBase>> UpdateUsernameAsync(string username)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUserBase>>();
        //    UpdateUsernameCallback(username, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUserBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUserBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> InviteToChannelAsync(TLInputChannelBase channel, TLVector<TLInputUserBase> users)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    InviteToChannelCallback(channel, users, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> EditTitleAsync(TLChannel channel, string title)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    EditTitleCallback(channel, title, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesChatFull>> UpdateChannelAsync(int? channelId)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesChatFull>>();
        //    UpdateChannelCallback(channelId, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLContactsTopPeersBase>> GetTopPeersAsync(TLContactsGetTopPeers.Flag flags, int offset, int limit, int hash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsTopPeersBase>>();
        //    GetTopPeersCallback(flags, offset, limit, hash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsTopPeersBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsTopPeersBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLPeerSettings>> GetPeerSettingsAsync(TLInputPeerBase peer)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLPeerSettings>>();
        //    GetPeerSettingsCallback(peer, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPeerSettings>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPeerSettings>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> ImportChatInviteAsync(string hash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    ImportChatInviteCallback(hash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> DeleteChatUserAsync(int chatId, TLInputUserBase userId)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    DeleteChatUserCallback(chatId, userId, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> ForwardMessageAsync(TLInputPeerBase peer, int fwdMessageId, TLMessage message)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    ForwardMessageCallback(peer, fwdMessageId, message, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesMessagesBase>> SearchGlobalAsync(string query, int offsetDate, TLInputPeerBase offsetPeer, int offsetId, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
        //    SearchGlobalCallback(query, offsetDate, offsetPeer, offsetId, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> SetInlineBotResultsAsync(bool gallery, bool pr, long queryId, TLVector<TLInputBotInlineResultBase> results, int cacheTime, string nextOffset, TLInlineBotSwitchPM switchPM)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    SetInlineBotResultsCallback(gallery, pr, queryId, results, cacheTime, nextOffset, switchPM, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> ReadFeaturedStickersAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ReadFeaturedStickersCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesAllStickersBase>> GetAllStickersAsync(byte[] hash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAllStickersBase>>();
        //    GetAllStickersCallback(hash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesAllStickersBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesAllStickersBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAccountPrivacyRules>> GetPrivacyAsync(TLInputPrivacyKeyBase key)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountPrivacyRules>>();
        //    GetPrivacyCallback(key, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountPrivacyRules>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountPrivacyRules>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLPong>> PingAsync(long pingId)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLPong>>();
        //    PingCallback(pingId, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPong>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPong>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLServerDHInnerData>> GetDHConfigAsync(int version, int randomLength)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLServerDHInnerData>>();
        //    GetDHConfigCallback(version, randomLength, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLServerDHInnerData>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLServerDHInnerData>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> UpdateNotifySettingsAsync(TLInputNotifyPeerBase peer, TLInputPeerNotifySettings settings)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    UpdateNotifySettingsCallback(peer, settings, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUploadFile>> GetFileAsync(TLInputFileLocationBase location, int offset, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUploadFile>>();
        //    GetFileCallback(location, offset, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUploadFile>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUploadFile>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> BlockAsync(TLInputUserBase id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    BlockCallback(id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> SetTypingAsync(TLInputPeerBase peer, bool typing)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    SetTypingCallback(peer, typing, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLVector<TLUserBase>>> GetUsersAsync(TLVector<TLInputUserBase> id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<TLUserBase>>>();
        //    GetUsersCallback(id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLVector<TLUserBase>>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLVector<TLUserBase>>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesAffectedMessages>> ReadMessageContentsAsync(TLVector<int> id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedMessages>>();
        //    ReadMessageContentsCallback(id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedMessages>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesAffectedHistory>> DeleteHistoryAsync(bool justClear, TLInputPeerBase peer, int offset)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedHistory>>();
        //    DeleteHistoryCallback(justClear, peer, offset, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedHistory>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedHistory>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesDialogsBase>> GetDialogsAsync(int offsetDate, int offsetId, TLInputPeerBase offsetPeer, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesDialogsBase>>();
        //    GetDialogsCallback(offsetDate, offsetId, offsetPeer, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesDialogsBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesDialogsBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAuthAuthorization>> SignUpAsync(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthAuthorization>>();
        //    SignUpCallback(phoneNumber, phoneCodeHash, phoneCode, firstName, lastName, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAuthAuthorization>> SignInAsync(string phoneNumber, string phoneCodeHash, string phoneCode)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthAuthorization>>();
        //    SignInCallback(phoneNumber, phoneCodeHash, phoneCode, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesDifferenceBase>> GetDifferenceAsync(int pts, int date, int qts)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesDifferenceBase>>();
        //    GetDifferenceCallback(pts, date, qts, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesDifferenceBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesDifferenceBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> MigrateChatAsync(int chatId)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    MigrateChatCallback(chatId, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesAffectedHistory>> DeleteUserHistoryAsync(TLChannel channel, TLInputUserBase userId)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesAffectedHistory>>();
        //    DeleteUserHistoryCallback(channel, userId, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedHistory>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesAffectedHistory>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> ToggleInvitesAsync(TLInputChannelBase channel, bool enabled)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    ToggleInvitesCallback(channel, enabled, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesChatFull>> GetFullChannelAsync(TLInputChannelBase channel)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesChatFull>>();
        //    GetFullChannelCallback(channel, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> EditAboutAsync(TLChannel channel, string about)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    EditAboutCallback(channel, about, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesMessagesBase>> GetMessagesAsync(TLVector<int> id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
        //    GetMessagesCallback(id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLVector<TLStickerSetCovered>>> GetUnusedStickersAsync(int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<TLStickerSetCovered>>>();
        //    GetUnusedStickersCallback(limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLVector<TLStickerSetCovered>>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLVector<TLStickerSetCovered>>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> HideReportSpamAsync(TLInputPeerBase peer)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    HideReportSpamCallback(peer, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLChatInviteBase>> CheckChatInviteAsync(string hash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLChatInviteBase>>();
        //    CheckChatInviteCallback(hash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLChatInviteBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLChatInviteBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> EditChatPhotoAsync(int chatId, TLInputChatPhotoBase photo)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    EditChatPhotoCallback(chatId, photo, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> SendMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    SendMediaCallback(inputPeer, inputMedia, message, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> SaveGifAsync(TLInputDocumentBase id, bool unsave)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    SaveGifCallback(id, unsave, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> SaveDraftAsync(TLInputPeerBase peer, TLDraftMessageBase draft)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    SaveDraftCallback(peer, draft, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAuthSentCode>> SendChangePhoneCodeAsync(string phoneNumber, bool? currentNumber)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
        //    SendChangePhoneCodeCallback(phoneNumber, currentNumber, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> SetAccountTTLAsync(TLAccountDaysTTL ttl)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    SetAccountTTLCallback(ttl, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLPong>> PingDelayDisconnectAsync(long pingId, int disconnectDelay)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLPong>>();
        //    PingDelayDisconnectCallback(pingId, disconnectDelay, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPong>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPong>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLPhotosPhotosBase>> GetUserPhotosAsync(TLInputUserBase userId, int offset, long maxId, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLPhotosPhotosBase>>();
        //    GetUserPhotosCallback(userId, offset, maxId, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPhotosPhotosBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPhotosPhotosBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLPeerNotifySettingsBase>> GetNotifySettingsAsync(TLInputNotifyPeerBase peer)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLPeerNotifySettingsBase>>();
        //    GetNotifySettingsCallback(peer, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPeerNotifySettingsBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPeerNotifySettingsBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> UpdateStatusAsync(bool offline)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    UpdateStatusCallback(offline, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> UnblockAsync(TLInputUserBase id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    UnblockCallback(id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLContactsContactsBase>> GetContactsAsync(string hash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsContactsBase>>();
        //    GetContactsCallback(hash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsContactsBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsContactsBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> ReadHistoryAsync(TLChannel channel, int maxId)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ReadHistoryCallback(channel, maxId, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> UnregisterDeviceAsync(int tokenType, string token)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    UnregisterDeviceCallback(tokenType, token, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> DeleteAccountAsync(string reason)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    DeleteAccountCallback(reason, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //// TODO: Layer 56 
        ////public Task<MTProtoResponse<TLMessagesChats>> GetAdminedPublicChannelsAsync()
        ////{
        ////    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesChats>>();
        ////    GetAdminedPublicChannelsCallback((callback) =>
        ////    {
        ////        tsc.TrySetResult(new MTProtoResponse<TLMessagesChats>(callback));
        ////    }, (faultCallback) =>
        ////    {
        ////        tsc.TrySetResult(new MTProtoResponse<TLMessagesChats>(faultCallback));
        ////    });
        ////    return tsc.Task;
        ////}

        //public Task<MTProtoResponse<TLUpdatesBase>> ToggleSignaturesAsync(TLInputChannelBase channel, bool enabled)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    ToggleSignaturesCallback(channel, enabled, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //// TODO: Layer 56 
        ////public Task<MTProtoResponse<TLMessagesMessagesBase>> GetImportantHistoryAsync(TLInputChannelBase channel, TLPeerBase peer, bool sync, int? offsetId, int? addOffset, int? limit, int? maxId, int? minId)
        ////{
        ////    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
        ////    GetImportantHistoryCallback(channel, peer, sync, offsetId, addOffset, limit, maxId, minId, (callback) =>
        ////    {
        ////        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
        ////    }, (faultCallback) =>
        ////    {
        ////        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
        ////    });
        ////    return tsc.Task;
        ////}

        //public Task<MTProtoResponse<TLUpdatesBase>> CreateChannelAsync(TLChannelsCreateChannel.Flag flags, string title, string about)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    CreateChannelCallback(flags, title, about, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLChannelsChannelParticipants>> GetParticipantsAsync(TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, int offset, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLChannelsChannelParticipants>>();
        //    GetParticipantsCallback(inputChannel, filter, offset, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLChannelsChannelParticipants>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLChannelsChannelParticipants>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesMessagesBase>> GetChannelHistoryAsync(string debugInfo, TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int offset, int maxId, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
        //    GetChannelHistoryCallback(debugInfo, inputPeer, peer, sync, offset, maxId, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> ClearRecentStickersAsync(bool attached)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ClearRecentStickersCallback(attached, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> UninstallStickerSetAsync(TLInputStickerSetBase stickerset)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    UninstallStickerSetCallback(stickerset, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLExportedChatInviteBase>> ExportChatInviteAsync(int chatId)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLExportedChatInviteBase>>();
        //    ExportChatInviteCallback(chatId, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLExportedChatInviteBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLExportedChatInviteBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> EditChatTitleAsync(int chatId, string title)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    EditChatTitleCallback(chatId, title, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesSavedGifsBase>> GetSavedGifsAsync(int hash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesSavedGifsBase>>();
        //    GetSavedGifsCallback(hash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesSavedGifsBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesSavedGifsBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> GetAllDraftsAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    GetAllDraftsCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> UpdateDeviceLockedAsync(int period)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    UpdateDeviceLockedCallback(period, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLContactsResolvedPeer>> ResolveUsernameAsync(string username)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsResolvedPeer>>();
        //    ResolveUsernameCallback(username, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsResolvedPeer>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsResolvedPeer>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAccountDaysTTL>> GetAccountTTLAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountDaysTTL>>();
        //    GetAccountTTLCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountDaysTTL>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountDaysTTL>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> ResetAuthorizationsAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ResetAuthorizationsCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLPhotoBase>> UpdateProfilePhotoAsync(TLInputPhotoBase id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLPhotoBase>>();
        //    UpdateProfilePhotoCallback(id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPhotoBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPhotoBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> SaveBigFilePartAsync(long fileId, int filePart, int fileTotalParts, byte[] bytes)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    SaveBigFilePartCallback(fileId, filePart, fileTotalParts, bytes, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUserBase>> UpdateProfileAsync(string firstName, string lastName, string about)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUserBase>>();
        //    UpdateProfileCallback(firstName, lastName, about, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUserBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUserBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLContactsImportedContacts>> ImportContactsAsync(TLVector<TLInputContactBase> contacts, bool replace)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsImportedContacts>>();
        //    ImportContactsCallback(contacts, replace, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsImportedContacts>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsImportedContacts>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUserFull>> GetFullUserAsync(TLInputUserBase id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUserFull>>();
        //    GetFullUserCallback(id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUserFull>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUserFull>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLContactsLink>> DeleteContactAsync(TLInputUserBase id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsLink>>();
        //    DeleteContactCallback(id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsLink>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsLink>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesMessagesBase>> GetHistoryAsync(TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int offset, int maxId, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessagesBase>>();
        //    GetHistoryCallback(inputPeer, peer, sync, offset, maxId, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessagesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLContactsFound>> SearchAsync(string q, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsFound>>();
        //    SearchCallback(q, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsFound>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsFound>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> LogOutAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    LogOutCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> CancelCodeAsync(string phoneNumber, string phoneCodeHash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    CancelCodeCallback(phoneNumber, phoneCodeHash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAuthSentCode>> ResendCodeAsync(string phoneNumber, string phoneCodeHash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
        //    ResendCodeCallback(phoneNumber, phoneCodeHash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAccountAuthorizations>> GetAuthorizationsAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountAuthorizations>>();
        //    GetAuthorizationsCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountAuthorizations>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountAuthorizations>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesChannelDifferenceBase>> GetChannelDifferenceAsync(TLInputChannelBase inputChannel, TLChannelMessagesFilterBase filter, int pts, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesChannelDifferenceBase>>();
        //    GetChannelDifferenceCallback(inputChannel, filter, pts, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesChannelDifferenceBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesChannelDifferenceBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesMessageEditData>> GetMessageEditDataAsync(TLInputPeerBase peer, int id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesMessageEditData>>();
        //    GetMessageEditDataCallback(peer, id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessageEditData>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesMessageEditData>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLExportedChatInviteBase>> ExportInviteAsync(TLInputChannelBase channel)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLExportedChatInviteBase>>();
        //    ExportInviteCallback(channel, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLExportedChatInviteBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLExportedChatInviteBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> EditPhotoAsync(TLChannel channel, TLInputChatPhotoBase photo)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    EditPhotoCallback(channel, photo, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> KickFromChannelAsync(TLChannel channel, TLInputUserBase userId, bool kicked)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    KickFromChannelCallback(channel, userId, kicked, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> ResetTopPeerRatingAsync(TLTopPeerCategoryBase category, TLInputPeerBase peer)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ResetTopPeerRatingCallback(category, peer, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesPeerDialogs>> GetPeerDialogsAsync(TLVector<TLInputPeerBase> peers)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesPeerDialogs>>();
        //    GetPeerDialogsCallback(peers, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesPeerDialogs>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesPeerDialogs>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesStickerSet>> GetStickerSetAsync(TLInputStickerSetBase stickerset)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesStickerSet>>();
        //    GetStickerSetCallback(stickerset, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesStickerSet>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesStickerSet>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> CreateChatAsync(TLVector<TLInputUserBase> users, string title)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    CreateChatCallback(users, title, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessage>> SendMessageAsync(TLMessage message, Action fastCallback)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessage>>();
        //    SendMessageCallback(message, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessage>(callback));
        //    }, fastCallback, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessage>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesFoundGifs>> SearchGifsAsync(string q, int offset)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesFoundGifs>>();
        //    SearchGifsCallback(q, offset, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesFoundGifs>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesFoundGifs>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesBotResults>> GetInlineBotResultsAsync(TLInputUserBase bot, TLInputPeerBase peer, TLInputGeoPointBase geoPoint, string query, string offset)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesBotResults>>();
        //    GetInlineBotResultsCallback(bot, peer, geoPoint, query, offset, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesBotResults>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesBotResults>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesFeaturedStickersBase>> GetFeaturedStickersAsync(bool full, int hash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesFeaturedStickersBase>>();
        //    GetFeaturedStickersCallback(full, hash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesFeaturedStickersBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesFeaturedStickersBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUserBase>> ChangePhoneAsync(string phoneNumber, string phoneCodeHash, string phoneCode)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUserBase>>();
        //    ChangePhoneCallback(phoneNumber, phoneCodeHash, phoneCode, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUserBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUserBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> DeleteAccountTTLAsync(string reason)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    DeleteAccountTTLCallback(reason, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLNearestDC>> GetNearestDCAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLNearestDC>>();
        //    GetNearestDCCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLNearestDC>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLNearestDC>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLContactsBlockedBase>> GetBlockedAsync(int offset, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLContactsBlockedBase>>();
        //    GetBlockedCallback(offset, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsBlockedBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLContactsBlockedBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesChatFull>> GetFullChatAsync(int chatId)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesChatFull>>();
        //    GetFullChatCallback(chatId, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesChatFull>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesDifferenceBase>> GetDifferenceWithoutUpdatesAsync(int pts, int date, int qts)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesDifferenceBase>>();
        //    GetDifferenceWithoutUpdatesCallback(pts, date, qts, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesDifferenceBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesDifferenceBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> ResetAuthorizationAsync(long hash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ResetAuthorizationCallback(hash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAccountPasswordBase>> GetPasswordAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountPasswordBase>>();
        //    GetPasswordCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountPasswordBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountPasswordBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAccountPasswordSettings>> GetPasswordSettingsAsync(byte[] currentPasswordHash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountPasswordSettings>>();
        //    GetPasswordSettingsCallback(currentPasswordHash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountPasswordSettings>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountPasswordSettings>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> UpdatePasswordSettingsAsync(byte[] currentPasswordHash, TLAccountPasswordInputSettings newSettings)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    UpdatePasswordSettingsCallback(currentPasswordHash, newSettings, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAuthAuthorization>> CheckPasswordAsync(byte[] passwordHash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthAuthorization>>();
        //    CheckPasswordCallback(passwordHash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAuthPasswordRecovery>> RequestPasswordRecoveryAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthPasswordRecovery>>();
        //    RequestPasswordRecoveryCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthPasswordRecovery>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthPasswordRecovery>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAuthAuthorization>> RecoverPasswordAsync(string code)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthAuthorization>>();
        //    RecoverPasswordCallback(code, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthAuthorization>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> ConfirmPhoneAsync(string phoneCodeHash, string phoneCode)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ConfirmPhoneCallback(phoneCodeHash, phoneCode, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAuthSentCode>> SendConfirmPhoneCodeAsync(string hash, bool currentNumber)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
        //    SendConfirmPhoneCodeCallback(hash, currentNumber, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLHelpAppChangelogBase>> GetAppChangelogAsync(string deviceModel, string systemVersion, string appVersion, string langCode)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLHelpAppChangelogBase>>();
        //    GetAppChangelogCallback(deviceModel, systemVersion, appVersion, langCode, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLHelpAppChangelogBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLHelpAppChangelogBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLHelpTermsOfService>> GetTermsOfServiceAsync(string langCode)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLHelpTermsOfService>>();
        //    GetTermsOfServiceCallback(langCode, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLHelpTermsOfService>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLHelpTermsOfService>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> UpdatePinnedMessageAsync(bool silent, TLInputChannelBase channel, int id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    UpdatePinnedMessageCallback(silent, channel, id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> CheckUsernameAsync(string username)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    CheckUsernameCallback(username, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> JoinChannelAsync(TLChannel channel)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    JoinChannelCallback(channel, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLChannelsChannelParticipant>> GetParticipantAsync(TLInputChannelBase inputChannel, TLInputUserBase userId)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLChannelsChannelParticipant>>();
        //    GetParticipantCallback(inputChannel, userId, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLChannelsChannelParticipant>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLChannelsChannelParticipant>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesBotCallbackAnswer>> GetBotCallbackAnswerAsync(TLInputPeerBase peer, int messageId, byte[] data, int gameId)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesBotCallbackAnswer>>();
        //    GetBotCallbackAnswerCallback(peer, messageId, data, gameId, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesBotCallbackAnswer>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesBotCallbackAnswer>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessage>> SendInlineBotResultAsync(TLMessage message, Action fastCallback)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessage>>();
        //    SendInlineBotResultCallback(message, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessage>(callback));
        //    }, fastCallback, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessage>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesArchivedStickers>> GetArchivedStickersAsync(bool full, long offsetId, int limit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesArchivedStickers>>();
        //    GetArchivedStickersCallback(full, offsetId, limit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesArchivedStickers>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesArchivedStickers>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLVector<TLWallPaperBase>>> GetWallpapersAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<TLWallPaperBase>>>();
        //    GetWallpapersCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLVector<TLWallPaperBase>>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLVector<TLWallPaperBase>>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> SaveFilePartAsync(long fileId, int filePart, byte[] bytes)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    SaveFilePartCallback(fileId, filePart, bytes, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> RegisterDeviceAsync(int tokenType, string token)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    RegisterDeviceCallback(tokenType, token, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> ReportPeerAsync(TLInputPeerBase peer, TLReportReasonBase reason)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ReportPeerCallback(peer, reason, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLExportedMessageLink>> ExportMessageLinkAsync(TLInputChannelBase channel, int id)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLExportedMessageLink>>();
        //    ExportMessageLinkCallback(channel, id, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLExportedMessageLink>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLExportedMessageLink>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> DeleteChannelAsync(TLChannel channel)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    DeleteChannelCallback(channel, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesRecentStickersBase>> GetRecentStickersAsync(bool attached, int hash)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesRecentStickersBase>>();
        //    GetRecentStickersCallback(attached, hash, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesRecentStickersBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesRecentStickersBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessagesStickerSetInstallResultBase>> InstallStickerSetAsync(TLInputStickerSetBase stickerset, bool archived)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessagesStickerSetInstallResultBase>>();
        //    InstallStickerSetCallback(stickerset, archived, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesStickerSetInstallResultBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessagesStickerSetInstallResultBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLMessageMediaBase>> GetWebPagePreviewAsync(string message)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLMessageMediaBase>>();
        //    GetWebPagePreviewCallback(message, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessageMediaBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLMessageMediaBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> ForwardMessagesAsync(TLInputPeerBase toPeer, TLVector<int> id, IList<TLMessage> messages, bool withMyScore)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    ForwardMessagesCallback(toPeer, id, messages, withMyScore, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLDocumentBase>> GetDocumentByHashAsync(byte[] sha256, int size, string mimeType)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLDocumentBase>>();
        //    GetDocumentByHashCallback(sha256, size, mimeType, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLDocumentBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLDocumentBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAccountPrivacyRules>> SetPrivacyAsync(TLInputPrivacyKeyBase key, TLVector<TLInputPrivacyRuleBase> rules)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAccountPrivacyRules>>();
        //    SetPrivacyCallback(key, rules, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountPrivacyRules>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAccountPrivacyRules>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLHelpSupport>> GetSupportAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLHelpSupport>>();
        //    GetSupportCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLHelpSupport>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLHelpSupport>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLPhotosPhoto>> UploadProfilePhotoAsync(TLInputFile file)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLPhotosPhoto>>();
        //    UploadProfilePhotoCallback(file, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPhotosPhoto>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLPhotosPhoto>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesState>> GetStateAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesState>>();
        //    GetStateCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesState>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesState>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> ToggleChatAdminsAsync(int chatId, bool enabled)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    ToggleChatAdminsCallback(chatId, enabled, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> EditMessageAsync(TLInputPeerBase peer, int id, string message, TLVector<TLMessageEntityBase> entities, TLReplyMarkupBase replyMarkup, bool noWebPage)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    EditMessageCallback(peer, id, message, entities, replyMarkup, noWebPage, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> LeaveChannelAsync(TLChannel channel)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    LeaveChannelCallback(channel, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> EditAdminAsync(TLChannel channel, TLInputUserBase userId, TLChannelParticipantRoleBase role)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    EditAdminCallback(channel, userId, role, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> AddChatUserAsync(int chatId, TLInputUserBase userId, int fwdLimit)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    AddChatUserCallback(chatId, userId, fwdLimit, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLUpdatesBase>> StartBotAsync(TLInputUserBase bot, string startParam, TLMessage message)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLUpdatesBase>>();
        //    StartBotCallback(bot, startParam, message, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLUpdatesBase>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<bool>> ReorderStickerSetsAsync(bool masks, TLVector<long> order)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<bool>>();
        //    ReorderStickerSetsCallback(masks, order, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<bool>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLVector<TLContactStatus>>> GetStatusesAsync()
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLVector<TLContactStatus>>>();
        //    GetStatusesCallback((callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLVector<TLContactStatus>>(callback));
        //    }, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLVector<TLContactStatus>>(faultCallback));
        //    });
        //    return tsc.Task;
        //}

        //public Task<MTProtoResponse<TLAuthSentCode>> SendCodeAsync(string phoneNumber, bool? currentNumber, Action<int> attemptFailed = null)
        //{
        //    var tsc = new TaskCompletionSource<MTProtoResponse<TLAuthSentCode>>();
        //    SendCodeCallback(phoneNumber, currentNumber, (callback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(callback));
        //    }, attemptFailed, (faultCallback) =>
        //    {
        //        tsc.TrySetResult(new MTProtoResponse<TLAuthSentCode>(faultCallback));
        //    });
        //    return tsc.Task;
        //}
    }
}
