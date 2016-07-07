using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Auth;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public async Task<MTProtoResponse<TLAuthCheckedPhone>> CheckPhoneAsync(string phoneNumber)
        {
            var obj = new TLAuthCheckPhone { PhoneNumber = phoneNumber };

            return await SendInformativeMessage<TLAuthCheckedPhone>("auth.checkPhone", obj);
        }

        public async Task<MTProtoResponse<TLAuthSentCode>> SendCodeAsync(string phoneNumber)
        {
            var obj = new TLAuthSendCode { PhoneNumber = phoneNumber, ApiId = Constants.ApiId, ApiHash = Constants.ApiHash, /*LangCode = Utils.CurrentUICulture()*/ };

            return await SendInformativeMessage<TLAuthSentCode>("auth.sendCode", obj, 3);
        }

        // DEPRECATED
        //public async Task<MTProtoResponse<bool>> SendCallAsync(string phoneNumber, string phoneCodeHash)
        //{
        //    var obj = new TLSendCall { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash };

        //    return await SendInformativeMessage<bool>("auth.sendCall", obj);
        //}

        public async Task<MTProtoResponse<TLAuthAuthorization>> SignUpAsync(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName)
        {
            var obj = new TLAuthSignUp { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode, FirstName = firstName, LastName = lastName };

            var result = await SendInformativeMessage<TLAuthAuthorization>("auth.signUp", obj);
            if (result.IsSucceeded)
            {
                // TODO: sync
                _cacheService.SyncUser(result.Value.User, (callback) => { });
            }
            return result;
        }

        public async Task<MTProtoResponse<TLAuthAuthorization>> SignInAsync(string phoneNumber, string phoneCodeHash, string phoneCode)
        {
            var obj = new TLAuthSignIn { PhoneNumber = phoneNumber, PhoneCodeHash = phoneCodeHash, PhoneCode = phoneCode };

            var result = await SendInformativeMessage<TLAuthAuthorization>("auth.signIn", obj);
            if (result.IsSucceeded)
            {
                // TODO: sync
                _cacheService.SyncUser(result.Value.User, (callback) => { });
            }
            return result;
        }

        public Task<MTProtoResponse<TLAuthSentCode>> CancelSignInAsync()
        {
            CancelDelayedItemsAsync(true);
            return null;
        }

        public async Task<MTProtoResponse<bool>> LogOutAsync()
        {
            var obj = new TLAuthLogOut();

            return await SendInformativeMessage<bool>("auth.logOut", obj);
        }

        public async Task<MTProtoResponse<bool>> SendInvitesAsync(TLVector<string> phoneNumbers, string message)
        {
            var obj = new TLAuthSendInvites { PhoneNumbers = phoneNumbers, Message = message };

            return await SendInformativeMessage<bool>("auth.sendInvites", obj);
        }

        public async Task<MTProtoResponse<TLAuthExportedAuthorization>> ExportAuthorizationAsync(int dcId)
        {
            var obj = new TLAuthExportAuthorization { DCId = dcId };

            return await SendInformativeMessage<TLAuthExportedAuthorization>("auth.exportAuthorization", obj);
        }

        public async Task<MTProtoResponse<TLAuthAuthorization>> ImportAuthorizationAsync(int id, byte[] bytes)
        {
            var obj = new TLAuthImportAuthorization { Id = id, Bytes = bytes };

            return await SendInformativeMessage<TLAuthAuthorization>("auth.importAuthorization", obj);
        }

        public async Task<MTProtoResponse<TLAuthAuthorization>> ImportAuthorizationByTransportAsync(ITransport transport, int id, byte[] bytes)
        {
            var obj = new TLAuthImportAuthorization { Id = id, Bytes = bytes };

            return await SendInformativeMessageByTransport<TLAuthAuthorization>(transport, "auth.importAuthorization", obj);
        }

        public async Task<MTProtoResponse<bool>> ResetAuthorizationsAsync()
        {
            var obj = new TLAuthResetAuthorizations();

            return await SendInformativeMessage<bool>("auth.resetAuthorizations", obj);
        }
    }
}
