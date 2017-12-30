//#define DEBUG_READ_HISTORY

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Help.Methods;
using Telegram.Api.TL.Messages.Methods;
using Telegram.Api.TL.Messages;
using Telegram.Api.Native.TL;
using Telegram.Api.Native;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void ReadMentionsAsync(TLInputPeerBase inputPeer, Action<TLMessagesAffectedHistory> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesReadMentions { Peer = inputPeer };

            const string caption = "messages.readMentions";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetUnreadMentionsAsync(TLInputPeerBase inputPeer, int offsetId, int addOffset, int limit, int maxId, int minId, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetUnreadMentions { Peer = inputPeer, OffsetId = offsetId, AddOffset = addOffset, Limit = limit, MaxId = maxId, MinId = minId };

            const string caption = "messages.getUnreadMentions";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void FaveStickerAsync(TLInputDocumentBase id, bool unfave, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesFaveSticker { Id = id, Unfave = unfave };

            const string caption = "messages.faveSticker";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetAttachedStickersAsync(TLInputStickeredMediaBase media, Action<TLVector<TLStickerSetCoveredBase>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetAttachedStickers { Media = media };

            const string caption = "messages.getAttachedStickers";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetRecentStickersAsync(bool attached, int hash, Action<TLMessagesRecentStickersBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetRecentStickers { Hash = hash, IsAttached = attached };

            const string caption = "messages.getRecentStickers";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetFavedStickersAsync(int hash, Action<TLMessagesFavedStickersBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetFavedStickers { Hash = hash };

            const string caption = "messages.getFavedStickers";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ClearRecentStickersAsync(bool attached, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesClearRecentStickers { IsAttached = attached };

            const string caption = "messages.clearRecentStickers";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ReadFeaturedStickersAsync(TLVector<long> id, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesReadFeaturedStickers { Id = id };

            const string caption = "messages.readFeaturedStickers";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetAllDraftsAsync(Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetAllDrafts();

            const string caption = "messages.getAllDrafts";
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

        public void SaveDraftAsync(TLInputPeerBase peer, TLDraftMessageBase draft, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = draft.ToSaveDraftObject(peer);

            const string caption = "messages.saveDraft";
            SendInformativeMessage<bool>(caption, obj, callback, faultCallback);
        }

        public void GetPeerDialogsAsync(TLVector<TLInputPeerBase> peers, Action<TLMessagesPeerDialogs> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetPeerDialogs { Peers = peers };

            const string caption = "messages.getPeerDialogs";
            SendInformativeMessage<TLMessagesPeerDialogs>(caption, obj, result =>
            {
                var dialogsCache = new Dictionary<int, List<TLDialog>>();
                foreach (var dialogBase in result.Dialogs)
                {
                    List<TLDialog> dialogs;
                    if (dialogsCache.TryGetValue(dialogBase.TopMessage, out dialogs))
                    {
                        dialogs.Add(dialogBase);
                    }
                    else
                    {
                        dialogsCache[dialogBase.TopMessage] = new List<TLDialog> { dialogBase };
                    }
                }

                foreach (var messageBase in result.Messages)
                {
                    ProcessSelfMessage(messageBase);

                    var messageCommon = messageBase as TLMessage;
                    if (messageCommon != null)
                    {
                        List<TLDialog> dialogs;
                        if (dialogsCache.TryGetValue(messageBase.Id, out dialogs))
                        {
                            TLDialog dialog53 = null;
                            if (messageCommon.ToId is TLPeerChannel)
                            {
                                dialog53 = dialogs.FirstOrDefault(x => x.Peer is TLPeerChannel && x.Peer.Id == messageCommon.ToId.Id) as TLDialog;
                            }
                            else if (messageCommon.ToId is TLPeerChat)
                            {
                                dialog53 = dialogs.FirstOrDefault(x => x.Peer is TLPeerChat && x.Peer.Id == messageCommon.ToId.Id) as TLDialog;
                            }
                            else if (messageCommon.ToId is TLPeerUser)
                            {
                                var peer = messageCommon.IsOut ? messageCommon.ToId : new TLPeerUser { Id = messageCommon.FromId ?? 0 };
                                dialog53 = dialogs.FirstOrDefault(x => x.Peer is TLPeerUser && x.Peer.Id == peer.Id) as TLDialog;
                            }
                            if (dialog53 != null)
                            {
                                if (messageCommon.IsOut)
                                {
                                    if (messageCommon.Id > dialog53.ReadOutboxMaxId)
                                    {
                                        messageCommon.SetUnreadSilent(true);
                                    }
                                }
                                else
                                {
                                    if (messageCommon.Id > dialog53.ReadInboxMaxId)
                                    {
                                        messageCommon.SetUnreadSilent(true);
                                    }
                                }
                            }
                        }
                    }
                }

                //Debug.WriteLine("messages.getDialogs response elapsed=" + stopwatch.Elapsed);

                var slice = new TLMessagesDialogs();
                slice.Chats = result.Chats;
                slice.Dialogs = result.Dialogs;
                slice.Messages = result.Messages;
                slice.Users = result.Users;

                var r = obj;
                _cacheService.SyncDialogs(slice, sync =>
                {
                    callback?.Invoke(new TLMessagesPeerDialogs
                    {
                        Chats = sync.Chats,
                        Dialogs = sync.Dialogs,
                        Messages = sync.Messages,
                        Users = sync.Users
                    });
                });
            },
            faultCallback);
        }

        public void GetInlineBotResultsAsync(TLInputUserBase bot, TLInputPeerBase peer, TLInputGeoPointBase geoPoint, string query, string offset, Action<TLMessagesBotResults> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetInlineBotResults { Flags = 0, Bot = bot, Peer = peer, GeoPoint = geoPoint, Query = query, Offset = offset };

            const string caption = "messages.getInlineBotResults";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void SetInlineBotResultsAsync(bool gallery, bool pr, long queryId, TLVector<TLInputBotInlineResultBase> results, int cacheTime, string nextOffset, TLInlineBotSwitchPM switchPM, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSetInlineBotResults { Flags = 0, IsGallery = gallery, IsPrivate = pr, QueryId = queryId, Results = results, CacheTime = cacheTime, NextOffset = nextOffset, HasSwitchPM = switchPM != null, SwitchPM = switchPM };

            const string caption = "messages.setInlineBotResults";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void SendInlineBotResultAsync(TLMessage message, Action<TLMessageCommonBase> callback, Action fastCallback, Action<TLRPCError> faultCallback = null)
        {
            var inputPeer = PeerToInputPeer(message.ToId);
            var obj = new TLMessagesSendInlineBotResult { Flags = 0, Peer = inputPeer, ReplyToMsgId = message.ReplyToMsgId, RandomId = message.RandomId ?? 0, QueryId = message.InlineBotResultQueryId, Id = message.InlineBotResultId };

            var message48 = message as TLMessage;
            if (message48 != null && message48.IsSilent)
            {
                obj.IsSilent = true;
            }

            const string caption = "messages.sendInlineBotResult";
            SendInlineBotResultAsyncInternal(obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    var shortSentMessage = result as TLUpdateShortSentMessage;
                    if (shortSentMessage != null)
                    {
                        // TODO: verify
                        message.Flags = (TLMessage.Flag)(int)shortSentMessage.Flags;
                        if (shortSentMessage.HasMedia)
                        {
                            // TODO: message._media = shortSentMessage.Media;
                            message.Media = shortSentMessage.Media;
                        }
                        if (shortSentMessage.HasEntities)
                        {
                            message.Entities = shortSentMessage.Entities;
                        }

                        Execute.BeginOnUIThread(() =>
                        {
                            message.State = GetMessageStatus(_cacheService, message.ToId);
                            message.Date = shortSentMessage.Date;
                            if (shortSentMessage.Media is TLMessageMediaWebPage)
                            {
                                message.RaisePropertyChanged(() => message.Media);
                            }

#if DEBUG
                            message.Id = shortSentMessage.Id;
                            message.RaisePropertyChanged(() => message.Id);
                            message.RaisePropertyChanged(() => message.Date);
#endif
                        });

                        _updatesService.SetState(multiPts, caption);

                        message.Id = shortSentMessage.Id;
                        _cacheService.SyncSendingMessage(message, null, callback);
                        return;
                    }

                    var updates = result as TLUpdates;
                    if (updates != null)
                    {
                        foreach (var update in updates.Updates)
                        {
                            var updateNewMessage = update as TLUpdateNewMessage;
                            if (updateNewMessage != null)
                            {
                                Execute.BeginOnUIThread(() =>
                                {
                                    // faster update web page with inline bots @imdb, @vid, @wiki
                                    var newMessage = updateNewMessage.Message as TLMessage;
                                    if (newMessage != null)
                                    {
                                        var mediaWebPage = newMessage.Media as TLMessageMediaWebPage;
                                        if (mediaWebPage != null)
                                        {
                                            message.Media = newMessage.Media;
                                        }

                                        if (mediaWebPage == null && newMessage.HasMedia)
                                        {
                                            Execute.ShowDebugMessage(newMessage.Media.GetType().ToString());
                                        }
                                    }
                                });

                                var messageCommon = updateNewMessage.Message as TLMessage;
                                if (messageCommon != null)
                                {
                                    messageCommon.RandomId = message.RandomId;
                                    message.Id = messageCommon.Id;
                                    message.Date = messageCommon.Date;
                                }
                            }
                        }

                        Execute.BeginOnUIThread(() =>
                        {
                            message.State = GetMessageStatus(_cacheService, message.ToId);
                        });

                        if (multiPts != null)
                        {
                            _updatesService.SetState(multiPts, caption);
                        }
                        else
                        {
                            _updatesService.ProcessUpdates(updates);
                        }

                        callback?.Invoke(message);
                    }
                },
                fastCallback,
                faultCallback);
        }

        public void GetDocumentByHashAsync(byte[] sha256, int size, string mimeType, Action<TLDocumentBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetDocumentByHash { Sha256 = sha256, Size = size, MimeType = mimeType };

            const string caption = "messages.getDocumentByHash";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void SearchGifsAsync(string q, int offset, Action<TLMessagesFoundGifs> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSearchGifs { Q = q, Offset = offset };

            const string caption = "messages.searchGifs";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetSavedGifsAsync(int hash, Action<TLMessagesSavedGifsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetSavedGifs { Hash = hash };

            const string caption = "messages.getSavedGifs";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void SaveGifAsync(TLInputDocumentBase id, bool unsave, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSaveGif { Id = id, Unsave = unsave };

            const string caption = "messages.saveGif";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ReorderStickerSetsAsync(bool masks, TLVector<long> order, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesReorderStickerSets { Order = order };
            if (masks)
            {
                obj.IsMasks = true;
            }

            const string caption = "messages.reorderStickerSets";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ReportSpamAsync(TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
#if DEBUG
            Execute.BeginOnThreadPool(() => callback?.Invoke(true));
            return;
#endif

            var obj = new TLMessagesReportSpam { Peer = peer };

            const string caption = "messages.reportSpam";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void GetWebPagePreviewAsync(string message, Action<TLMessageMediaBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetWebPagePreview { Message = message };

            const string caption = "messages.getWebPagePreview";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetWebPageAsync(string url, int hash, Action<TLWebPageBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetWebPage { Url = url, Hash = hash };

            const string caption = "messages.getWebPage";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetFeaturedStickersAsync(int hash, Action<TLMessagesFeaturedStickersBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetFeaturedStickers { Hash = hash };

            const string caption = "messages.getFeaturedStickers";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetArchivedStickersAsync(long offsetId, int limit, bool masks, Action<TLMessagesArchivedStickers> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetArchivedStickers { OffsetId = offsetId, Limit = limit, IsMasks = masks };

            const string caption = "messages.getArchivedStickers";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetAllStickersAsync(int hash, Action<TLMessagesAllStickersBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetAllStickers { Hash = hash };

            const string caption = "messages.getAllStickers";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetStickerSetAsync(TLInputStickerSetBase stickerset, Action<TLMessagesStickerSet> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetStickerSet { StickerSet = stickerset };

            const string caption = "messages.getStickerSet";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void InstallStickerSetAsync(TLInputStickerSetBase stickerset, bool archived, Action<TLMessagesStickerSetInstallResultBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesInstallStickerSet { StickerSet = stickerset, Archived = archived };

            const string caption = "messages.installStickerSet";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void UninstallStickerSetAsync(TLInputStickerSetBase stickerset, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesUninstallStickerSet { StickerSet = stickerset };

            const string caption = "messages.uninstallStickerSet";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        private static TLMessageState GetMessageStatus(ICacheService cacheService, TLPeerBase peer)
        {
            var status = TLMessageState.Confirmed;
            if (peer is TLPeerUser)
            {
                var userBase = cacheService.GetUser(peer.Id);
                var user = userBase as TLUser;
                if (userBase != null)
                {
                    // TODO: 06/05/2017
                    /*var botInfo = userBase.BotInfo as TLBotInfo;
                    if (botInfo != null)
                    {
                        status = TLMessageState.Read;
                    }
                    else*/
                    if (user != null && user.IsSelf)
                    {
                        status = TLMessageState.Read;
                    }
                }
            }

            //if (peer is TLPeerChannel)
            //{
            //    status = TLMessageState.Read;
            //}

            return status;
        }

        private TLInputPeerBase PeerToInputPeer(TLPeerBase peer)
        {
            if (peer is TLPeerUser)
            {
                var cachedUser = _cacheService.GetUser(peer.Id);
                if (cachedUser != null)
                {
                    //var userForeign = cachedUser as TLUserForeign;
                    //var userRequest = cachedUser as TLUserRequest;
                    var user = cachedUser as TLUser;

                    //if (userForeign != null)
                    //{
                    //    return new TLInputPeerUser { UserId = userForeign.Id, AccessHash = userForeign.AccessHash };
                    //}

                    //if (userRequest != null)
                    //{
                    //    return new TLInputPeerUser { UserId = userRequest.Id, AccessHash = userRequest.AccessHash };
                    //}

                    if (user != null)
                    {
                        return user.ToInputPeer();
                    }

                    return new TLInputPeerUser { UserId = peer.Id, AccessHash = 0 };
                }

                return new TLInputPeerUser { UserId = peer.Id, AccessHash = 0 };
            }

            if (peer is TLPeerChannel)
            {
                var channel = _cacheService.GetChat(peer.Id) as TLChannel;
                if (channel != null)
                {
                    return new TLInputPeerChannel { ChannelId = peer.Id, AccessHash = channel.AccessHash ?? 0 };
                }
            }

            if (peer is TLPeerChat)
            {
                return new TLInputPeerChat { ChatId = peer.Id };
            }

            //return new TLInputPeerBroadcast { ChatId = peer.Id };
            return new TLInputPeerEmpty();
        }

        public void SendMessageAsync(TLMessage message, Action<TLMessageCommonBase> callback, Action fastCallback, Action<TLRPCError> faultCallback = null)
        {
            var inputPeer = PeerToInputPeer(message.ToId);
            var obj = new TLMessagesSendMessage { Peer = inputPeer, ReplyToMsgId = message.ReplyToMsgId, Message = message.Message, RandomId = message.RandomId.Value };

            if (message.Entities != null)
            {
                obj.Entities = message.Entities;
                obj.HasEntities = true;
            }

            // TODO: future
            //if (message.NoWebpage)
            //{
            //    obj.IsNoWebpage = true;
            //}

            // TODO
            //if (message.IsChannelMessage)
            //{
            //    obj.SetChannelMessage();
            //}

            var message48 = message as TLMessage;
            if (message48 != null && message48.IsSilent)
            {
                obj.IsSilent = true;
            }

            obj.IsClearDraft = true;

            const string caption = "messages.sendMessage";
            SendMessageAsyncInternal(obj,
                result =>
                {
#if DEBUG
                    var builder = new StringBuilder();
                    builder.Append(result.GetType());
                    var updates = result as TLUpdates;
                    var updatesShort = result as TLUpdateShort;
                    if (updates != null)
                    {
                        foreach (var update in updates.Updates)
                        {
                            builder.Append(update);
                        }
                    }
                    else if (updatesShort != null)
                    {
                        builder.Append(updatesShort.Update);
                    }

                    Logs.Log.Write(string.Format("{0} result={1}", caption, builder.ToString()));
#endif

                    var multiPts = result as ITLMultiPts;

                    var shortSentMessage = result as TLUpdateShortSentMessage;
                    if (shortSentMessage != null)
                    {
                        // TODO: verify
                        message.Flags = (TLMessage.Flag)(int)shortSentMessage.Flags;
                        if (shortSentMessage.HasMedia)
                        {
                            // TODO: verify
                            //message._media = shortSentMessage.Media;
                            message.Media = shortSentMessage.Media;
                        }
                        if (shortSentMessage.HasEntities)
                        {
                            message.HasEntities = shortSentMessage.HasEntities;
                            message.Entities = shortSentMessage.Entities;
                        }

                        Execute.BeginOnUIThread(() =>
                        {
                            message.State = GetMessageStatus(_cacheService, message.ToId);
                            message.Date = shortSentMessage.Date;
                            if (shortSentMessage.Media is TLMessageMediaWebPage)
                            {
                                message.RaisePropertyChanged(() => message.Media);
                            }

#if DEBUG
                            message.Id = shortSentMessage.Id;
                            message.RaisePropertyChanged(() => message.Id);
                            message.RaisePropertyChanged(() => message.Date);
                            message.RaisePropertyChanged(() => message.Self);
#endif
                        });

                        _updatesService.SetState(multiPts, caption);

                        message.Id = shortSentMessage.Id;
                        _cacheService.SyncSendingMessage(message, null, callback);
                        return;
                    }

                    Execute.BeginOnUIThread(() =>
                    {
                        message.State = GetMessageStatus(_cacheService, message.ToId);
                    });

                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, new List<TLMessage> { message });
                    }

                    callback?.Invoke(message);
                },
                fastCallback,
                faultCallback);
        }

        private void ProcessUpdates(TLUpdatesBase updatesBase, IList<TLMessage> messages, bool notifyNewMessage = false)
        {
            var updates = updatesBase as TLUpdates;
            if (updates != null)
            {
                var messagesRandomIndex = new Dictionary<long, TLMessage>();
                if (messages != null)
                {
                    for (var i = 0; i < messages.Count; i++)
                    {
                        if (messages[i].RandomId != 0)
                        {
                            messagesRandomIndex[messages[i].RandomId.Value] = messages[i];
                        }
                    }
                }

                var updateNewMessageIndex = new Dictionary<long, TLUpdateNewMessage>();
                var updateNewChannelMessageIndex = new Dictionary<long, TLUpdateNewChannelMessage>();
                var updateMessageIdList = new List<TLUpdateMessageID>();
                for (var i = 0; i < updates.Updates.Count; i++)
                {
                    var updateNewMessage = updates.Updates[i] as TLUpdateNewMessage;
                    if (updateNewMessage != null)
                    {
                        ProcessSelfMessage(updateNewMessage.Message);

                        updateNewMessageIndex[updateNewMessage.Message.Id] = updateNewMessage;
                        continue;
                    }

                    var updateNewChannelMessage = updates.Updates[i] as TLUpdateNewChannelMessage;
                    if (updateNewChannelMessage != null)
                    {
                        ProcessSelfMessage(updateNewChannelMessage.Message);

                        updateNewChannelMessageIndex[updateNewChannelMessage.Message.Id] = updateNewChannelMessage;
                        continue;
                    }

                    var updateMessageId = updates.Updates[i] as TLUpdateMessageID;
                    if (updateMessageId != null)
                    {
                        updateMessageIdList.Add(updateMessageId);
                        continue;
                    }
                }

                foreach (var updateMessageId in updateMessageIdList)
                {
                    TLUpdateNewMessage updateNewMessage;
                    if (updateNewMessageIndex.TryGetValue(updateMessageId.Id, out updateNewMessage))
                    {
                        var cachedSendingMessage = _cacheService.GetMessage(updateMessageId.RandomId);
                        if (cachedSendingMessage != null)
                        {
                            updateNewMessage.Message.RandomId = updateMessageId.RandomId;
                        }
                    }

                    TLUpdateNewChannelMessage updateNewChannelMessage;
                    if (updateNewChannelMessageIndex.TryGetValue(updateMessageId.Id, out updateNewChannelMessage))
                    {
                        var cachedSendingMessage = _cacheService.GetMessage(updateMessageId.RandomId);
                        if (cachedSendingMessage != null)
                        {
                            updateNewChannelMessage.Message.RandomId = updateMessageId.RandomId;
                        }
                    }

                    TLMessage message;
                    if (messagesRandomIndex.TryGetValue(updateMessageId.RandomId, out message))
                    {
                        message.Id = updateMessageId.Id;

                        TLMessageCommonBase messageCommon = null;
                        if (updateNewMessage != null)
                        {
                            messageCommon = updateNewMessage.Message as TLMessageCommonBase;
                        }

                        if (updateNewChannelMessage != null)
                        {
                            messageCommon = updateNewChannelMessage.Message as TLMessageCommonBase;
                        }

                        if (messageCommon != null)
                        {
                            message.Date = messageCommon.Date;

                            if (messageCommon is TLMessage messageMessage)
                            {
                                if (message.Media != null && messageMessage.Media != null && message.Media.TypeId != messageMessage.Media.TypeId)
                                {
                                    message.Media = messageMessage.Media;
                                    message.RaisePropertyChanged(() => message.Media);
                                }

                                if (message.Message != messageMessage.Message || (message.HasEntities != messageMessage.HasEntities) || (message.Entities?.Count != messageMessage.Entities?.Count))
                                {
                                    message.HasEntities = messageMessage.HasEntities;
                                    message.Entities = messageMessage.Entities;
                                    message.Message = messageMessage.Message;
                                    message.RaisePropertyChanged(() => message.Self);
                                }
                            }
                        }
                    }
                }
            }

            _updatesService.ProcessUpdates(updates, notifyNewMessage);
        }

        public static void ProcessSelfMessage(TLMessageBase messageBase)
        {
            var messageCommon = messageBase as TLMessage;
            if (messageCommon != null
                && messageCommon.ToId is TLPeerUser
                && messageCommon.FromId != null
                && messageCommon.FromId.Value == messageCommon.ToId.Id)
            {
                messageCommon.IsOut = true;
                messageCommon.SetUnreadSilent(false);
            }
        }

        public void GetBotCallbackAnswerAsync(TLInputPeerBase peer, int messageId, byte[] data, bool game, Action<TLMessagesBotCallbackAnswer> callback, Action<TLRPCError> faultCallback = null)
        {
            // TODO: Layer 56
            var obj = new TLMessagesGetBotCallbackAnswer { Peer = peer, MsgId = messageId, Data = data, HasData = data != null, IsGame = game };

            const string caption = "messages.getBotCallbackAnswer";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void StartBotAsync(TLInputUserBase bot, string startParam, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesStartBot { Bot = bot, Peer = PeerToInputPeer(message.ToId), RandomId = message.RandomId ?? 0, StartParam = startParam };

            const string caption = "messages.startBot";
            StartBotAsyncInternal(obj,
                result =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        message.State = GetMessageStatus(_cacheService, message.ToId);

                        // TODO: 24/04/2017 verify if this is really needed
                        if (message.Media is TLMessageMediaPhoto photoMedia)
                        {
                            photoMedia.Photo.LastProgress = 0.0;
                            photoMedia.Photo.DownloadingProgress = 0.0;
                        }
                        else if (message.Media is TLMessageMediaDocument documentMedia)
                        {
                            documentMedia.Document.LastProgress = 0.0;
                            documentMedia.Document.DownloadingProgress = 0.0;
                        }
                    });

                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, new List<TLMessage> { message });
                    }

                    callback?.Invoke(result);
                },
                () =>
                {
                    //TLUtils.WriteLine(caption + " fast result " + message.RandomIndex, LogSeverity.Error);
                    //fastCallback();
                },
                faultCallback);
        }

        public void UploadMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message, Action<TLMessageMediaBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesUploadMedia { Peer = inputPeer, Media = inputMedia };

            const string caption = "messages.uploadMedia";
            SendInformativeMessage<TLMessageMediaBase>(caption, obj,
                result =>
                {
                    message.Media = result;
                    message.RaisePropertyChanged(() => message.Media);

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void SendMultiMediaAsync(TLInputPeerBase inputPeer, TLVector<TLInputSingleMedia> multiMedia, IList<TLMessage> messages, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSendMultiMedia { Peer = inputPeer, MultiMedia = multiMedia };

            var first = messages.FirstOrDefault();
            if (first != null)
            {
                obj.ReplyToMsgId = first.ReplyToMsgId;
                obj.HasReplyToMsgId = first.HasReplyToMsgId;

                obj.IsSilent = first.IsSilent;
            }

            const string caption = "messages.sendMultiMedia";
            SendMultiMediaAsyncInternal(obj,
                result =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        foreach (var message in messages)
                        {
                            message.State = GetMessageStatus(_cacheService, message.ToId);

                            // TODO: 24/04/2017 verify if this is really needed
                            if (message.Media is TLMessageMediaPhoto photoMedia)
                            {
                                photoMedia.Photo.LastProgress = 0.0;
                                photoMedia.Photo.DownloadingProgress = 0.0;
                            }
                            else if (message.Media is TLMessageMediaDocument documentMedia)
                            {
                                documentMedia.Document.LastProgress = 0.0;
                                documentMedia.Document.DownloadingProgress = 0.0;
                            }
                        }
                    });

                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, messages);
                    }

                    callback?.Invoke(result);
                },
                () =>
                {
                    //TLUtils.WriteLine(caption + " fast result " + message.RandomIndex, LogSeverity.Error);
                    //fastCallback();
                },
                faultCallback);
        }

        public void SendMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSendMedia { Peer = inputPeer, ReplyToMsgId = message.ReplyToMsgId, Media = inputMedia, RandomId = message.RandomId.Value };

            obj.IsSilent = message.IsSilent;

            const string caption = "messages.sendMedia";
            SendMediaAsyncInternal(obj,
                result =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        message.State = GetMessageStatus(_cacheService, message.ToId);

                        // TODO: 24/04/2017 verify if this is really needed
                        if (message.Media is TLMessageMediaPhoto photoMedia)
                        {
                            photoMedia.Photo.LastProgress = 0.0;
                            photoMedia.Photo.DownloadingProgress = 0.0;
                        }
                        else if (message.Media is TLMessageMediaDocument documentMedia)
                        {
                            documentMedia.Document.LastProgress = 0.0;
                            documentMedia.Document.DownloadingProgress = 0.0;
                        }
                    });

                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, new List<TLMessage> { message });
                    }

                    callback?.Invoke(result);
                },
                () =>
                {
                    //TLUtils.WriteLine(caption + " fast result " + message.RandomIndex, LogSeverity.Error);
                    //fastCallback();
                },
                faultCallback);
        }

        public void SendEncryptedAsync(TLInputEncryptedChat peer, long randomId, byte[] data, Action<TLMessagesSentEncryptedMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSendEncrypted { Peer = peer, RandomId = randomId, Data = data };

            SendEncryptedAsyncInternal(
                obj,
                result =>
                {
                    callback(result);
                },
                () =>
                {

                },
                faultCallback);
        }

        public void SendEncryptedFileAsync(TLInputEncryptedChat peer, long randomId, byte[] data, TLInputEncryptedFileBase file, Action<TLMessagesSentEncryptedFile> callback, Action fastCallback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSendEncryptedFile { Peer = peer, RandomId = randomId, Data = data, File = file };

            SendEncryptedFileAsyncInternal(
                obj,
                callback,
                () =>
                {

                },
                faultCallback);
        }

        public void SendEncryptedServiceAsync(TLInputEncryptedChat peer, long randomId, byte[] data, Action<TLMessagesSentEncryptedMessage> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSendEncryptedService { Peer = peer, RandomId = randomId, Data = data };

            SendEncryptedServiceAsyncInternal(
                obj,
                callback,
                () =>
                {

                },
                faultCallback);
        }

        public void ReadEncryptedHistoryAsync(TLInputEncryptedChat peer, int maxDate, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesReadEncryptedHistory { Peer = peer, MaxDate = maxDate };

            ReadEncryptedHistoryAsyncInternal(obj, callback, () => { }, faultCallback);
        }

        public void SetEncryptedTypingAsync(TLInputEncryptedChat peer, bool typing, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSetEncryptedTyping { Peer = peer, Typing = typing };

            const string caption = "messages.setEncryptedTyping";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void SetTypingAsync(TLInputPeerBase peer, bool typing, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var action = typing ? (TLSendMessageActionBase)new TLSendMessageTypingAction() : new TLSendMessageCancelAction();
            var obj = new TLMessagesSetTyping { Peer = peer, Action = action };

            const string caption = "messages.setTyping";
            SendInformativeMessage(caption, obj, callback, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void SetTypingAsync(TLInputPeerBase peer, TLSendMessageActionBase action, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSetTyping { Peer = peer, Action = action ?? new TLSendMessageTypingAction() };

            const string caption = "messages.setTyping";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetMessagesAsync(TLVector<int> id, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetMessages { Id = id };

            const string caption = "messages.getMessages";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetDialogsAsync(int offsetDate, int offsetId, TLInputPeerBase offsetPeer, int limit, Action<TLMessagesDialogsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetDialogs { OffsetDate = offsetDate, OffsetId = offsetId, OffsetPeer = offsetPeer, Limit = limit };

            //TLUtils.WriteLine(string.Format("{0} messages.getDialogs offset_date={1} offset_peer={2} offset_id={3} limit={4}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), offsetDate, offsetPeer, offsetId, limit), LogSeverity.Error);          

            const string caption = "messages.getDialogs";
            SendInformativeMessage<TLMessagesDialogsBase>(caption, obj, result =>
            {
                var dialogsCache = new Dictionary<int, List<TLDialog>>();
                foreach (var dialogBase in result.Dialogs)
                {
                    List<TLDialog> dialogs;
                    if (dialogsCache.TryGetValue(dialogBase.TopMessage, out dialogs))
                    {
                        dialogs.Add(dialogBase);
                    }
                    else
                    {
                        dialogsCache[dialogBase.TopMessage] = new List<TLDialog> { dialogBase };
                    }
                }

                foreach (var messageBase in result.Messages)
                {
                    ProcessSelfMessage(messageBase);

                    var messageCommon = messageBase as TLMessage;
                    if (messageCommon != null)
                    {
                        List<TLDialog> dialogs;
                        if (dialogsCache.TryGetValue(messageBase.Id, out dialogs))
                        {
                            TLDialog dialog53 = null;
                            if (messageCommon.ToId is TLPeerChannel)
                            {
                                dialog53 = dialogs.FirstOrDefault(x => x.Peer is TLPeerChannel && x.Peer.Id == messageCommon.ToId.Id) as TLDialog;
                            }
                            else if (messageCommon.ToId is TLPeerChat)
                            {
                                dialog53 = dialogs.FirstOrDefault(x => x.Peer is TLPeerChat && x.Peer.Id == messageCommon.ToId.Id) as TLDialog;
                            }
                            else if (messageCommon.ToId is TLPeerUser)
                            {
                                var peer = messageCommon.IsOut ? messageCommon.ToId : new TLPeerUser { Id = messageCommon.FromId ?? 0 };
                                dialog53 = dialogs.FirstOrDefault(x => x.Peer is TLPeerUser && x.Peer.Id == peer.Id) as TLDialog;
                            }
                            if (dialog53 != null)
                            {
                                if (messageCommon.IsOut)
                                {
                                    if (messageCommon.Id > dialog53.ReadOutboxMaxId)
                                    {
                                        messageCommon.SetUnreadSilent(true);
                                    }
                                }
                                else
                                {
                                    if (messageCommon.Id > dialog53.ReadInboxMaxId)
                                    {
                                        messageCommon.SetUnreadSilent(true);
                                    }
                                }
                            }
                        }
                    }
                }

                //Debug.WriteLine("messages.getDialogs response elapsed=" + stopwatch.Elapsed);

                var r = obj;
                _cacheService.SyncDialogs(result, callback);
            },
            faultCallback);
        }

        private void GetChannelHistoryAsyncInternal(bool sync, TLPeerBase peer, TLMessagesMessagesBase result, Action<TLMessagesMessagesBase> callback)
        {
            if (sync)
            {
                _cacheService.SyncPeerMessages(peer, result, false, false, callback);
            }
            else
            {
                if (result is ITLMessages messages)
                {
                    _cacheService.AddChats(messages.Chats, results => { });
                    _cacheService.AddUsers(messages.Users, results => { });
                }

                callback(result);
            }
        }

        private void GetHistoryAsyncInternal(bool sync, TLPeerBase peer, TLMessagesMessagesBase result, Action<TLMessagesMessagesBase> callback)
        {
            if (sync)
            {
                _cacheService.SyncPeerMessages(peer, result, false, true, callback);
            }
            else
            {
                if (result is ITLMessages messages)
                {
                    _cacheService.AddChats(messages.Chats, results => { });
                    _cacheService.AddUsers(messages.Users, results => { });
                }

                callback(result);
            }
        }

        public void GetHistoryAsync(TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int offset, int offsetDate, int maxId, int limit, int hash, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetHistory { Peer = inputPeer, AddOffset = offset, OffsetId = maxId, OffsetDate = offsetDate, Limit = limit, MaxId = int.MaxValue, MinId = 0, Hash = hash };

            //Debug.WriteLine("UpdateItems start request elapsed=" + (timer != null? timer.Elapsed.ToString() : null));

            const string caption = "messages.getHistory";
            SendInformativeMessage<TLMessagesMessagesBase>(caption, obj,
                result =>
                {
                    //Debug.WriteLine("UpdateItems stop request elapsed=" + (timer != null ? timer.Elapsed.ToString() : null));

                    if (result is ITLMessages messages)
                    {
                        foreach (var message in messages.Messages)
                        {
                            ProcessSelfMessage(message);
                        }
                    }

                    GetHistoryAsyncInternal(sync, peer, result, callback);
                },
                faultCallback);
        }

        public void SearchAsync(TLInputPeerBase peer, string query, TLInputUserBase from, TLMessagesFilterBase filter, int minDate, int maxDate, int offset, int maxId, int limit, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            //TLUtils.WriteLine(string.Format("{0} messages.search query={1} offset={2} limit={3}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), query, offset, limit), LogSeverity.Error);
            //Execute.ShowDebugMessage("messages.search filter=" + filter);

            var obj = new TLMessagesSearch { Flags = 0, Peer = peer, Q = query, FromId = from, Filter = filter, MinDate = minDate, MaxDate = maxDate, AddOffset = offset, MaxId = maxId, Limit = limit };
            //obj.SetImportant();

            const string caption = "messages.search";
            SendInformativeMessage<TLMessagesMessagesBase>(caption, obj, result =>
            {
                if (result is ITLMessages messages)
                {
                    _cacheService.SyncUsersAndChats(messages.Users, messages.Chats, tuple => callback?.Invoke(result));
                }
                else
                {
                    callback?.Invoke(result);
                }

                //Execute.ShowDebugMessage("messages.search result " + result.Messages.Count);
                //callback?.Invoke(result);
            }, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void SearchGlobalAsync(string query, int offsetDate, TLInputPeerBase offsetPeer, int offsetId, int limit, Action<TLMessagesMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            TLUtils.WriteLine(string.Format("{0} messages.searchGlobal query={1} offset_date={2} offset_peer={3} offset_id={4} limit={5}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), query, offsetDate, offsetPeer, offsetId, limit), LogSeverity.Error);

            var obj = new TLMessagesSearchGlobal { Q = query, OffsetDate = offsetDate, OffsetPeer = offsetPeer, OffsetId = offsetId, Limit = limit };

            const string caption = "messages.searchGlobal";
            SendInformativeMessage<TLMessagesMessagesBase>(caption, obj, result =>
            {
                if (result is ITLMessages messages)
                {
                    TLUtils.WriteLine(string.Format("{0} messages.searchGlobal result={1}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), messages.Messages.Count), LogSeverity.Error);
                }

                callback?.Invoke(result);
            }, faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void ReadHistoryAsync(TLInputPeerBase peer, int maxId, int offset, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesReadHistory { Peer = peer, MaxId = maxId };

            const string caption = "messages.readHistory";
            ReadHistoryAsyncInternal(obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        _updatesService.SetState(null, result.Pts, null, null, null, caption);
                    }

                    callback?.Invoke(result);
                },
                () => { },
                faultCallback);
        }

        public void ReadMessageContentsAsync(TLVector<int> id, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesReadMessageContents { Id = id };

            const string caption = "messages.readMessageContents";
            SendInformativeMessage<TLMessagesAffectedMessages>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        _updatesService.SetState(null, result.Pts, null, null, null, caption);
                    }

                    callback?.Invoke(result);
                },
                /*() => { },*/
                faultCallback);
        }

        public void DeleteHistoryAsync(bool justClear, TLInputPeerBase peer, int maxId, Action<TLMessagesAffectedHistory> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesDeleteHistory { IsJustClear = justClear, Peer = peer, MaxId = maxId };

            const string caption = "messages.deleteHistory";
            SendInformativeMessage<TLMessagesAffectedHistory>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        // TODO: Verify Value.PtsCount, before was Seq.
                        _updatesService.SetState(null, result.Pts, null, null, null, caption);
                    }

                    callback(result);
                },
                faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void DeleteMessagesAsync(TLVector<int> id, bool revoke, Action<TLMessagesAffectedMessages> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesDeleteMessages { Id = id, IsRevoke = revoke };

            const string caption = "messages.deleteMessages";
            SendInformativeMessage<TLMessagesAffectedMessages>(caption, obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        _updatesService.SetState(null, result.Pts, null, null, null, caption);
                    }

                    callback(result);
                },
                faultCallback);
        }

        public void ReceivedMessagesAsync(int maxId, Action<TLVector<TLReceivedNotifyMessage>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesReceivedMessages { MaxId = maxId };

            const string caption = "messages.receivedMessages";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void ForwardMessageAsync(TLInputPeerBase peer, int fwdMessageId, TLMessage message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesForwardMessage { Peer = peer, Id = fwdMessageId, RandomId = message.RandomId ?? 0 };

            const string caption = "messages.forwardMessage";
            ForwardMessageAsyncInternal(obj,
                result =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        message.State = TLMessageState.Confirmed;

                        // TODO: 24/04/2017 verify if this is really needed
                        if (message.Media is TLMessageMediaPhoto photoMedia)
                        {
                            photoMedia.Photo.LastProgress = 0.0;
                            photoMedia.Photo.DownloadingProgress = 0.0;
                        }
                        else if (message.Media is TLMessageMediaDocument documentMedia)
                        {
                            documentMedia.Document.LastProgress = 0.0;
                            documentMedia.Document.DownloadingProgress = 0.0;
                        }
                    });

                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, new List<TLMessage> { message });
                    }

                    callback?.Invoke(result);
                },
                () =>
                {

                },
                faultCallback);
        }

        public void ForwardMessagesAsync(TLInputPeerBase toPeer, TLInputPeerBase fromPeer, TLVector<int> id, IList<TLMessage> messages, bool withMyScore, bool grouped, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var randomId = new TLVector<long>();
            foreach (var message in messages)
            {
                randomId.Add(message.RandomId.Value);
            }

            //TLInputPeerBase fromPeer = null;
            var message48 = messages.FirstOrDefault() as TLMessage;
            //// TODO: verify
            ////if (message48 != null)
            ////{
            ////    fromPeer = message48.FwdFromChannelPeer;
            ////}

            ////if (fromPeer == null)
            ////{
            ////    var message40 = messages.FirstOrDefault() as TLMessage;
            ////    if (message40 != null)
            ////    {
            ////        fromPeer = message40.FwdFromChannelPeer ?? PeerToInputPeer(message40.FwdFromPeer);
            ////    }
            ////}

            //if (message48.HasFwdFrom)
            //{
            //    fromPeer = message48.Parent.ToInputPeer();

            //    //if (message48.FwdFrom.HasChannelId)
            //    //{
            //    //    fromPeer = PeerToInputPeer(new TLPeerChannel { ChannelId = message48.FwdFrom.ChannelId.Value });
            //    //}
            //    //else if (message48.FwdFrom.HasFromId)
            //    //{
            //    //    fromPeer = PeerToInputPeer(new TLPeerUser { UserId = message48.FwdFrom.FromId.Value });
            //    //}
            //}

            var obj = new TLMessagesForwardMessages { ToPeer = toPeer, Id = id, RandomId = randomId, FromPeer = fromPeer };

            if (message48 != null && message48.IsSilent)
            {
                obj.IsSilent = true;
            }

            if (withMyScore)
            {
                obj.IsWithMyScore = true;
            }

            if (grouped)
            {
                obj.IsGrouped = true;
            }

            const string caption = "messages.forwardMessages";

            Execute.ShowDebugMessage(string.Format(caption + " to_peer={0} from_peer={1} id={2} flags={3}", toPeer, fromPeer, string.Join(",", id), "TLMessagesForwardMessages.ForwardMessagesFlagsString(obj.Flags)"));
            //Execute.ShowDebugMessage(caption + string.Format("id={0} random_id={1} from_peer={2} to_peer={3}", obj.Id.FirstOrDefault(), obj.RandomIds.FirstOrDefault(), obj.FromPeer, obj.ToPeer));

            ForwardMessagesAsyncInternal(obj,
                result =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        for (var i = 0; i < messages.Count; i++)
                        {
                            messages[i].State = TLMessageState.Confirmed;

                            // TODO: 24/04/2017 verify if this is really needed
                            if (messages[i].Media is TLMessageMediaPhoto photoMedia)
                            {
                                photoMedia.Photo.LastProgress = 0.0;
                                photoMedia.Photo.DownloadingProgress = 0.0;
                            }
                            else if (messages[i].Media is TLMessageMediaDocument documentMedia)
                            {
                                documentMedia.Document.LastProgress = 0.0;
                                documentMedia.Document.DownloadingProgress = 0.0;
                            }
                        }
                    });

                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, messages);
                    }

                    callback?.Invoke(result);
                },
                () =>
                {

                },
                faultCallback);
        }

        public void GetChatsAsync(TLVector<int> id, Action<TLMessagesChats> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetChats { Id = id };

            const string caption = "messages.getChats";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetFullChatAsync(int chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetFullChat { ChatId = chatId };

            SendInformativeMessage<TLMessagesChatFull>(
                "messages.getFullChat", obj,
                messagesChatFull =>
                {
                    _cacheService.SyncChat(messagesChatFull, result => callback?.Invoke(messagesChatFull));
                },
                faultCallback);
        }

        public void EditChatTitleAsync(int chatId, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesEditChatTitle { ChatId = chatId, Title = title };

            const string caption = "messages.editChatTitle";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void EditChatPhotoAsync(int chatId, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesEditChatPhoto { ChatId = chatId, Photo = photo };

            const string caption = "messages.editChatPhoto";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null, true);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void AddChatUserAsync(int chatId, TLInputUserBase userId, int fwdLimit, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesAddChatUser { ChatId = chatId, UserId = userId, FwdLimit = fwdLimit };

            const string caption = "messages.addChatUser";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void DeleteChatUserAsync(int chatId, TLInputUserBase userId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesDeleteChatUser { ChatId = chatId, UserId = userId };

            const string caption = "messages.deleteChatUser";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.InvokeAfter);
        }

        public void CreateChatAsync(TLVector<TLInputUserBase> users, string title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesCreateChat { Users = users, Title = title };

            const string caption = "messages.createChat";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void ExportChatInviteAsync(int chatId, Action<TLExportedChatInviteBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesExportChatInvite { ChatId = chatId };

            const string caption = "messages.exportChatInvite";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void CheckChatInviteAsync(string hash, Action<TLChatInviteBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesCheckChatInvite { Hash = hash };

            const string caption = "messages.checkChatInvite";
            SendInformativeMessage<TLChatInviteBase>(caption, obj,
                result =>
                {
                    if (result is TLChatInvite chatInvite)
                    {
                        _cacheService.SyncUsers(chatInvite.Participants, participants =>
                        {
                            chatInvite.Participants = participants;
                            callback?.Invoke(result);
                        });
                    }
                    else if (result is TLChatInviteAlready chatInviteAlready)
                    {
                        _cacheService.SyncUsersAndChats(new TLVector<TLUserBase>(), new TLVector<TLChatBase> { chatInviteAlready.Chat }, tuple =>
                        {
                            chatInviteAlready.Chat = tuple.Item2.FirstOrDefault() ?? chatInviteAlready.Chat;
                            callback?.Invoke(result);
                        });
                    }
                    else
                    {
                        callback?.Invoke(result);
                    }
                },
                faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void ImportChatInviteAsync(string hash, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesImportChatInvite { Hash = hash };

            const string caption = "messages.importChatInvite";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var updates = result as TLUpdates;
                    if (updates != null)
                    {
                        _cacheService.SyncUsersAndChats(updates.Users, updates.Chats, tuple => { });
                    }

                    var multiPts = result as ITLMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback, flags: RequestFlag.FailOnServerError);
        }

        public void ToggleChatAdminsAsync(int chatId, bool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesToggleChatAdmins { ChatId = chatId, Enabled = enabled };

            const string caption = "messages.toggleChatAdmins";
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
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void EditChatAdminAsync(int chatId, TLInputUserBase userId, bool isAdmin, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesEditChatAdmin { ChatId = chatId, UserId = userId, IsAdmin = isAdmin };

            const string caption = "messages.editChatAdmin";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetCommonChatsAsync(TLInputUserBase id, int maxId, int limit, Action<TLMessagesChatsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetCommonChats { UserId = id, MaxId = maxId, Limit = limit };

            const string caption = "messages.getCommonChats";
            SendInformativeMessage<TLMessagesChatsBase>(caption, obj,
                result =>
                {
                    _cacheService.SyncUsersAndChats(new TLVector<TLUserBase>(), result.Chats, tuple =>
                    {
                        result.Chats = tuple.Item2;

                        callback?.Invoke(result);
                    });
                },
                faultCallback);
        }

        public void ToggleDialogPinAsync(TLInputPeerBase peer, bool pin, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesToggleDialogPin { Peer = peer, IsPinned = pin };

            const string caption = "messages.toggleDialogPin";
            SendInformativeMessage<bool>(caption, obj,
                result =>
                {
                    var dialog = _cacheService.GetDialog(peer.ToPeer());
                    if (dialog != null)
                    {
                        dialog.IsPinned = pin;
                        dialog.RaisePropertyChanged(() => dialog.IsPinned);
                        _cacheService.Commit();
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        public void ReorderPinnedDialogsAsync(TLVector<TLInputPeerBase> order, bool force, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesReorderPinnedDialogs { Order = order, IsForce = force };

            const string caption = "messages.reorderPinnedDialogs";
            SendInformativeMessage<bool>(caption, obj, callback, faultCallback);
        }

        public void HideReportSpamAsync(TLInputPeerBase peer, Action<bool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesHideReportSpam { Peer = peer };

            const string caption = "messages.hideReportSpam";
            SendInformativeMessage<bool>(caption, obj, callback, faultCallback);
        }

        public void GetPeerSettingsAsync(TLInputPeerBase peer, Action<TLPeerSettings> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesGetPeerSettings { Peer = peer };

            const string caption = "messages.getPeerSettings";
            SendInformativeMessage<TLPeerSettings>(caption, obj, callback, faultCallback);
        }

        public void MigrateChatAsync(int chatId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesMigrateChat { ChatId = chatId };

            const string caption = "messages.migrateChat";
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
                        ProcessUpdates(result, null);
                    }

                    callback?.Invoke(result);
                },
                faultCallback);
        }
    }
}
