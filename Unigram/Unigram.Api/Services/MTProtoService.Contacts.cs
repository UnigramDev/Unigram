using System;
using Telegram.Api.Extensions;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Contacts;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void ResetTopPeerRatingAsync(TLTopPeerCategoryBase category, TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsResetTopPeerRating { Category = category, Peer = peer };

            SendInformativeMessage<bool>("contacts.resetTopPeerRating", obj, callback, faultCallback);
        }

        public void GetTopPeersAsync(TLContactsGetTopPeers.Flag flags, int offset, int limit, int hash, Action<TLContactsTopPeersBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsGetTopPeers { Flags = flags, Offset  = offset, Limit = limit, Hash = hash };

            SendInformativeMessage<TLContactsTopPeersBase>("contacts.getTopPeers", obj, result =>
            {
                var topPeers = result as TLContactsTopPeers;
                if (topPeers != null)
                {
                    _cacheService.SyncUsersAndChats(topPeers.Users, topPeers.Chats,
                        tuple =>
                        {
                            topPeers.Users = tuple.Item1;
                            topPeers.Chats = tuple.Item2;
                            callback?.Invoke(result);
                        });
                }
                else
                {
                    callback?.Invoke(result);
                }
            }, faultCallback);
        }

        public void ResolveUsernameAsync(string username, Action<TLContactsResolvedPeer> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsResolveUsername { Username = username };

            SendInformativeMessage<TLContactsResolvedPeer>("contacts.resolveUsername", obj,
                result =>
                {
                    _cacheService.SyncUsersAndChats(result.Users, result.Chats, 
                        tuple =>
                        {
                            result.Users = tuple.Item1;
                            result.Chats = tuple.Item2;
                            callback?.Invoke(result);
                        });
                }, 
                faultCallback);
        }

        public void GetStatusesAsync(Action<TLVector<TLContactStatus>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsGetStatuses();

            SendInformativeMessage<TLVector<TLContactStatus>>("contacts.getStatuses", obj, 
                contacts =>
                {
                    _cacheService.SyncStatuses(contacts, callback);
                }, 
                faultCallback);
        }

        public void GetContactsAsync(string hash, Action<TLContactsContactsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsGetContacts { Hash = hash };

            SendInformativeMessage<TLContactsContactsBase>("contacts.getContacts", obj, result => _cacheService.SyncContacts(result, callback), faultCallback);
        }

        public void ImportContactsAsync(TLVector<TLInputContactBase> contacts, bool replace, Action<TLContactsImportedContacts> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsImportContacts { Contacts = contacts, Replace = replace };

            SendInformativeMessage<TLContactsImportedContacts>("contacts.importContacts", obj, result => _cacheService.SyncContacts(result, callback), faultCallback);
        }

        public void DeleteContactAsync(TLInputUserBase id, Action<TLContactsLink> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsDeleteContact { Id = id };

            SendInformativeMessage<TLContactsLink>("contacts.deleteContact", obj, result => _cacheService.SyncUserLink(result, callback), faultCallback);
        }

        public void DeleteContactsAsync(TLVector<TLInputUserBase> id, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsDeleteContacts { Id = id };

            SendInformativeMessage("contacts.deleteContacts", obj, callback, faultCallback);
        }

        public void BlockAsync(TLInputUserBase id, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsBlock { Id = id };

            SendInformativeMessage("contacts.block", obj, callback, faultCallback);
        }

        public void UnblockAsync(TLInputUserBase id, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsUnblock { Id = id };

            SendInformativeMessage("contacts.unblock", obj, callback, faultCallback);
        }

        public void GetBlockedAsync(int offset, int limit, Action<TLContactsBlockedBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsGetBlocked { Offset = offset, Limit = limit };

            SendInformativeMessage<TLContactsBlockedBase>("contacts.getBlocked", obj, 
                result =>
                {
                    _cacheService.SyncUsers(result.Users, 
                        r =>
                        {
                            callback?.Invoke(result);
                        });
                },
                faultCallback);
        }

        public void SearchAsync(string q, int limit, Action<TLContactsFound> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsSearch { Q = q, Limit = limit };
            //var invokeWithLayer18 = new TLInvokeWithLayer18 {Data = obj};
            SendInformativeMessage("contacts.search", obj, callback, faultCallback);
        }
    }
}
