using System;
using Telegram.Api.Extensions;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Users.Methods;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        public void GetUsersAsync(TLVector<TLInputUserBase> id, Action<TLVector<TLUserBase>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUsersGetUsers { Id = id };

            const string caption = "users.getUsers";
            SendInformativeMessage<TLVector<TLUserBase>>(caption, obj, result =>
            {
                _cacheService.SyncUsers(result, callback);
            }, 
            faultCallback);
        }

        public void GetFullUserAsync(TLInputUserBase id, Action<TLUserFull> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUsersGetFullUser { Id = id };

            const string caption = "users.getFullUser";
            SendInformativeMessage<TLUserFull>(caption, obj, userFull =>
            {
                _cacheService.SyncUser(userFull, result => callback?.Invoke(result));
            },
            faultCallback);
        }
	}
}
