//#define DEBUG_READ_HISTORY

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Methods.Account;
using Telegram.Api.TL.Methods.Messages;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public Task<MTProtoResponse<bool>> ReportPeerAsync(TLInputPeerBase peer, TLReportReasonBase reason)
        {
#if DEBUG
            return Task.FromResult(new MTProtoResponse<bool>(true));
#endif

            return SendInformativeMessage<bool>("account.reportPeer", new TLAccountReportPeer { Peer = peer, Reason = reason });
        }

        public Task<MTProtoResponse<bool>> ReportSpamAsync(TLInputPeerBase peer)
        {
#if DEBUG
            return Task.FromResult(new MTProtoResponse<bool>(true));
#endif

            return SendInformativeMessage<bool>("messages.reportSpam", new TLMessagesReportSpam { Peer = peer });
        }

        public Task<MTProtoResponse<TLMessageMediaBase>> GetWebPagePreviewAsync(string message)
        {
            var obj = new TLMessagesGetWebPagePreview { Message = message };

            const string caption = "messages.getWebPagePreview";
            return SendInformativeMessage<TLMessageMediaBase>(caption, obj);
        }

        public async Task<MTProtoResponse<TLMessagesAllStickersBase>> GetAllStickersAsync(int hash)
        {
            MTProtoResponse<TLMessagesAllStickersBase> result = null;

            var obj = new TLMessagesGetAllStickers { Hash = hash };

            const string caption = "messages.getAllStickers";

            //Execute.ShowDebugMessage(caption + " hash=" + hash);
            //SendInformativeMessage(caption, obj, callback, faultCallback);
            //return;

            var results = new List<TLMessagesStickerSet>();
            var resultsSyncRoot = new object();
            var stopwatch = Stopwatch.StartNew();
            var allStickersBase = await SendInformativeMessage<TLMessagesAllStickersBase>(caption, obj);
            if (allStickersBase.Error == null)
            {
                var allStickers32 = allStickersBase.Value as TLMessagesAllStickers;
                if (allStickers32 != null)
                {
                    var stickerSetResult = await GetAllStickerSetsAsync(allStickers32);
                    if (stickerSetResult.Error == null)
                    {

                        //var messagesStickerSet = stickerSetResult.Value as TLMessagesStickerSet;
                        //if (messagesStickerSet != null)
                        //{
                        //    bool processStickerSets;
                        //    lock (resultsSyncRoot)
                        //    {
                        //        results.Add(messagesStickerSet);
                        //        processStickerSets = results.Count == allStickers32.Sets.Count;
                        //    }

                        //    if (processStickerSets)
                        //    {
                        //        ProcessStickerSets(allStickers32, results);
                        //    }
                        //}
                    }
                }
                else
                {
                    result = new MTProtoResponse<TLMessagesAllStickersBase>(null, allStickersBase.Error);
                }
            }

            return result;
        }

        private static void ProcessStickerSets(TLMessagesAllStickers allStickers32, List<TLMessagesStickerSet> results)
        {
            var documentsDict = new Dictionary<long, TLDocumentBase>();
            var packsDict = new Dictionary<string, TLStickerPack>();
            foreach (var result in results)
            {
                foreach (var pack in result.Packs)
                {
                    var emoticon = pack.Emoticon.ToString();
                    TLStickerPack currentPack;
                    if (packsDict.TryGetValue(emoticon, out currentPack))
                    {
                        var docDict = new Dictionary<long, long>();
                        foreach (var document in currentPack.Documents)
                        {
                            docDict[document] = document;
                        }
                        foreach (var document in pack.Documents)
                        {
                            if (!docDict.ContainsKey(document))
                            {
                                docDict[document] = document;
                                currentPack.Documents.Add(document);
                            }
                        }
                    }
                    else
                    {
                        packsDict[emoticon] = pack;
                    }
                }

                foreach (var document in result.Documents)
                {
                    documentsDict[document.Id] = document;
                }
            }
            // TODO
            //allStickers32.Packs = new TLVector<TLStickerPack>();
            //foreach (var pack in packsDict.Values)
            //{
            //    allStickers32.Packs.Add(pack);
            //}
            //allStickers32.Documents = new TLVector<TLDocumentBase>();
            //foreach (var document in documentsDict.Values)
            //{
            //    allStickers32.Documents.Add(document);
            //}
        }


        private Task<MTProtoResponse<TLMessagesAllStickersBase>> GetAllStickerSetsAsync(TLMessagesAllStickers allStickers32)
        {
            var callback = new TaskCompletionSource<MTProtoResponse>();
            var container = new TLMessageContainer { Messages = new List<TLContainerTransportMessage>() };
            var historyItems = new List<HistoryItem>();
            for (var i = 0; i < allStickers32.Sets.Count; i++)
            {
                var set = allStickers32.Sets[i];
                var obj = new TLMessagesGetStickerSet { Stickerset = new TLInputStickerSetID { Id = set.Id, AccessHash = set.AccessHash } };
                int sequenceNumber;
                long messageId;
                lock (_activeTransportRoot)
                {
                    sequenceNumber = _activeTransport.SequenceNumber * 2 + 1;
                    _activeTransport.SequenceNumber++;
                    messageId = _activeTransport.GenerateMessageId(true);
                }

                var data = i > 0 ? (TLObject)new TLInvokeAfterMsg { MsgId = container.Messages[i - 1].MsgId, Query = obj } : obj;

                var transportMessage = new TLContainerTransportMessage
                {
                    MsgId = messageId,
                    SeqNo = sequenceNumber,
                    Query = data
                };

                var historyItem = new HistoryItem
                {
                    SendTime = DateTime.Now,
                    Caption = "stickers.containerGetStickerSetPart" + i,
                    Object = obj,
                    Callback = callback,
                    Message = transportMessage,
                    AttemptFailed = null,
                    ClientTicksDelta = ClientTicksDelta,
                    Status = RequestStatus.Sent,
                };
                historyItems.Add(historyItem);

                container.Messages.Add(transportMessage);
            }


            lock (_historyRoot)
            {
                foreach (var historyItem in historyItems)
                {
                    _history[historyItem.Hash] = historyItem;
                }
            }
#if DEBUG
            NotifyOfPropertyChange(() => History);
#endif

            return SendNonInformativeMessage<TLMessagesAllStickersBase>("stickers.container", container, callback);
        }

        public Task<MTProtoResponse<TLMessagesStickerSet>> GetStickerSetAsync(TLInputStickerSetBase stickerset)
        {
            return SendInformativeMessage<TLMessagesStickerSet>("messages.getStickerSet", new TLMessagesGetStickerSet { Stickerset = stickerset });
        }

        public Task<MTProtoResponse<bool>> InstallStickerSetAsync(TLInputStickerSetBase stickerset)
        {
            return SendInformativeMessage<bool>("messages.installStickerSet", new TLMessagesInstallStickerSet { Stickerset = stickerset, Archived = false });
        }

        public Task<MTProtoResponse<bool>> UninstallStickerSetAsync(TLInputStickerSetBase stickerset)
        {
            return SendInformativeMessage<bool>("messages.uninstallStickerSet", new TLMessagesUninstallStickerSet { Stickerset = stickerset });
        }

        private static TLMessageState GetMessageStatus(ICacheService cacheService, TLPeerBase peer)
        {
            var status = TLMessageState.Confirmed;
            if (peer is TLPeerUser)
            {
                var user = cacheService.GetUser(peer.Id);
                if (user != null)
                {
                    var botInfo = user.BotInfo as TLBotInfo;
                    if (botInfo != null)
                    {
                        status = TLMessageState.Read;
                    }
                }
            }

            return status;
        }

        // TODO
        private TLInputPeerBase PeerToInputPeer(TLMessageFwdHeader peer)
        {
            if (peer.HasChannelId)
            {
                var channel = _cacheService.GetChat(peer.ChannelId.Value) as TLChannel;
                if (channel != null)
                {
                    return new TLInputPeerChannel { ChannelId = peer.ChannelId.Value, AccessHash = channel.AccessHash.Value };
                }
            }

            if (peer.HasFromId)
            {
                var cachedUser = _cacheService.GetUser(peer.FromId.Value);
                if (cachedUser != null)
                {
                    //var userForeign = cachedUser as TLUserForeign;
                    //var userRequest = cachedUser as TLUserRequest;
                    var user = cachedUser as TLUser;

                    //if (userForeign != null)
                    //{
                    //    return new TLInputPeerForeign { UserId = userForeign.Id, AccessHash = userForeign.AccessHash };
                    //}

                    //if (userRequest != null)
                    //{
                    //    return new TLInputPeerForeign { UserId = userRequest.Id, AccessHash = userRequest.AccessHash };
                    //}

                    if (user != null)
                    {
                        return user.ToInputPeer();
                    }

                    //return new TLInputPeerContact { UserId = peer.Id };
                    return new TLInputPeerUser { UserId = peer.FromId.Value };
                }

                //return new TLInputPeerContact { UserId = peer.Id };
                return new TLInputPeerUser { UserId = peer.FromId.Value };
            }

            //return new TLInputPeerContact { UserId = peer.Id };
            return new TLInputPeerUser { UserId = peer.FromId.Value };
        }

        // TODO
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
                    //    return new TLInputPeerForeign { UserId = userForeign.Id, AccessHash = userForeign.AccessHash };
                    //}

                    //if (userRequest != null)
                    //{
                    //    return new TLInputPeerForeign { UserId = userRequest.Id, AccessHash = userRequest.AccessHash };
                    //}

                    if (user != null)
                    {
                        return user.ToInputPeer();
                    }

                    //return new TLInputPeerContact { UserId = peer.Id };
                    return new TLInputPeerUser { UserId = peer.Id };
                }

                //return new TLInputPeerContact { UserId = peer.Id };
                return new TLInputPeerUser { UserId = peer.Id };
            }

            if (peer is TLPeerChannel)
            {
                var channel = _cacheService.GetChat(peer.Id) as TLChannel;
                if (channel != null)
                {
                    return new TLInputPeerChannel { ChannelId = peer.Id, AccessHash = channel.AccessHash.Value };
                }
                else
                {
                    // TODO:
                    return new TLInputPeerChannel { ChannelId = peer.Id };
                }
            }

            if (peer is TLPeerChat)
            {
                return new TLInputPeerChat { ChatId = peer.Id };
            }

            //return new TLInputPeerBroadcast { ChatId = peer.Id };
            return new TLInputPeerUser { UserId = peer.Id };
        }

        public async Task<MTProtoResponse<TLMessage>> SendMessageAsync(TLMessage message)
        {
            MTProtoResponse<TLMessage> resultMessage = null;
            var inputPeer = PeerToInputPeer(message.ToId);
            var obj = new TLMessagesSendMessage { Peer = inputPeer, ReplyToMsgId = message.ReplyToMsgId, Message = message.Message, RandomId = message.RandomId.Value };

            // TODO:
            //if (message.DisableWebPagePreview)
            //{
            //    obj.IsNoWebpage = true;
            //}

            if (_deviceInfo.IsBackground)
            {
                obj.IsBackground = true;
            }

            if (obj.ReplyToMsgId != null)
            {
                obj.HasReplyToMsgId = true;
            }

            //if (!message.HasFromId || message.FromId <= 0) // IsChannelMessage
            //{
            //    obj.IsBroadcast = true;
            //}

            const string caption = "messages.sendMessage";
            var result = await SendMessageAsyncInternal(obj);
            if (result.IsSucceeded)
            {
                var multiPts = result as ITLMultiPts;
                var shortSentMessage = result.Value as TLUpdateShortSentMessage;
                if (shortSentMessage != null)
                {
                    message.Flags = (TLMessage.Flag)(int)shortSentMessage.Flags;
                    if (shortSentMessage.HasMedia)
                    {
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

                    message.Id = shortSentMessage.Id;

                    var task = new TaskCompletionSource<MTProtoResponse<TLMessage>>();
                    _cacheService.SyncSendingMessage(message, null, message.ToId, (callback) => task.SetResult(new MTProtoResponse<TLMessage>(callback)));
                    await task.Task;
                    //return new MTProtoResponse<TLMessage>(message);
                }

                var updates = result.Value as TLUpdates;
                if (updates != null)
                {
                    foreach (var update in updates.Updates)
                    {
                        var updateNewMessage = update as TLUpdateNewMessage;
                        if (updateNewMessage != null)
                        {
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
                        // TODO: notifyNewMessages?
                        ProcessUpdates(result.Value, new[] { message });
                    }

                    resultMessage = new MTProtoResponse<TLMessage>(message);
                }
            }

            return resultMessage;
        }

        private void ProcessUpdates(TLUpdatesBase updatesBase, IList<TLMessage> messages)
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
                var updateMessageIdList = new List<TLUpdateMessageID>();
                for (var i = 0; i < updates.Updates.Count; i++)
                {
                    var updateNewMessage = updates.Updates[i] as TLUpdateNewMessage;
                    if (updateNewMessage != null)
                    {
                        updateNewMessageIndex[updateNewMessage.Message.Id] = updateNewMessage;
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
                        updateNewMessage.Message.RandomId = updateMessageId.RandomId;
                    }

                    TLMessage message;
                    if (messagesRandomIndex.TryGetValue(updateMessageId.RandomId, out message))
                    {
                        message.Id = updateMessageId.Id;
                        if (updateNewMessage != null)
                        {
                            var messageCommon = updateNewMessage.Message as TLMessage;
                            if (messageCommon != null)
                            {
                                message.Date = messageCommon.Date;
                            }
                        }
                    }
                }

                _updatesService.ProcessUpdates(updates);
            }
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> StartBotAsync(TLInputUserBase bot, string startParam, TLMessage message)
        {
            // TODO: Verify parameters
            //var obj = new TLMessagesStartBot { Bot = bot, ChatId = message.ToId is TLPeerChat ? message.ToId.Id : new int?(0), RandomId = message.RandomId, StartParam = startParam };
            var obj = new TLMessagesStartBot { Bot = bot, Peer = message.ToId is TLPeerChat ? (TLInputPeerBase)new TLInputPeerChat { ChatId = message.ToId.Id } : new TLInputPeerEmpty(), RandomId = message.RandomId.Value, StartParam = startParam };

            const string caption = "messages.startBot";
            var result = await StartBotAsyncInternal(obj);
            if (result.Error == null)
            {

                Execute.BeginOnUIThread(() =>
                {
                    message.State = GetMessageStatus(_cacheService, message.ToId);
                    //message.Media.LastProgress = 0.0;
                    //message.Media.DownloadingProgress = 0.0;
                });

                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, new List<TLMessage> { message });
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> SendMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage message)
        {
            var obj = new TLMessagesSendMedia { Peer = inputPeer, ReplyToMsgId = message.ReplyToMsgId, Media = inputMedia, RandomId = message.RandomId.Value };

            //if (message.FromId <= 0) // IsChannelMessage
            //{
            //    obj.IsBroadcast = true;
            //}

            const string caption = "messages.sendMedia";
            var result = await SendMediaAsyncInternal(obj);
            if (result.Error == null)
            {

                Execute.BeginOnUIThread(() =>
                {
                    message.State = GetMessageStatus(_cacheService, message.ToId);
                    //message.Media.LastProgress = 0.0;
                    //message.Media.DownloadingProgress = 0.0;
                });

                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, new List<TLMessage> { message });
                }
            }

            return result;
        }

        // NO MORE SUPPORTED:
        //public async Task<MTProtoResponse<TLUpdatesBase>> SendBroadcastAsync(TLVector<TLInputUserBase> contacts, TLInputMediaBase inputMedia, TLMessage message)
        //{
        //    var randomId = new TLVector<long?>();
        //    for (var i = 0; i < contacts.Count; i++)
        //    {
        //        randomId.Add(TLLong.Random());
        //    }

        //    var obj = new TLSendBroadcast { Contacts = contacts, RandomId = randomId, Message = message.Message, Media = inputMedia };

        //    const string caption = "messages.sendBroadcast";
        //    var result = await SendInformativeMessage<TLUpdatesBase>(caption, obj);
        //    if (result.Error == null)
        //    {

        //        var multiPts = result as ITLMultiPts;
        //        if (multiPts != null)
        //        {
        //            _updatesService.SetState(multiPts, caption);
        //        }
        //        else
        //        {
        //            ProcessUpdates(result.Value, new List<TLMessage25> { message });
        //        }

        //        var updates = result.Value as TLUpdates;
        //        if (updates != null)
        //        {
        //            var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
        //            if (updateNewMessage != null)
        //            {
        //                var messageCommon = updateNewMessage.Message as TLMessageCommon;
        //                if (messageCommon != null)
        //                {
        //                    message.Date = new int?(messageCommon.DateIndex - 1); // Делаем бродкаст после всех чатов, в которые отправили, в списке диалогов
        //                }
        //            }
        //        }

        //        //message.Id = result.Id;
        //        message.Status = MessageStatus.Confirmed;
        //    }

        //    return result;
        //}



        public Task<MTProtoResponse<TLMessagesSentEncryptedMessage>> SendEncryptedAsync(TLInputEncryptedChat peer, long randomId, byte[] data)
        {
            var obj = new TLMessagesSendEncrypted { Peer = peer, RandomId = randomId, Data = data };

            return SendEncryptedAsyncInternal(obj);
        }


        public Task<MTProtoResponse<TLMessagesSentEncryptedFile>> SendEncryptedFileAsync(TLInputEncryptedChat peer, long randomId, byte[] data, TLInputEncryptedFileBase file)
        {
            var obj = new TLMessagesSendEncryptedFile { Peer = peer, RandomId = randomId, Data = data, File = file };

            return SendEncryptedFileAsyncInternal(obj);
        }

        public Task<MTProtoResponse<TLMessagesSentEncryptedMessage>> SendEncryptedServiceAsync(TLInputEncryptedChat peer, long randomId, byte[] data)
        {
            var obj = new TLMessagesSendEncryptedService { Peer = peer, RandomId = randomId, Data = data };

            return SendEncryptedServiceAsyncInternal(obj);
        }
        public async void SendEncryptedServiceCallbackAsync(TLInputEncryptedChat peer, long randomId, byte[] data, Action<TLMessagesSentEncryptedMessage> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLMessagesSendEncryptedService { Peer = peer, RandomId = randomId, Data = data };

            var result = await SendEncryptedServiceAsyncInternal(obj);
            if (result?.IsSucceeded == true)
            {
                callback?.Invoke(result.Value);
            }
            else
            {
                faultCallback?.Invoke(result?.Error);
            }
        }


        public Task<MTProtoResponse<bool>> ReadEncryptedHistoryAsync(TLInputEncryptedChat peer, int maxDate)
        {
            var obj = new TLMessagesReadEncryptedHistory { Peer = peer, MaxDate = maxDate };

            return ReadEncryptedHistoryAsyncInternal(obj);
        }

        public Task<MTProtoResponse<bool>> SetEncryptedTypingAsync(TLInputEncryptedChat peer, bool typing)
        {
            var obj = new TLMessagesSetEncryptedTyping { Peer = peer, Typing = typing };

            return SendInformativeMessage<bool>("messages.setEncryptedTyping", obj);
        }

        public Task<MTProtoResponse<bool>> SetTypingAsync(TLInputPeerBase peer, bool typing)
        {
            var action = typing ? (TLSendMessageActionBase)new TLSendMessageTypingAction() : new TLSendMessageCancelAction();
            var obj = new TLMessagesSetTyping { Peer = peer, Action = action };

            return SendInformativeMessage<bool>("messages.setTyping", obj);
        }

        public Task<MTProtoResponse<bool>> SetTypingAsync(TLInputPeerBase peer, TLSendMessageActionBase action)
        {
            var obj = new TLMessagesSetTyping { Peer = peer, Action = action ?? new TLSendMessageTypingAction() };

            return SendInformativeMessage<bool>("messages.setTyping", obj);
        }

        public Task<MTProtoResponse<TLMessagesMessagesBase>> GetMessagesAsync(TLVector<int> id)
        {
            var obj = new TLMessagesGetMessages { Id = id };

            return SendInformativeMessage<TLMessagesMessagesBase>("messages.getMessages", obj);
        }

