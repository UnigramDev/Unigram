//#define DEBUG_READ_HISTORY

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Help;
using Telegram.Api.TL.Functions.Messages;

namespace Telegram.Api.Services
{
	public partial class MTProtoService
	{
        public void ReportSpamAsync(TLInputPeerBase peer, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
#if DEBUG
            Execute.BeginOnThreadPool(() => callback.SafeInvoke(TLBool.True));
            return;
#endif

            var obj = new TLReportSpam { Peer = peer };

            const string caption = "messages.reportSpam";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

	    public void GetWebPagePreviewAsync(TLString message, Action<TLMessageMediaBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLGetWebPagePreview { Message = message };

            const string caption = "messages.getWebPagePreview";
            SendInformativeMessage(caption, obj, callback, faultCallback);
	    }

	    public void GetAllStickersAsync(TLString hash, Action<TLAllStickersBase> callback, Action<TLRPCError> faultCallback = null)
	    {
	        var obj = new TLGetAllStickers {Hash = hash};

	        const string caption = "messages.getAllStickers";

	        //Execute.ShowDebugMessage(caption + " hash=" + hash);
	        //SendInformativeMessage(caption, obj, callback, faultCallback);
	        //return;

	        var results = new List<TLMessagesStickerSet>();
	        var resultsSyncRoot = new object();
	        var stopwatch = Stopwatch.StartNew();
	        SendInformativeMessage<TLAllStickersBase>(caption, obj,
	            result =>
	            {
	                var allStickers32 = result as TLAllStickers32;
	                if (allStickers32 != null)
	                {
	                    GetAllStickerSetsAsync(allStickers32, callback,
	                        stickerSetResult =>
	                        {
	                            var messagesStickerSet = stickerSetResult as TLMessagesStickerSet;
	                            if (messagesStickerSet != null)
	                            {
	                                bool processStickerSets;
	                                lock (resultsSyncRoot)
	                                {
	                                    results.Add(messagesStickerSet);
	                                    processStickerSets = results.Count == allStickers32.Sets.Count;
	                                }

	                                if (processStickerSets)
	                                {
                                        ProcessStickerSets(allStickers32, results);

                                        //Execute.ShowDebugMessage(caption + " elapsed=" + stopwatch.Elapsed);
                                        callback.SafeInvoke(allStickers32);
	                                }
	                            }
	                        },
	                        faultCallback);
	                }
	                else
	                {
                        callback.SafeInvoke(result);
	                }
	            });
	    }

	    private static void ProcessStickerSets(TLAllStickers32 allStickers32, List<TLMessagesStickerSet> results)
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
	                        docDict[document.Value] = document.Value;
	                    }
	                    foreach (var document in pack.Documents)
	                    {
	                        if (!docDict.ContainsKey(document.Value))
	                        {
                                docDict[document.Value] = document.Value;
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
	                documentsDict[document.Id.Value] = document;
	            }
	        }
            allStickers32.Packs = new TLVector<TLStickerPack>();
            foreach (var pack in packsDict.Values)
            {
                allStickers32.Packs.Add(pack);
            }
            allStickers32.Documents = new TLVector<TLDocumentBase>();
	        foreach (var document in documentsDict.Values)
	        {
	            allStickers32.Documents.Add(document);
	        }
	    }


