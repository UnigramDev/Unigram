using System;
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Users;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        public void GetUsersCallback(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUsersGetUsers { Id = id };

            SendInformativeMessage<TLVector<TLUserBase>>("users.getUsers", obj, result =>
            {
                _cacheService.SyncUsers(result, callback);
            }, 
            faultCallback);
        }

        public void GetFullUserCallback(TLInputUserBase id, Action<TLUserFull> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUsersGetFullUser { Id = id };

            SendInformativeMessage<TLUserFull>("users.getFullUser", obj, userFull =>
            {
                _cacheService.SyncUser(userFull, result => callback?.Invoke(result));
            }, faultCallback);
        }
	}
}
