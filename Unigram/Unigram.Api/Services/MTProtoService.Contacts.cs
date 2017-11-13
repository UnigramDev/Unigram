using System;
using Telegram.Api.Extensions;
using Telegram.Api.Native;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Contacts;
using Telegram.Api.TL.Contacts.Methods;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void ResetSavedAsync(Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsResetSaved();

            const string caption = "contacts.resetSaved";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ResetTopPeerRatingAsync(TLTopPeerCategoryBase category, TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsResetTopPeerRating { Category = category, Peer = peer };

            const string caption = "contacts.resetTopPeerRating";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetTopPeersAsync(TLContactsGetTopPeers.Flag flags, int offset, int limit, int hash, Action<TLContactsTopPeersBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsGetTopPeers { Flags = flags, Offset  = offset, Limit = limit, Hash = hash };

            const string caption = "contacts.getTopPeers";
            SendInformativeMessage<TLContactsTopPeersBase>(caption, obj, result =>
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

            const string caption = "contacts.resolveUsername";
            SendInformativeMessage<TLContactsResolvedPeer>(caption, obj,
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

            const string caption = "contacts.getStatuses";
            SendInformativeMessage<TLVector<TLContactStatus>>(caption, obj, 
                contacts =>
                {
                    _cacheService.SyncStatuses(contacts, callback);
                }, 
                faultCallback);
        }

        public void GetContactsAsync(int hash, Action<TLContactsContactsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsGetContacts { Hash = hash };

            const string caption = "contacts.getContacts";
            SendInformativeMessage<TLContactsContactsBase>(caption, obj, result => _cacheService.SyncContacts(result, callback), faultCallback);
        }

        public void ImportContactsAsync(TLVector<TLInputContactBase> contacts, Action<TLContactsImportedContacts> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsImportContacts { Contacts = contacts };

            const string caption = "contacts.importContacts";
            SendInformativeMessage<TLContactsImportedContacts>(caption, obj,
                result =>
                {
                    _cacheService.SyncContacts(result, callback);
                },
                faultCallback, flags: RequestFlag.FailOnServerError | RequestFlag.CanCompress);
        }

        public void DeleteContactAsync(TLInputUserBase id, Action<TLContactsLink> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsDeleteContact { Id = id };

            const string caption = "contacts.deleteContact";
            SendInformativeMessage<TLContactsLink>(caption, obj, result => _cacheService.SyncUserLink(result, callback), faultCallback);
        }

        public void DeleteContactsAsync(TLVector<TLInputUserBase> id, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsDeleteContacts { Id = id };

            const string caption = "contacts.deleteContacts";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void BlockAsync(TLInputUserBase id, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsBlock { Id = id };

            const string caption = "contacts.block";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void UnblockAsync(TLInputUserBase id, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsUnblock { Id = id };

            const string caption = "contacts.unblock";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetBlockedAsync(int offset, int limit, Action<TLContactsBlockedBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLContactsGetBlocked { Offset = offset, Limit = limit };

            const string caption = "contacts.getBlocked";
            SendInformativeMessage<TLContactsBlockedBase>(caption, obj, 
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

            const string caption = "contacts.search";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }
    }
}
