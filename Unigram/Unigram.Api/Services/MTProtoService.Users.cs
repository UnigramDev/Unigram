using System;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Users;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public async Task<MTProtoResponse<TLVector<TLUserBase>>> GetUsersAsync(TLVector<TLInputUserBase> id)
        {
            var obj = new TLUsersGetUsers { Id = id };

            var result = await SendInformativeMessage<TLVector<TLUserBase>>("users.getUsers", obj);
            if (result.Error == null)
            {
                var task = new TaskCompletionSource<MTProtoResponse<TLVector<TLUserBase>>>();
                _cacheService.SyncUsers(result.Value, (callback) => task.SetResult(new MTProtoResponse<TLVector<TLUserBase>>(callback)));
                return await task.Task;
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUserFull>> GetFullUserAsync(TLInputUserBase id)
        {
            var obj = new TLUsersGetFullUser { Id = id };

            var result = await SendInformativeMessage<TLUserFull>("users.getFullUser", obj);
            if (result.Error == null)
            {
                var task = new TaskCompletionSource<MTProtoResponse<TLUserFull>>();
                _cacheService.SyncUser(result.Value, (callback) => task.SetResult(new MTProtoResponse<TLUserFull>(callback)));
                return await task.Task;
            }

            return result;
        }
    }
}