	    private void GetAllStickerSetsAsync(TLAllStickers32 allStickers32, Action<TLAllStickersBase> callback, Action<TLObject> getStickerSetCallback, Action<TLRPCError> faultCallback)
	    {
	        var container = new TLContainer { Messages = new List<TLContainerTransportMessage>() };
            var historyItems = new List<HistoryItem>();
            for (var i = 0; i < allStickers32.Sets.Count; i++)
            {
                var set = allStickers32.Sets[i];
                var obj = new TLGetStickerSet { Stickerset = new TLInputStickerSetId { Id = set.Id, AccessHash = set.AccessHash } };
                int sequenceNumber;
                TLLong messageId;
                lock (_activeTransportRoot)
                {
                    sequenceNumber = _activeTransport.SequenceNumber * 2 + 1;
                    _activeTransport.SequenceNumber++;
                    messageId = _activeTransport.GenerateMessageId(true);
                }

                var data = i > 0 ? (TLObject)new TLInvokeAfterMsg { MsgId = container.Messages[i - 1].MessageId, Object = obj } : obj;

                var transportMessage = new TLContainerTransportMessage
                {
                    MessageId = messageId,
                    SeqNo = new TLInt(sequenceNumber),
                    MessageData = data
                };

                var historyItem = new HistoryItem
                {
                    SendTime = DateTime.Now,
                    Caption = "stickers.containerGetStickerSetPart" + i,
                    Object = obj,
                    Message = transportMessage,
                    Callback = getStickerSetCallback,
                    AttemptFailed = null,
                    FaultCallback = faultCallback,
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

            SendNonInformativeMessage<TLObject>("stickers.container", container, result => callback(null), faultCallback);
	    }

	    public void GetStickerSetAsync(TLInputStickerSetBase stickerset, Action<TLMessagesStickerSet> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetStickerSet { Stickerset = stickerset };

            const string caption = "messages.getStickerSet";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void InstallStickerSetAsync(TLInputStickerSetBase stickerset, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLInstallStickerSet { Stickerset = stickerset, Disabled = TLBool.False };

            const string caption = "messages.installStickerSet";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void UninstallStickerSetAsync(TLInputStickerSetBase stickerset, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUninstallStickerSet { Stickerset = stickerset };

            const string caption = "messages.uninstallStickerSet";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        private static MessageStatus GetMessageStatus(ICacheService cacheService, TLPeerBase peer)
        {
            var status = MessageStatus.Confirmed;
            if (peer is TLPeerUser)
            {
                var user = cacheService.GetUser(peer.Id);
                if (user != null)
                {
                    var botInfo = user.BotInfo as TLBotInfo;
                    if (botInfo != null)
                    {
                        status = MessageStatus.Read;
                    }
                }
            }

            return status;
        }

        private TLInputPeerBase PeerToInputPeer(TLPeerBase peer)
        {
            if (peer is TLPeerUser)
            {
                var cachedUser = _cacheService.GetUser(peer.Id);
                if (cachedUser != null)
                {
                    var userForeign = cachedUser as TLUserForeign;
                    var userRequest = cachedUser as TLUserRequest;
                    var user = cachedUser as TLUser;

                    if (userForeign != null)
                    {
                        return new TLInputPeerForeign { UserId = userForeign.Id, AccessHash = userForeign.AccessHash };
                    }

                    if (userRequest != null)
                    {
                        return new TLInputPeerForeign { UserId = userRequest.Id, AccessHash = userRequest.AccessHash };
                    }

                    if (user != null)
                    {
                        return user.ToInputPeer();
                    }

                    return new TLInputPeerContact { UserId = peer.Id };
                }

                return new TLInputPeerContact { UserId = peer.Id };
            }

            if (peer is TLPeerChannel)
            {
                var channel = _cacheService.GetChat(peer.Id) as TLChannel;
                if (channel != null)
                {
                    return new TLInputPeerChannel { ChatId = peer.Id, AccessHash = channel.AccessHash };
                }
            }

            if (peer is TLPeerChat)
            {
                return new TLInputPeerChat { ChatId = peer.Id };
            }

            return new TLInputPeerBroadcast { ChatId = peer.Id };
        }

        public void SendMessageAsync(TLMessage36 message, Action<TLMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback = null)
        {
            var inputPeer = PeerToInputPeer(message.ToId);
            var obj = new TLSendMessage { Peer = inputPeer, ReplyToMsgId = message.ReplyToMsgId, Message = message.Message, RandomId = message.RandomId };

            if (message.DisableWebPagePreview)
            {
                obj.DisableWebPagePreview();
            }

            if (message.IsChannelMessage)
            {
                obj.SetChannelMessage();
            }

            const string caption = "messages.sendMessage";
            SendMessageAsyncInternal(obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    var shortSentMessage = result as TLUpdatesShortSentMessage;
                    if (shortSentMessage != null)
                    {
                        message.Flags = shortSentMessage.Flags;
                        if (shortSentMessage.HasMedia)
                        {
                            message._media = shortSentMessage.Media;
                        }
                        if (shortSentMessage.HasEntities)
                        {
                            message.Entities = shortSentMessage.Entities;
                        }

                        Execute.BeginOnUIThread(() =>
                        {
                            message.Status = GetMessageStatus(_cacheService, message.ToId);
                            message.Date = shortSentMessage.Date;
                            if (shortSentMessage.Media is TLMessageMediaWebPage)
                            {
                                message.NotifyOfPropertyChange(() => message.Media);
                            }

#if DEBUG
                            message.Id = shortSentMessage.Id;
                            message.NotifyOfPropertyChange(() => message.Id);
                            message.NotifyOfPropertyChange(() => message.Date);
#endif
                        });

                        _updatesService.SetState(multiPts, caption);

                        message.Id = shortSentMessage.Id;
                        _cacheService.SyncSendingMessage(message, null, message.ToId, callback);
                        return;
                    }

                    var updates = result as TLUpdates;
                    if (updates != null)
                    {
                        foreach (var update in updates.Updates)
                        {
                            var updateNewMessage = update as TLUpdateNewMessage24;
                            if (updateNewMessage != null)
                            {
                                var messageCommon = updateNewMessage.Message as TLMessageCommon;
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
                            message.Status = GetMessageStatus(_cacheService, message.ToId);
                        });

                        if (multiPts != null)
                        {
                            _updatesService.SetState(multiPts, caption);
                        }
                        else
                        {
                            _updatesService.ProcessUpdates(updates);
                        }

                        callback.SafeInvoke(message);
                    }
                },
                fastCallback,
                faultCallback.SafeInvoke);
        }

	    private void ProcessUpdates(TLUpdatesBase updatesBase, IList<TLMessage25> messages)
	    {
	        var updates = updatesBase as TLUpdates;
	        if (updates != null)
	        {
	            var messagesRandomIndex = new Dictionary<long, TLMessage25>();
	            if (messages != null)
                {
                    for (var i = 0; i < messages.Count; i++)
                    {
                        if (messages[i].RandomIndex != 0)
                        {
                            messagesRandomIndex[messages[i].RandomIndex] = messages[i];
                        }
                    }
	            }

	            var updateNewMessageIndex = new Dictionary<long, TLUpdateNewMessage>();
	            var updateMessageIdList = new List<TLUpdateMessageId>();
                for (var i = 0; i < updates.Updates.Count; i++)
                {
                    var updateNewMessage = updates.Updates[i] as TLUpdateNewMessage;
                    if (updateNewMessage != null)
                    {
                        updateNewMessageIndex[updateNewMessage.Message.Index] = updateNewMessage;
                        continue;
                    }

                    var updateMessageId = updates.Updates[i] as TLUpdateMessageId;
                    if (updateMessageId != null)
                    {
                        updateMessageIdList.Add(updateMessageId);
                        continue;
                    }
                }

	            foreach (var updateMessageId in updateMessageIdList)
	            {
                    TLUpdateNewMessage updateNewMessage;
                    if (updateNewMessageIndex.TryGetValue(updateMessageId.Id.Value, out updateNewMessage))
                    {
                        updateNewMessage.Message.RandomId = updateMessageId.RandomId;
                    }

	                TLMessage25 message;
	                if (messagesRandomIndex.TryGetValue(updateMessageId.RandomId.Value, out message))
	                {
	                    message.Id = updateMessageId.Id;
	                    if (updateNewMessage != null)
	                    {
	                        var messageCommon = updateNewMessage.Message as TLMessageCommon;
	                        if (messageCommon != null)
	                        {
                                message.Date = messageCommon.Date;
	                        }
	                    }
	                }
	            }
	        }

            _updatesService.ProcessUpdates(updates);
	    }

        public void StartBotAsync(TLInputUserBase bot, TLString startParam, TLMessage25 message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLStartBot { Bot = bot, ChatId = message.ToId is TLPeerChat? message.ToId.Id : new TLInt(0), RandomId = message.RandomId, StartParam = startParam };

            const string caption = "messages.startBot";
            StartBotAsyncInternal(obj,
                result =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        message.Status = GetMessageStatus(_cacheService, message.ToId);
                        message.Media.LastProgress = 0.0;
                        message.Media.DownloadingProgress = 0.0;
                    });

                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, new List<TLMessage25> { message });
                    }

                    callback.SafeInvoke(result);
                },
                () =>
                {
                    //TLUtils.WriteLine(caption + " fast result " + message.RandomIndex, LogSeverity.Error);
                    //fastCallback();
                },
                faultCallback.SafeInvoke);
        }

        public void SendMediaAsync(TLInputPeerBase inputPeer, TLInputMediaBase inputMedia, TLMessage25 message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSendMedia { Peer = inputPeer, ReplyToMsgId = message.ReplyToMsgId, Media = inputMedia, RandomId = message.RandomId };

            if (message.IsChannelMessage)
            {
                obj.SetChannelMessage();
            }

            const string caption = "messages.sendMedia";
            SendMediaAsyncInternal(obj,
                result =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        message.Status = GetMessageStatus(_cacheService, message.ToId);
                        message.Media.LastProgress = 0.0;
                        message.Media.DownloadingProgress = 0.0;
                    });

                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, new List<TLMessage25>{message});
                    }

                    callback.SafeInvoke(result);
                },
                () =>
                {
                    //TLUtils.WriteLine(caption + " fast result " + message.RandomIndex, LogSeverity.Error);
                    //fastCallback();
                },
                faultCallback.SafeInvoke);
        }

        public void SendBroadcastAsync(TLVector<TLInputUserBase> contacts, TLInputMediaBase inputMedia, TLMessage25 message, Action<TLUpdatesBase> callback, Action fastCallback, Action<TLRPCError> faultCallback = null)
        {
            var randomId = new TLVector<TLLong>();
            for (var i = 0; i < contacts.Count; i++)
            {
                randomId.Add(TLLong.Random());
            }

            var obj = new TLSendBroadcast { Contacts = contacts, RandomId = randomId, Message = message.Message, Media = inputMedia };



            const string caption = "messages.sendBroadcast";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, new List<TLMessage25> { message });
                    }

                    var updates = result as TLUpdates;
                    if (updates != null)
                    {
                        var updateNewMessage = updates.Updates.FirstOrDefault(x => x is TLUpdateNewMessage) as TLUpdateNewMessage;
                        if (updateNewMessage != null)
                        {
                            var messageCommon = updateNewMessage.Message as TLMessageCommon;
                            if (messageCommon != null)
                            {
                                message.Date = new TLInt(messageCommon.DateIndex - 1); // Делаем бродкаст после всех чатов, в которые отправили, в списке диалогов
                            }
                        }
                    }

                    //message.Id = result.Id;
                    message.Status = MessageStatus.Confirmed;

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }



        public void SendEncryptedAsync(TLInputEncryptedChat peer, TLLong randomId, TLString data, Action<TLSentEncryptedMessage> callback, Action fastCallback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSendEncrypted { Peer = peer, RandomId = randomId, Data = data };

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


	    public void SendEncryptedFileAsync(TLInputEncryptedChat peer, TLLong randomId, TLString data, TLInputEncryptedFileBase file, Action<TLSentEncryptedFile> callback, Action fastCallback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLSendEncryptedFile { Peer = peer, RandomId = randomId, Data = data, File = file };

            SendEncryptedFileAsyncInternal(
                obj,
                callback,
                () =>
                {

                },
                faultCallback);
	    }

	    public void SendEncryptedServiceAsync(TLInputEncryptedChat peer, TLLong randomId, TLString data, Action<TLSentEncryptedMessage> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLSendEncryptedService { Peer = peer, RandomId = randomId, Data = data };

            SendEncryptedServiceAsyncInternal(
                obj,
                callback,
                () =>
                {

                },
                faultCallback);
	    }

	    public void ReadEncryptedHistoryAsync(TLInputEncryptedChat peer, TLInt maxDate, Action<TLBool> callback,
	        Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLReadEncryptedHistory { Peer = peer, MaxDate = maxDate };

            ReadEncryptedHistoryAsyncInternal(obj, callback, () => { }, faultCallback);
	    }

	    public void SetEncryptedTypingAsync(TLInputEncryptedChat peer, TLBool typing, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLSetEncryptedTyping { Peer = peer, Typing = typing };

            SendInformativeMessage("messages.setEncryptedTyping", obj, callback, faultCallback);
	    }

        public void SetTypingAsync(TLInputPeerBase peer, TLBool typing, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var action = typing.Value ? (TLSendMessageActionBase) new TLSendMessageTypingAction() : new TLSendMessageCancelAction();
            var obj = new TLSetTyping { Peer = peer, Action = action };

            SendInformativeMessage("messages.setTyping", obj, callback, faultCallback);
        }

        public void SetTypingAsync(TLInputPeerBase peer, TLSendMessageActionBase action, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSetTyping { Peer = peer, Action = action ?? new TLSendMessageTypingAction() };

            SendInformativeMessage("messages.setTyping", obj, callback, faultCallback);
        }

        public void GetMessagesAsync(TLVector<TLInt> id, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetMessages { Id = id };

            SendInformativeMessage("messages.getMessages", obj, callback, faultCallback);
        }

#if LAYER_40
        public void GetDialogsAsync(TLInt offset, TLInt limit, Action<TLDialogsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetDialogs { Offset = offset, Limit = limit };

            SendInformativeMessage<TLDialogsBase>("messages.getDialogs", obj, result => _cacheService.SyncDialogs(result, callback), faultCallback);
        }
#else
        public void GetDialogsAsync(TLInt offset, TLInt maxId, TLInt limit, Action<TLDialogsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetDialogs { Offset = offset, MaxId = maxId, Limit = limit };

            SendInformativeMessage<TLDialogsBase>("messages.getDialogs", obj, result =>_cacheService.SyncDialogs(result, callback), faultCallback);
        }
#endif

        public void GetChannelDialogsAsync(TLInt offset, TLInt limit, Action<TLDialogsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TL.Functions.Channels.TLGetDialogs { Offset = offset, Limit = limit };

            SendInformativeMessage<TLDialogsBase>("channels.getDialogs", obj, result =>
            {
                //return;
                var channelsCache = new Context<TLChannel>();
                foreach (var chatBase in result.Chats)
                {
                    var channel = chatBase as TLChannel;
                    if (channel != null)
                    {
                        channelsCache[channel.Index] = channel;
                    }
                }

                var dialogsCache = new Context<TLDialogChannel>();
                foreach (var dialogBase in result.Dialogs)
                {
                    var dialogChannel = dialogBase as TLDialogChannel;
                    if (dialogChannel != null)
                    {
                        var channelId = dialogChannel.Peer.Id.Value;
                        dialogsCache[channelId] = dialogChannel;
                        TLChannel channel;
                        if (channelsCache.TryGetValue(channelId, out channel))
                        {
                            channel.ReadInboxMaxId = dialogChannel.ReadInboxMaxId;
                            //channel.UnreadCount = dialogChannel.UnreadCount;
                            //channel.UnreadImportantCount = dialogChannel.UnreadImportantCount;
                            channel.NotifySettings = dialogChannel.NotifySettings;
                            //channel.Pts = dialogChannel.Pts;
                        }
                    }
                }

                //_cacheService.SyncChannelDialogs(result, callback);
                _cacheService.SyncUsersAndChats(result.Users, result.Chats, x =>
                {
                    _cacheService.MergeMessagesAndChannels(result);

                    callback.SafeInvoke(result);
                });
            }, faultCallback);
        }


        private void GetHistoryAsyncInternal(bool sync, TLPeerBase peer, TLMessagesBase result, Action<TLMessagesBase> callback)
	    {
            if (sync)
            {
                _cacheService.SyncMessages(result, peer, false, true, callback);
            }
            else
            {
                _cacheService.AddChats(result.Chats, results => { });
                _cacheService.AddUsers(result.Users, results => { });
                callback(result);
            }
	    }

        public void GetHistoryAsync(string debugInfo, TLInputPeerBase inputPeer, TLPeerBase peer, bool sync, TLInt offset, TLInt maxId, TLInt limit, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = 
#if LAYER_40
                new TLGetHistory { Peer = inputPeer, AddOffset = offset, OffsetId = maxId, Limit = limit, MaxId = new TLInt(int.MaxValue), MinId = new TLInt(0) };
#else
                new TLGetHistory { Peer = inputPeer, Offset = offset, MaxId = maxId, Limit = limit };
#endif
            TLUtils.WriteLine(string.Format("{0} {1} messages.getHistory peer={2} offset={3} max_id={4} limit={5}", string.Empty, debugInfo, inputPeer, offset, maxId, limit), LogSeverity.Error);
            SendInformativeMessage<TLMessagesBase>("messages.getHistory", obj, 
                result =>
                {
                    var replyId = new TLVector<TLInt>();
                    var waitingList = new List<TLMessage25>();
                    //for (var i = 0; i < result.Messages.Count; i++)
                    //{
                    //    var message25 = result.Messages[i] as TLMessage25;
                    //    if (message25 != null 
                    //        && message25.ReplyToMsgId != null 
                    //        && message25.ReplyToMsgId.Value > 0)
                    //    {
                    //        var cachedReply = _cacheService.GetMessage(message25.ReplyToMsgId);
                    //        if (cachedReply != null)
                    //        {
                    //            message25.Reply = cachedReply;
                    //        }
                    //        else
                    //        {
                    //            replyId.Add(message25.ReplyToMsgId);
                    //            waitingList.Add(message25);
                    //        }
                    //    }
                    //}

                    if (replyId.Count > 0)
                    {
                        GetMessagesAsync(
                            replyId, 
                            messagesResult =>
                            {
                                _cacheService.AddChats(result.Chats, results => { });
                                _cacheService.AddUsers(result.Users, results => { });

                                for (var i = 0; i < messagesResult.Messages.Count; i++)
                                {
                                    for (var j = 0; j < waitingList.Count; j++)
                                    {
                                        var messageToReply = messagesResult.Messages[i] as TLMessageCommon;
                                        if (messageToReply != null
                                            && messageToReply.Index == waitingList[j].Index)
                                        {
                                            waitingList[j].Reply = messageToReply;
                                        }
                                    }
                                }

                                var inputChannelPeer = inputPeer as TLInputPeerChannel;
                                if (inputChannelPeer != null)
                                {
                                    var channel = _cacheService.GetChat(inputChannelPeer.ChatId) as TLChannel;
                                    if (channel != null)
                                    {
                                        var maxIndex = channel.ReadInboxMaxId != null ? channel.ReadInboxMaxId.Value : 0;
                                        foreach (var messageBase in messagesResult.Messages)
                                        {
                                            var messageCommon = messageBase as TLMessageCommon;
                                            if (messageCommon != null
                                                && !messageCommon.Out.Value
                                                && messageCommon.Index > maxIndex)
                                            {
                                                messageCommon.SetUnread(TLBool.True);
                                            }
                                        }
                                    }
                                }

                                GetHistoryAsyncInternal(sync, peer, result, callback);
                            },
                            faultCallback);
                    }
                    else
                    {
                        GetHistoryAsyncInternal(sync, peer, result, callback);
                    }
                }, 
                faultCallback);
        }

        public void SearchAsync(TLInputPeerBase peer, TLString query, TLInputMessagesFilterBase filter, TLInt minDate, TLInt maxDate, TLInt offset, TLInt maxId, TLInt limit, Action<TLMessagesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            //TLUtils.WriteLine(string.Format("{0} messages.search query={1} offset={2} limit={3}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), query, offset, limit), LogSeverity.Error);
            //Execute.ShowDebugMessage("messages.search filter=" + filter);

            var obj = new TLSearch { Flags = new TLInt(0), Peer = peer, Query = query, Filter = filter, MinDate = minDate, MaxDate = maxDate, Offset = offset, MaxId = maxId, Limit = limit };
            //obj.SetImportant();

            SendInformativeMessage<TLMessagesBase>("messages.search", obj, result =>
            {
                //Execute.ShowDebugMessage("messages.search result " + result.Messages.Count);
                callback.SafeInvoke(result);
            }, faultCallback);
        }

#if LAYER_41
        public void ReadHistoryAsync(TLInputPeerBase peer, TLInt maxId, TLInt offset, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null)
#else
        public void ReadHistoryAsync(TLInputPeerBase peer, TLInt maxId, TLInt offset, Action<TLAffectedHistory> callback, Action<TLRPCError> faultCallback = null)
#endif
        {
#if LAYER_41
            var obj = new TLReadHistory { Peer = peer, MaxId = maxId };
#else
            var obj = new TLReadHistory { Peer = peer, MaxId = maxId, Offset = offset };
#endif

            const string caption = "messages.readHistory";
            ReadHistoryAsyncInternal(obj,
                result =>
                {
#if LAYER_41
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        _updatesService.SetState(null, result.Pts, null, null, null, caption);
                    }

                    callback.SafeInvoke(result);
#else
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        _updatesService.SetState(result.Seq, result.Pts, null, null, null, caption);
                    }

                    if (result.Offset.Value > 0)
                    {
                        ReadHistoryAsync(peer, maxId, result.Offset, callback, faultCallback);
                    }
                    else
                    {
                        callback.SafeInvoke(result);
                    }
#endif

                },
                () => { },
                faultCallback.SafeInvoke);
        }

        public void ReadMessageContentsAsync(TLVector<TLInt> id, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLReadMessageContents { Id = id };

            const string caption = "messages.readMessageContents";
            ReadMessageContentsAsyncInternal(obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        _updatesService.SetState(null, result.Pts, null, null, null, caption);
                    }

                    callback.SafeInvoke(result);
                },
                () => { },
                faultCallback.SafeInvoke);
        }

        public void DeleteHistoryAsync(TLInputPeerBase peer, TLInt offset, Action<TLAffectedHistory> callback, Action<TLRPCError> faultCallback = null)
        {
#if LAYER_41
            var obj = new TLDeleteHistory { Peer = peer, MaxId = new TLInt(int.MaxValue) };
#else
            var obj = new TLDeleteHistory { Peer = peer, Offset = offset };
#endif

            const string caption = "messages.deleteHistory";
            SendInformativeMessage<TLAffectedHistory>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        _updatesService.SetState(result.Seq, result.Pts, null, null, null, caption);
                    }

                    callback(result);
                },
                faultCallback);
        }

        public void DeleteMessagesAsync(TLVector<TLInt> id, Action<TLAffectedMessages> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLDeleteMessages { Id = id };

            const string caption = "messages.deleteMessages";
            SendInformativeMessage<TLAffectedMessages>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
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

        public void RestoreMessagesAsync(TLVector<TLInt> id, Action<TLVector<TLInt>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLRestoreMessages{ Id = id };

            SendInformativeMessage("messages.restoreMessages", obj, callback, faultCallback);
        }

        public void ReceivedMessagesAsync(TLInt maxId, Action<TLVector<TLReceivedNotifyMessage>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLReceivedMessages { MaxId = maxId };

            SendInformativeMessage("messages.receivedMessages", obj, callback, faultCallback);
        }

        public void ForwardMessageAsync(TLInputPeerBase peer, TLInt fwdMessageId, TLMessage25 message, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLForwardMessage { Peer = peer, Id = fwdMessageId, RandomId = message.RandomId };

            const string caption = "messages.forwardMessage";
            ForwardMessageAsyncInternal(obj, 
                result =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        message.Status = MessageStatus.Confirmed;
                        message.Media.LastProgress = 0.0;
                        message.Media.DownloadingProgress = 0.0;
                    });

                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, new List<TLMessage25> { message });
                    }

                    callback.SafeInvoke(result);
                },
                () =>
                {
                    
                },
                faultCallback);
        }

        public void ForwardMessagesAsync(TLInputPeerBase toPeer, TLVector<TLInt> id, IList<TLMessage25> messages, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var randomId = new TLVector<TLLong>();
            foreach (var message in messages)
            {
                randomId.Add(message.RandomId);
            }

            var message40 = messages.FirstOrDefault() as TLMessage40;

            var obj = new TLForwardMessages { ToPeer = toPeer, Id = id, RandomIds = randomId, FromPeer = PeerToInputPeer(message40.FwdFromPeer), Flags = new TLInt(0) };
            if (obj.ToPeer is TLInputPeerChannel)
            {
                obj.SetAsAdmin();
            }
            const string caption = "messages.forwardMessages";

            ForwardMessagesAsyncInternal(obj,
                result =>
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        for (var i = 0; i < messages.Count; i++)
                        {
                            messages[i].Status = MessageStatus.Confirmed;
                            messages[i].Media.LastProgress = 0.0;
                            messages[i].Media.DownloadingProgress = 0.0;
                        }
                    });

                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, messages);
                    }

                    callback.SafeInvoke(result);
                },
                () =>
                {
                    
                },
                faultCallback.SafeInvoke);
        }

        public void GetChatsAsync(TLVector<TLInt> id, Action<TLChatsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetChats{ Id = id };

            SendInformativeMessage("messages.getChats", obj, callback, faultCallback);
        }

        public void GetFullChatAsync(TLInt chatId, Action<TLMessagesChatFull> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetFullChat { ChatId = chatId };

            SendInformativeMessage<TLMessagesChatFull>(
                "messages.getFullChat", obj,
                messagesChatFull =>
                {
                    _cacheService.SyncChat(messagesChatFull, result => callback.SafeInvoke(messagesChatFull));
                },
                faultCallback);
        }

        public void EditChatTitleAsync(TLInt chatId, TLString title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLEditChatTitle { ChatId = chatId, Title = title };

            const string caption = "messages.editChatTitle";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void EditChatPhotoAsync(TLInt chatId, TLInputChatPhotoBase photo, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLEditChatPhoto { ChatId = chatId, Photo = photo };

            const string caption = "messages.editChatPhoto";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void AddChatUserAsync(TLInt chatId, TLInputUserBase userId, TLInt fwdLimit, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLAddChatUser { ChatId = chatId, UserId = userId, FwdLimit = fwdLimit };

            const string caption = "messages.addChatUser";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void DeleteChatUserAsync(TLInt chatId, TLInputUserBase userId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLDeleteChatUser { ChatId = chatId, UserId = userId };

            const string caption = "messages.deleteChatUser";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void CreateChatAsync(TLVector<TLInputUserBase> users, TLString title, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLCreateChat { Users = users, Title = title };

            const string caption = "messages.createChat";
            SendInformativeMessage<TLUpdatesBase>(caption,
                obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

        public void ExportChatInviteAsync(TLInt chatId, Action<TLExportedChatInvite> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLExportChatInvite { ChatId = chatId };

            SendInformativeMessage("messages.exportChatInvite", obj, callback, faultCallback);
        }

        public void CheckChatInviteAsync(TLString hash, Action<TLChatInviteBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLCheckChatInvite { Hash = hash };

            SendInformativeMessage("messages.checkChatInvite", obj, callback, faultCallback);
        }

        public void ImportChatInviteAsync(TLString hash, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLImportChatInvite { Hash = hash };

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

                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

	    public void SendActionsAsync(List<TLObject> actions, Action<TLObject, TLObject> callback, Action<TLRPCError> faultCallback = null)
	    {
	        var container = new TLContainer{ Messages = new List<TLContainerTransportMessage>() };
	        var historyItems = new List<HistoryItem>();
	        for (var i = 0; i < actions.Count; i++)
	        {
	            var obj = actions[i];
                int sequenceNumber;
                TLLong messageId;
                lock (_activeTransportRoot)
                {
                    sequenceNumber = _activeTransport.SequenceNumber * 2 + 1;
                    _activeTransport.SequenceNumber++;
                    messageId = _activeTransport.GenerateMessageId(true);
                }

	            var data = i > 0 ? new TLInvokeAfterMsg {MsgId = container.Messages[i - 1].MessageId, Object = obj} : obj;
	            var invokeWithoutUpdates = new TLInvokeWithoutUpdates {Object = data};

                var transportMessage = new TLContainerTransportMessage
                {
                    MessageId = messageId,
                    SeqNo = new TLInt(sequenceNumber),
                    MessageData = invokeWithoutUpdates
                };

                var historyItem = new HistoryItem
                {
                    SendTime = DateTime.Now,
                    Caption = "messages.containerPart" + i,
                    Object = obj,
                    Message = transportMessage,
                    Callback = result => callback(obj, result),
                    AttemptFailed = null,
                    FaultCallback = faultCallback,
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

            SendNonInformativeMessage<TLObject>("messages.container", container, result => callback(null, result), faultCallback);
	    }

	    public void ToggleChatAdminsAsync(TLInt chatId, TLBool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLToggleChatAdmins { ChatId = chatId, Enabled = enabled };

            const string caption = "messages.toggleChatAdmins";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
	    }

        public void EditChatAdminAsync(TLInt chatId, TLInputUserBase userId, TLBool isAdmin, Action<TLBool> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLEditChatAdmin { ChatId = chatId, UserId = userId, IsAdmin = isAdmin };

            SendInformativeMessage("messages.editChatAdmin", obj, callback, faultCallback);
	    }

        public void DeactivateChatAsync(TLInt chatId, TLBool enabled, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLDeactivateChat { ChatId = chatId, Enabled = enabled };

            const string caption = "messages.deactivateChat";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
        }

	    public void MigrateChatAsync(TLInt chatId, Action<TLUpdatesBase> callback, Action<TLRPCError> faultCallback = null)
	    {
            var obj = new TLMigrateChat { ChatId = chatId };

            const string caption = "messages.migrateChat";
            SendInformativeMessage<TLUpdatesBase>(caption, obj,
                result =>
                {
                    var multiPts = result as IMultiPts;
                    if (multiPts != null)
                    {
                        _updatesService.SetState(multiPts, caption);
                    }
                    else
                    {
                        ProcessUpdates(result, null);
                    }

                    callback.SafeInvoke(result);
                },
                faultCallback);
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
