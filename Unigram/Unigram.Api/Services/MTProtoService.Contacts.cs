using System;
using System.Threading.Tasks;
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Contacts;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public async Task<MTProtoResponse<TLContactsResolvedPeer>> ResolveUsernameAsync(string username)
        {
            var obj = new TLContactsResolveUsername { Username = username };

            var result = await SendInformativeMessage<TLContactsResolvedPeer>("contacts.resolveUsername", obj);
            if (result.Error == null)
            {
                _cacheService.SyncUsersAndChats(result.Value.Users, result.Value.Chats,
                    tuple =>
                    {
                        result.Value.Users = tuple.Item1;
                        result.Value.Chats = tuple.Item2;
                    });
            }

            return result;
        }

        public async Task<MTProtoResponse<TLVector<TLContactStatus>>> GetStatusesAsync()
        {
            var obj = new TLContactsGetStatuses();

            var result = await SendInformativeMessage<TLVector<TLContactStatus>>("contacts.getStatuses", obj);
            if (result.Error == null)
            {
                _cacheService.SyncStatuses(result.Value, null);
            }

            return result;
        }

        public async Task<MTProtoResponse<TLContactsContactsBase>> GetContactsAsync(string hash)
        {
            var obj = new TLContactsGetContacts { Hash = hash };

            var result = await SendInformativeMessage<TLContactsContactsBase>("contacts.getContacts", obj);
            if (result.Error == null)
            {
                var task = new TaskCompletionSource<MTProtoResponse<TLContactsContactsBase>>();
                _cacheService.SyncContacts(result.Value, (callback) =>
                {
                    task.TrySetResult(new MTProtoResponse<TLContactsContactsBase>(callback));
                });
                return await task.Task;
            }
            return result;
        }

        public async Task<MTProtoResponse<TLContactsImportedContacts>> ImportContactsAsync(TLVector<TLInputContactBase> contacts, bool replace)
        {
            var obj = new TLContactsImportContacts { Contacts = contacts, Replace = replace };

            var result = await SendInformativeMessage<TLContactsImportedContacts>("contacts.importContacts", obj);
            if (result.Error == null)
            {
                var task = new TaskCompletionSource<MTProtoResponse<TLContactsImportedContacts>>();
                _cacheService.SyncContacts(result.Value, (callback) =>
                {
                    task.TrySetResult(new MTProtoResponse<TLContactsImportedContacts>(callback));
                });
                return await task.Task;
            }
            return result;
        }

        public async Task<MTProtoResponse<TLContactsLink>> DeleteContactAsync(TLInputUserBase id)
        {
            var obj = new TLContactsDeleteContact { Id = id };

            var result = await SendInformativeMessage<TLContactsLink>("contacts.deleteContact", obj);
            if (result.Error == null)
            {
                var task = new TaskCompletionSource<MTProtoResponse<TLContactsLink>>();
                _cacheService.SyncUserLink(result.Value, (callback) =>
                {
                    task.TrySetResult(new MTProtoResponse<TLContactsLink>(callback));
                });
                await task.Task;
            }
            return result;
        }

        public Task<MTProtoResponse<bool>> DeleteContactsAsync(TLVector<TLInputUserBase> id)
        {
            var obj = new TLContactsDeleteContacts { Id = id };

            return SendInformativeMessage<bool>("contacts.deleteContacts", obj);
        }

        public Task<MTProtoResponse<bool>> BlockAsync(TLInputUserBase id)
        {
            var obj = new TLContactsBlock { Id = id };

            return SendInformativeMessage<bool>("contacts.block", obj);
        }

        public Task<MTProtoResponse<bool>> UnblockAsync(TLInputUserBase id)
        {
            var obj = new TLContactsUnblock { Id = id };

            return SendInformativeMessage<bool>("contacts.unblock", obj);
        }

        public Task<MTProtoResponse<TLContactsBlockedBase>> GetBlockedAsync(int offset, int limit)
        {
            var obj = new TLContactsGetBlocked { Offset = offset, Limit = limit };

            return SendInformativeMessage<TLContactsBlockedBase>("contacts.getBlocked", obj);
        }

        public Task<MTProtoResponse<TLContactsFound>> SearchAsync(string q, int limit)
        {
            var obj = new TLContactsSearch { Q = q, Limit = limit };

            return SendInformativeMessage<TLContactsFound>("contacts.search", obj);
        }
    }
}
