using System;
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Users;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        public void GetUsersAsync(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetUsers { Id = id };

            SendInformativeMessage<TLVector<TLUserBase>>("users.getUsers", obj, result =>
            {
                _cacheService.SyncUsers(result, callback);
            }, 
            faultCallback);
        }

        public void GetFullUserAsync(TLInputUserBase id, Action<TLUserFull> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetFullUser { Id = id };

            SendInformativeMessage<TLUserFull>("users.getFullUser", obj, userFull =>
            {
                _cacheService.SyncUser(userFull, result => callback.SafeInvoke(result));
            }, faultCallback);
        }
	}
}