#if LAYER_40
        public async Task<MTProtoResponse<TLMessagesDialogsBase>> GetDialogsAsync(int offsetDate, int offsetId, TLInputPeerBase peer, int limit)
        {
            var obj = new TLMessagesGetDialogs { OffsetDate = offsetDate, OffsetId = offsetId, OffsetPeer = peer, Limit = limit };

            var result = await SendInformativeMessage<TLMessagesDialogsBase>("messages.getDialogs", obj);
            if (result.Error == null)
            {
                var task = new TaskCompletionSource<MTProtoResponse<TLMessagesDialogsBase>>();
                _cacheService.SyncDialogs(result.Value, (callback) =>
                {
                    task.SetResult(new MTProtoResponse<TLMessagesDialogsBase>(callback));
                });
                return await task.Task;
            }

            return result;
        }
#else
        public async Task<MTProtoResponse<TLResPQ>> GetDialogsAsync(int? offset, int? maxId, int? limit, Action<TLDialogsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetDialogs { Offset = offset, MaxId = maxId, Limit = limit };

            SendInformativeMessage<TLDialogsBase>("messages.getDialogs", obj, result =>_cacheService.SyncDialogs(result, callback), faultCallback);
        }
#endif

        // TODO:
        //public async Task<MTProtoResponse<TLMessagesDialogsBase>> GetChannelDialogsAsync(int offset, int limit)
        //{
        //    var obj = new TLChannelsGetDialogs { Offset = offset, Limit = limit };

        //    var result = await SendInformativeMessage<TLMessagesDialogsBase>("channels.getDialogs", obj);
        //    if (result.Error == null)
        //    {

        //        //return;
        //        var channelsCache = new Context<TLChannel>();
        //        foreach (var chatBase in result.Value.Chats)
        //        {
        //            var channel = chatBase as TLChannel;
        //            if (channel != null)
        //            {
        //                channelsCache[channel.Id] = channel;
        //            }
        //        }

        //        var dialogsCache = new Context<TLDialogChannel>();
        //        foreach (var dialogBase in result.Value.Dialogs)
        //        {
        //            var dialogChannel = dialogBase as TLDialogChannel;
        //            if (dialogChannel != null)
        //            {
        //                var channelId = dialogChannel.Peer.Id;
        //                dialogsCache[channelId] = dialogChannel;
        //                TLChannel channel;
        //                if (channelsCache.TryGetValue(channelId, out channel))
        //                {
        //                    channel.ReadInboxMaxId = dialogChannel.ReadInboxMaxId;
        //                    //channel.UnreadCount = dialogChannel.UnreadCount;
        //                    //channel.UnreadImportantCount = dialogChannel.UnreadImportantCount;
        //                    channel.NotifySettings = dialogChannel.NotifySettings;
        //                    //channel.Pts = dialogChannel.Pts;
        //                }
        //            }
        //        }

        //        //_cacheService.SyncChannelDialogs(result, callback);
        //        _cacheService.SyncUsersAndChats(result.Value.Users, result.Value.Chats, x =>
        //        {
        //            _cacheService.MergeMessagesAndChannels(result.Value);
        //        });
        //    }

        //    return result;
        //}


        private void GetHistoryAsyncInternal(bool sync, TLPeerBase peer, TLMessagesMessagesBase result)
        {
            if (sync)
            {
                _cacheService.SyncMessages(result, peer, false, true, null);
            }
            else
            {
                _cacheService.AddChats(result.Chats, results => { });
                _cacheService.AddUsers(result.Users, results => { });
            }
        }

        public async Task<MTProtoResponse<TLMessagesMessagesBase>> GetHistoryAsync(string debugInfo, TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, int offset, int maxId, int limit)
        {
            var obj = new TLMessagesGetHistory { Peer = inputPeer, AddOffset = offset, OffsetId = maxId, Limit = limit, MaxId = int.MaxValue, MinId = 0 };

            TLUtils.WriteLine(string.Format("{0} {1} messages.getHistory peer={2} offset={3} max_id={4} limit={5}", string.Empty, debugInfo, inputPeer, offset, maxId, limit), LogSeverity.Error);
            var result = await SendInformativeMessage<TLMessagesMessagesBase>("messages.getHistory", obj);
            if (result.Error == null)
            {
                var replyId = new TLVector<int>();
                var waitingList = new List<TLMessage>();
                if (replyId.Count > 0)
                {
                    var messagesResult = await GetMessagesAsync(replyId);
                    if (messagesResult.Error == null)
                    {

                        _cacheService.AddChats(result.Value.Chats, results => { });
                        _cacheService.AddUsers(result.Value.Users, results => { });

                        for (var i = 0; i < messagesResult.Value.Messages.Count; i++)
                        {
                            for (var j = 0; j < waitingList.Count; j++)
                            {
                                var messageToReply = messagesResult.Value.Messages[i] as TLMessage;
                                if (messageToReply != null && messageToReply.Id == waitingList[j].Id)
                                {
                                    waitingList[j].Reply = messageToReply;
                                }
                            }
                        }

                        var inputChannelPeer = inputPeer as TLInputPeerChannel;
                        if (inputChannelPeer != null)
                        {
                            var channel = _cacheService.GetChat(inputChannelPeer.ChannelId) as TLChannel;
                            if (channel != null)
                            {
                                var maxIndex = channel.ReadInboxMaxId != null ? channel.ReadInboxMaxId : 0;
                                foreach (var messageBase in messagesResult.Value.Messages)
                                {
                                    var messageCommon = messageBase as TLMessage;
                                    if (messageCommon != null &&
                                        !messageCommon.IsOut &&
                                        messageCommon.Id > maxIndex)
                                    {
                                        messageCommon.IsUnread = true;
                                    }
                                }
                            }
                        }

                        GetHistoryAsyncInternal(sync, peer, result.Value);
                    }
                }
                else
                {
                    GetHistoryAsyncInternal(sync, peer, result.Value);
                }
            }

            return result;
        }

        public Task<MTProtoResponse<TLMessagesMessagesBase>> SearchAsync(TLInputPeerBase peer, string query, TLMessagesFilterBase filter, int minDate, int maxDate, int offset, int maxId, int limit)
        {
            var obj = new TLMessagesSearch { Peer = peer, Q = query, Filter = filter, MinDate = minDate, MaxDate = maxDate, Offset = offset, MaxId = maxId, Limit = limit };

            return SendInformativeMessage<TLMessagesMessagesBase>("messages.search", obj);
        }

        public async Task<MTProtoResponse<TLMessagesAffectedHistory>> ReadHistoryAsync(TLInputPeerBase peer, int maxId)
        {
            var obj = new TLMessagesReadHistory { Peer = peer, MaxId = maxId };

            const string caption = "messages.readHistory";
            var result = await ReadHistoryAsyncInternal(obj);
            if (result.Error == null)
            {
                var multiPts = result.Value as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    _updatesService.SetState(null, result.Value.Pts, null, null, null, caption);
                }                
            }

            return result;
        }

        public async Task<MTProtoResponse<TLMessagesAffectedMessages>> ReadMessageContentsAsync(TLVector<int> id)
        {
            var obj = new TLMessagesReadMessageContents { Id = id };

            const string caption = "messages.readMessageContents";
            var result = await ReadMessageContentsAsyncInternal(obj);
            if (result.Error == null)
            {
                var multiPts = result.Value as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    _updatesService.SetState(null, result.Value.Pts, null, null, null, caption);
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLMessagesAffectedHistory>> DeleteHistoryAsync(TLInputPeerBase peer, int offset)
        {
            var obj = new TLMessagesDeleteHistory { Peer = peer, MaxId = int.MaxValue };

            const string caption = "messages.deleteHistory";
            var result = await SendInformativeMessage<TLMessagesAffectedHistory>(caption, obj);
            if (result.Error == null)
            {
                var multiPts = result.Value as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    // TODO: Verify Value.PtsCount, before was Seq.
                    _updatesService.SetState(result.Value.PtsCount, result.Value.Pts, null, null, null, caption);
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLMessagesAffectedMessages>> DeleteMessagesAsync(TLVector<int> id)
        {
            var obj = new TLMessagesDeleteMessages { Id = id };

            const string caption = "messages.deleteMessages";
            var result = await SendInformativeMessage<TLMessagesAffectedMessages>(caption, obj);
            if (result.Error == null)
            {

                var multiPts = result.Value as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    _updatesService.SetState(null, result.Value.Pts, null, null, null, caption);
                }
            }

            return result;
        }

        // TODO: Probably deprecated.
        //public Task<MTProtoResponse<TLVector<int>>> RestoreMessagesAsync(TLVector<int> id)
        //{
        //    var obj = new TLRestoreMessages { Id = id };

        //    return SendInformativeMessage<TLVector<int?>>("messages.restoreMessages", obj);
        //}

        public Task<MTProtoResponse<TLVector<TLReceivedNotifyMessage>>> ReceivedMessagesAsync(int maxId)
        {
            var obj = new TLMessagesReceivedMessages { MaxId = maxId };

            return SendInformativeMessage<TLVector<TLReceivedNotifyMessage>>("messages.receivedMessages", obj);
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> ForwardMessageAsync(TLInputPeerBase peer, int fwdMessageId, TLMessage message)
        {
            var obj = new TLMessagesForwardMessage { Peer = peer, Id = fwdMessageId, RandomId = message.RandomId.Value };

            const string caption = "messages.forwardMessage";
            var result = await ForwardMessageAsyncInternal(obj);
            if (result.Error == null)
            {

                Execute.BeginOnUIThread(() =>
                {
                    message.State = TLMessageState.Confirmed;
                    //message.Media.LastProgress = 0.0;
                    //message.Media.DownloadingProgress = 0.0;
                });

                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, new List<TLMessage> { message });
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> ForwardMessagesAsync(TLInputPeerBase toPeer, TLVector<int> id, IList<TLMessage> messages)
        {
            var randomId = new TLVector<long>();
            foreach (var message in messages)
            {
                randomId.Add(message.RandomId.Value);
            }

            var message40 = messages.FirstOrDefault() as TLMessage;

            var obj = new TLMessagesForwardMessages { ToPeer = toPeer, Id = id, RandomId = randomId, FromPeer = PeerToInputPeer(message40.FwdFrom), Flags = 0 };

            var result = await ForwardMessagesAsyncInternal(obj);
            if (result.Error == null)
            {

                Execute.BeginOnUIThread(() =>
                {
                    for (var i = 0; i < messages.Count; i++)
                    {
                        messages[i].State = TLMessageState.Confirmed;
                        //messages[i].Media.LastProgress = 0.0;
                        //messages[i].Media.DownloadingProgress = 0.0;
                    }
                });

                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, "messages.forwardMessages");
                }
                else
                {
                    ProcessUpdates(result.Value, messages);
                }

            }

            return result;
        }

        public Task<MTProtoResponse<TLMessagesChats>> GetChatsAsync(TLVector<int> id)
        {
            var obj = new TLMessagesGetChats { Id = id };

            return SendInformativeMessage<TLMessagesChats>("messages.getChats", obj);
        }

        public async Task<MTProtoResponse<TLMessagesChatFull>> GetFullChatAsync(int chatId)
        {
            var obj = new TLMessagesGetFullChat { ChatId = chatId };

            var result = await SendInformativeMessage<TLMessagesChatFull>("messages.getFullChat", obj);
            if (result.Error == null)
            {
                var task = new TaskCompletionSource<MTProtoResponse<TLMessagesChatFull>>();
                _cacheService.SyncChat(result.Value, (callback) =>
                {
                    task.SetResult(new MTProtoResponse<TLMessagesChatFull>(callback));
                });
                return await task.Task;
            }

            return result;
        }
        public async void GetFullChatCallbackAsync(int chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback)
        {
            var obj = new TLMessagesGetFullChat { ChatId = chatId };

            var result = await SendInformativeMessage<TLMessagesChatFull>("messages.getFullChat", obj);
            if (result.Error == null)
            {
                _cacheService.SyncChat(result.Value, callback);
            }
            else
            {
                faultCallback(result.Error);
            }
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> EditChatTitleAsync(int chatId, string title)
        {
            var obj = new TLMessagesEditChatTitle { ChatId = chatId, Title = title };

            const string caption = "messages.editChatTitle";
            var result = await SendInformativeMessage<TLUpdatesBase>(caption, obj);
            if (result.Error == null)
            {

                var multiPts = result.Value as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> EditChatPhotoAsync(int chatId, TLInputChatPhotoBase photo)
        {
            var obj = new TLMessagesEditChatPhoto { ChatId = chatId, Photo = photo };

            const string caption = "messages.editChatPhoto";
            var result = await SendInformativeMessage<TLUpdatesBase>(caption, obj);
            if (result.Error == null)
            {

                var multiPts = result.Value as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> AddChatUserAsync(int chatId, TLInputUserBase userId, int fwdLimit)
        {
            var obj = new TLMessagesAddChatUser { ChatId = chatId, UserId = userId, FwdLimit = fwdLimit };

            const string caption = "messages.addChatUser";
            var result = await SendInformativeMessage<TLUpdatesBase>(caption, obj);
            if (result.Error == null)
            {

                var multiPts = result.Value as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> DeleteChatUserAsync(int chatId, TLInputUserBase userId)
        {
            var obj = new TLMessagesDeleteChatUser { ChatId = chatId, UserId = userId };

            const string caption = "messages.deleteChatUser";
            var result = await SendInformativeMessage<TLUpdatesBase>(caption, obj);
            if (result.Error == null)
            {

                var multiPts = result.Value as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> CreateChatAsync(TLVector<TLInputUserBase> users, string title)
        {
            var obj = new TLMessagesCreateChat { Users = users, Title = title };

            const string caption = "messages.createChat";
            var result = await SendInformativeMessage<TLUpdatesBase>(caption, obj);
            if (result.Error == null)
            {

                var multiPts = result.Value as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public Task<MTProtoResponse<TLExportedChatInviteBase>> ExportChatInviteAsync(int chatId)
        {
            var obj = new TLMessagesExportChatInvite { ChatId = chatId };

            return SendInformativeMessage<TLExportedChatInviteBase>("messages.exportChatInvite", obj);
        }

        public Task<MTProtoResponse<TLChatInviteBase>> CheckChatInviteAsync(string hash)
        {
            var obj = new TLMessagesCheckChatInvite { Hash = hash };

            return SendInformativeMessage<TLChatInviteBase>("messages.checkChatInvite", obj);
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> ImportChatInviteAsync(string hash)
        {
            var obj = new TLMessagesImportChatInvite { Hash = hash };

            const string caption = "messages.importChatInvite";
            var result = await SendInformativeMessage<TLUpdatesBase>(caption, obj);
            if (result.Error == null)
            {

                var updates = result.Value as TLUpdates;
                if (updates != null)
                {
                    _cacheService.SyncUsersAndChats(updates.Users, updates.Chats, tuple => { });
                }

                var multiPts = result.Value as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public Task<MTProtoResponse<TLObject>> SendActionsAsync(List<TLObject> actions)
        {
            var callback = new TaskCompletionSource<MTProtoResponse>();
            var container = new TLMessageContainer { Messages = new List<TLContainerTransportMessage>() };
            var historyItems = new List<HistoryItem>();
            for (var i = 0; i < actions.Count; i++)
            {
                var obj = actions[i];
                int sequenceNumber;
                long messageId;
                lock (_activeTransportRoot)
                {
                    sequenceNumber = _activeTransport.SequenceNumber * 2 + 1;
                    _activeTransport.SequenceNumber++;
                    messageId = _activeTransport.GenerateMessageId(true);
                }

                var data = i > 0 ? new TLInvokeAfterMsg { MsgId = container.Messages[i - 1].MsgId, Query = obj } : obj;
                var invokeWithoutUpdates = new TLInvokeWithoutUpdates { Query = data };

                var transportMessage = new TLContainerTransportMessage
                {
                    MsgId = messageId,
                    SeqNo = sequenceNumber,
                    Query = invokeWithoutUpdates
                };


                var historyItem = new HistoryItem
                {
                    SendTime = DateTime.Now,
                    Caption = "messages.containerPart" + i,
                    Object = obj,
                    Message = transportMessage,
                    Callback = callback,
                    //Callback = result => callback(obj, result),
                    AttemptFailed = null,
                    //FaultCallback = faultCallback,
                    ClientTicksDelta = ClientTicksDelta,
                    Status = RequestStatus.Sent,
                };
                historyItems.Add(historyItem);

                container.Messages.Add(transportMessage);
            }


            lock (_historyRoot)
            {
                foreach (var historyItem in historyItems)
                {
                    _history[historyItem.Hash] = historyItem;
                }
            }
#if DEBUG
            NotifyOfPropertyChange(() => History);
#endif

            return SendNonInformativeMessage<TLObject>("messages.container", container, callback);
            // return SendNonInformativeMessage<TLObject>("messages.container", container);
        }

        public async Task<MTProtoResponse<TLUpdatesBase>> ToggleChatAdminsAsync(int chatId, bool enabled)
        {
            var obj = new TLMessagesToggleChatAdmins { ChatId = chatId, Enabled = enabled };

            const string caption = "messages.toggleChatAdmins";
            var result = await SendInformativeMessage<TLUpdatesBase>(caption, obj);
            if (result.Error == null)
            {

                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public Task<MTProtoResponse<bool>> EditChatAdminAsync(int chatId, TLInputUserBase userId, bool isAdmin)
        {
            var obj = new TLMessagesEditChatAdmin { ChatId = chatId, UserId = userId, IsAdmin = isAdmin };

            return SendInformativeMessage<bool>("messages.editChatAdmin", obj);
        }

        // TODO: Probably deprecated
        //public async Task<MTProtoResponse<TLUpdatesBase>> DeactivateChatAsync(int chatId, bool enabled)
        //{
        //    var obj = new TLDeactivateChat { ChatId = chatId, Enabled = enabled };

        //    const string caption = "messages.deactivateChat";
        //    var result = await SendInformativeMessage<TLUpdatesBase>(caption, obj);
        //    if (result.Error == null)
        //    {
        //        var multiPts = result as ITLMultiPts;
        //        if (multiPts != null)
        //        {
        //            _updatesService.SetState(multiPts, caption);
        //        }
        //        else
        //        {
        //            ProcessUpdates(result.Value, null);
        //        }
        //    }

        //    return result;
        //}

        public async Task<MTProtoResponse<TLUpdatesBase>> MigrateChatAsync(int chatId)
        {
            var obj = new TLMessagesMigrateChat { ChatId = chatId };

            const string caption = "messages.migrateChat";
            var result = await SendInformativeMessage<TLUpdatesBase>(caption, obj);
            if (result.Error == null)
            {
                var multiPts = result as ITLMultiPts;
                if (multiPts != null)
                {
                    _updatesService.SetState(multiPts, caption);
                }
                else
                {
                    ProcessUpdates(result.Value, null);
                }
            }

            return result;
        }

        public int SendingMessages
        {
            get
            {
                var result = 0;
                lock (_historyRoot)
                {
                    foreach (var historyItem in _history.Values)
                    {
                        if (historyItem.Caption.StartsWith("messages.containerPart"))
                        {
                            result++;
                            break;
                        }
                    }
                }

                return result;
            }
        }
    }
}
