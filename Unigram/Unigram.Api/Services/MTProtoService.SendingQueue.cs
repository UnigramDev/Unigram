﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Extensions;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Methods.Messages;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        private readonly object _sendingQueueSyncRoot = new object();

        private readonly List<HistoryItem> _sendingQueue = new List<HistoryItem>();

        private static Timer _sendingTimer;

        private static void StartSendingTimer()
        {
            //Helpers.Execute.ShowDebugMessage("MTProtoService.StartSendingTimer");
            _sendingTimer.Change(TimeSpan.FromSeconds(Constants.ResendMessageInterval), TimeSpan.FromSeconds(Constants.ResendMessageInterval));
        }

        private static void StopSendingTimer()
        {
            //Helpers.Execute.ShowDebugMessage("MTProtoService.StoptSendingTimer");
            _sendingTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private static void CheckSendingMessages(object state)
        {
#if DEBUG
            if (Debugger.IsAttached) return;
#endif

            var service = (MTProtoService)state;

            service.ProcessQueue();
        }

        private Task<MTProtoResponse<bool>> ReadEncryptedHistoryAsyncInternal(TLMessagesReadEncryptedHistory message)
        {
            return SendAsyncInternal<bool>("messages.readEncryptedHistory", int.MaxValue, message);
        }

#if LAYER_41

        private void ReadHistoryAsyncInternal(TLReadHistory message, Action<TLAffectedMessages> callback, Action fastCallback, Action<TLRPCError> faultCallback)
#else
        private Task<MTProtoResponse<TLMessagesAffectedHistory>> ReadHistoryAsyncInternal(TLMessagesReadHistory message)
#endif
        {
            return SendAsyncInternal<TLMessagesAffectedHistory>("messages.readHistory", int.MaxValue, message);
        }

        private Task<MTProtoResponse<TLMessagesAffectedMessages>> ReadMessageContentsAsyncInternal(TLMessagesReadMessageContents message)
        {
            return SendAsyncInternal<TLMessagesAffectedMessages>("messages.readMessageContents", int.MaxValue, message);
        }

        private Task<MTProtoResponse<TLMessagesSentEncryptedMessage>> SendEncryptedAsyncInternal(TLMessagesSendEncrypted message)
        {
            return SendAsyncInternal<TLMessagesSentEncryptedMessage>("messages.sendEncrypted", Constants.MessageSendingInterval, message);
        }

        private Task<MTProtoResponse<TLMessagesSentEncryptedFile>> SendEncryptedFileAsyncInternal(TLMessagesSendEncryptedFile message)
        {
            return SendAsyncInternal<TLMessagesSentEncryptedFile>("messages.sendEncryptedFile", Constants.MessageSendingInterval, message);
        }

        private Task<MTProtoResponse<TLMessagesSentEncryptedMessage>> SendEncryptedServiceAsyncInternal(TLMessagesSendEncryptedService message)
        {
            return SendAsyncInternal<TLMessagesSentEncryptedMessage>("messages.sendEncryptedService", Constants.MessageSendingInterval, message);
        }

        private Task<MTProtoResponse<TLUpdatesBase>> SendMessageAsyncInternal(TLMessagesSendMessage message)
        {
            return SendAsyncInternal<TLUpdatesBase>("messages.sendMessage", Constants.MessageSendingInterval, message);
        }

        private Task<MTProtoResponse<TLUpdatesBase>> SendMediaAsyncInternal(TLMessagesSendMedia message)
        {
            return SendAsyncInternal<TLUpdatesBase>("messages.sendMedia", Constants.MessageSendingInterval, message);
        }

        private Task<MTProtoResponse<TLUpdatesBase>> StartBotAsyncInternal(TLMessagesStartBot message)
        {
            return SendAsyncInternal<TLUpdatesBase>("messages.startBot", Constants.MessageSendingInterval, message);
        }

        private Task<MTProtoResponse<TLUpdatesBase>> ForwardMessageAsyncInternal(TLMessagesForwardMessage message)
        {
            return SendAsyncInternal<TLUpdatesBase>("messages.forwardMessage", Constants.MessageSendingInterval, message);
        }

        private Task<MTProtoResponse<TLUpdatesBase>> ForwardMessagesAsyncInternal(TLMessagesForwardMessages message)
        {
            return SendAsyncInternal<TLUpdatesBase>("messages.forwardMessages", Constants.MessageSendingInterval, message);
        }

        private async Task<MTProtoResponse<T>> SendAsyncInternal<T>(string caption, double timeout, TLObject obj)
        {
            int sequenceNumber;
            long messageId;
            lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2 + 1;
                _activeTransport.SequenceNumber++;
                messageId = _activeTransport.GenerateMessageId(true);
            }

            var transportMessage = new TLContainerTransportMessage
            {
                MsgId = messageId,
                SeqNo = sequenceNumber,
                Query = obj
            };

            var callback = new TaskCompletionSource<MTProtoResponse>();

            var now = DateTime.Now;
            var sendBeforeTime = now.AddSeconds(timeout);
            var sendingItem = new HistoryItem
            {
                SendTime = now,
                SendBeforeTime = sendBeforeTime,
                Caption = caption,
                Callback = callback,
                Object = obj,
                Message = transportMessage,
                FaultCallback = null, // чтобы не вылететь по таймауту не сохраняем сюда faultCallback, а просто запоминаем последнюю ошибку,                
                InvokeAfter = null,   // устанвливаем в момент создания контейнера historyItems.LastOrDefault(),
                Status = RequestStatus.ReadyToSend,
            };

            //обрабатываем ошибки
            sendingItem.FaultCallback = error => ProcessFault(sendingItem, error);

            AddActionInfoToFile(TLUtils.DateToUniversalTimeTLInt(ClientTicksDelta, sendBeforeTime), obj);

            lock (_sendingQueueSyncRoot)
            {
                _sendingQueue.Add(sendingItem);

                StartSendingTimer();
            }

            ProcessQueue();

            return await callback.Task;

            //await Task.WhenAny(ProcessQueue(), callback.Task);
            //return callback.Task.Result;
        }

        private void ProcessFault(HistoryItem item, TLRPCError error)
        {
            item.LastError = error;
            if (error != null &&
                (error.CodeEquals(TLErrorCode.BAD_REQUEST) ||
                    error.CodeEquals(TLErrorCode.FLOOD) ||
                    error.CodeEquals(TLErrorCode.UNAUTHORIZED)))
            {
                RemoveFromQueue(item);
                item.FaultQueueCallback.SafeInvoke(error);
            }
        }

        private void ProcessQueue()
        {
            CleanupQueue();
            SendQueue();
        }

        private async void SendQueue()
        {
            List<HistoryItem> itemsSnapshort;
            lock (_sendingQueueSyncRoot)
            {
                itemsSnapshort = _sendingQueue.ToList();
            }
            if (itemsSnapshort.Count == 0) return;

            var historyItems = new List<HistoryItem>();
            for (var i = 0; i < itemsSnapshort.Count; i++)
            {
                itemsSnapshort[i].SendTime = DateTime.Now;
                itemsSnapshort[i].InvokeAfter = historyItems.LastOrDefault();
                historyItems.Add(itemsSnapshort[i]);
            }

#if DEBUG
            NotifyOfPropertyChange(() => History);
#endif

            lock (_historyRoot)
            {
                for (var i = 0; i < historyItems.Count; i++)
                {
                    _history[historyItems[i].Hash] = historyItems[i];
                }
            }

            var container = CreateContainer(historyItems);

            await SendNonInformativeMessage<TLObject>("container.sendMessages", container);
        }

        private void CleanupQueue()
        {
            var itemsToRemove = new List<HistoryItem>();

            lock (_sendingQueueSyncRoot)
            {
                var now = DateTime.Now;
                for (var i = 0; i < _sendingQueue.Count; i++)
                {
                    var historyItem = _sendingQueue[i];
                    if (historyItem.SendBeforeTime > now) continue;

                    itemsToRemove.Add(historyItem);
                    _sendingQueue.RemoveAt(i--);
                }

                if (_sendingQueue.Count == 0)
                {
                    StopSendingTimer();
                }
            }

            lock (_historyRoot)
            {
                for (var i = 0; i < itemsToRemove.Count; i++)
                {
                    _history.Remove(itemsToRemove[i].Hash);
                }
            }

            var actions = new TLVector<TLObject>();
            for (var i = 0; i < itemsToRemove.Count; i++)
            {
                actions.Add(itemsToRemove[i].Object);
            }
            RemoveActionInfoFromFile(actions);

            Helpers.Execute.BeginOnThreadPool(() =>
            {
                foreach (var item in itemsToRemove)
                {
                    item.FaultQueueCallback.SafeInvoke(new TLRPCError { ErrorCode = 404, ErrorMessage = "MTProtoService.CleanupQueue" });
                }
            });
        }

        public void ClearQueue()
        {
            var itemsToRemove = new List<HistoryItem>();

            lock (_sendingQueueSyncRoot)
            {
                var now = DateTime.Now;
                for (var i = 0; i < _sendingQueue.Count; i++)
                {
                    var historyItem = _sendingQueue[i];

                    itemsToRemove.Add(historyItem);
                    _sendingQueue.RemoveAt(i--);
                }

                if (_sendingQueue.Count == 0)
                {
                    StopSendingTimer();
                }
            }

            lock (_historyRoot)
            {
                for (var i = 0; i < itemsToRemove.Count; i++)
                {
                    _history.Remove(itemsToRemove[i].Hash);
                }
            }

            var actions = new TLVector<TLObject>();
            for (var i = 0; i < itemsToRemove.Count; i++)
            {
                actions.Add(itemsToRemove[i].Object);
            }

            ClearActionInfoFile();

            Helpers.Execute.BeginOnThreadPool(() =>
            {
                foreach (var item in itemsToRemove)
                {
                    item.FaultQueueCallback.SafeInvoke(new TLRPCError { ErrorCode = 404, ErrorMessage = "MTProtoService.CleanupQueue" });
                }
            });
        }

        private void RemoveFromQueue(HistoryItem item)
        {
            if (item == null)
            {
                Helpers.Execute.ShowDebugMessage("MTProtoService.RemoveFromQueue item=null");
                return;
            }

            lock (_sendingQueueSyncRoot)
            {
                _sendingQueue.Remove(item);
            }

            RemoveActionInfoFromFile(item.Object);
        }

        private void RemoveFromQueue(long? id)
        {
            HistoryItem item = null;
            lock (_sendingQueueSyncRoot)
            {
                foreach (var historyItem in _sendingQueue)
                {
                    var randomId = historyItem.Object as ITLRandomId;
                    if (randomId != null && randomId.RandomId.Value == id.Value)
                    {
                        item = historyItem;
                        break;
                    }
                }
                if (item != null)
                {
                    _sendingQueue.Remove(item);
                }
            }

            RemoveActionInfoFromFile(id);
        }

        private readonly object _actionsSyncRoot = new object();

        private TLVector<TLActionInfo> _actionInfo;

        public TLVector<TLActionInfo> GetActionInfoFromFile()
        {
            if (_actionInfo != null)
            {
                return _actionInfo;
            }

            _actionInfo = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLActionInfo>>(_actionsSyncRoot, Constants.ActionQueueFileName) ?? new TLVector<TLActionInfo>();

            return _actionInfo;
        }

        private void SaveActionInfoToFile(TLVector<TLActionInfo> data)
        {
            TLUtils.SaveObjectToMTProtoFile(_actionsSyncRoot, Constants.ActionQueueFileName, data);
        }

        private void AddActionInfoToFile(int sendBefore, TLObject obj)
        {
            if (!TLUtils.IsValidAction(obj))
            {
                return;
            }

            var actions = GetActionInfoFromFile();

            var actionInfo = new TLActionInfo
            {
                Action = obj,
                SendBefore = sendBefore
            };
            actions.Add(actionInfo);

            SaveActionInfoToFile(actions);
        }

        private void RemoveActionInfoFromFile(TLVector<TLObject> objects)
        {
            var actions = GetActionInfoFromFile();

            foreach (var obj in objects)
            {
                RemoveActionInfoCommon(actions, obj);
            }

            SaveActionInfoToFile(actions);
        }

        private void RemoveActionInfoCommon(TLVector<TLActionInfo> actions, TLObject obj)
        {
            for (var i = 0; i < actions.Count; i++)
            {
                if (actions[i].Action.GetType() == obj.GetType())
                {
                    if (actions[i].Action == obj)
                    {
                        actions.RemoveAt(i--);
                        continue;
                    }

                    var randomId1 = actions[i].Action as ITLRandomId;
                    var randomId2 = obj as ITLRandomId;
                    if (randomId1 != null
                        && randomId2 != null
                        && randomId1.RandomId.Value == randomId2.RandomId.Value)
                    {
                        actions.RemoveAt(i--);
                        continue;
                    }
                }
            }
        }

        private void RemoveActionInfoFromFile(long? id)
        {
            var actions = GetActionInfoFromFile();

            for (var i = 0; i < actions.Count; i++)
            {
                var randomId = actions[i].Action as ITLRandomId;
                if (randomId != null
                    && randomId.RandomId.Value == id.Value)
                {
                    actions.RemoveAt(i--);
                }
            }

            SaveActionInfoToFile(actions);
        }

        private void RemoveActionInfoFromFile(TLObject obj)
        {
            var actions = GetActionInfoFromFile();

            RemoveActionInfoCommon(actions, obj);

            SaveActionInfoToFile(actions);
        }

        public void RemoveActionInfoFromFile(IEnumerable<TLObject> obj)
        {
            var actions = GetActionInfoFromFile();

            foreach (var o in obj)
            {
                RemoveActionInfoCommon(actions, o);
            }

            SaveActionInfoToFile(actions);
        }

        public void ClearActionInfoFile()
        {
            var actions = new TLVector<TLActionInfo>();
            SaveActionInfoToFile(actions);
        }

        private static TLMessageContainer CreateContainer(IList<HistoryItem> items)
        {
            var messages = new List<TLContainerTransportMessage>();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];

                var transportMessage = (TLContainerTransportMessage)item.Message;
                if (item.InvokeAfter != null)
                {
                    transportMessage.Query = new TLInvokeAfterMsg
                    {
                        MsgId = item.InvokeAfter.Message.MsgId,
                        Query = item.Object
                    };
                }

                item.Status = RequestStatus.Sent;

                messages.Add(transportMessage);
            }

            var container = new TLMessageContainer
            {
                Messages = new List<TLContainerTransportMessage>(messages)
            };

            return container;
        }


        public void GetSyncErrorsAsync(Action<ExceptionInfo, IList<ExceptionInfo>> callback)
        {
            Helpers.Execute.BeginOnThreadPool(() => callback.SafeInvoke(_cacheService.LastSyncMessageException, _updatesService.SyncDifferenceExceptions));
        }

        public void GetSendingQueueInfoAsync(Action<string> callback)
        {
            Helpers.Execute.BeginOnThreadPool(() =>
            {
                var info = new StringBuilder();
                lock (_sendingQueueSyncRoot)
                {
                    var count = 0;
                    foreach (var item in _sendingQueue)
                    {
                        var sendBeforeTimeString = item.SendBeforeTime.HasValue
                            ? item.SendBeforeTime.Value.ToString("H:mm:ss.fff")
                            : null;

                        var message = string.Empty;
                        try
                        {
                            var transportMessage = item.Message as TLContainerTransportMessage;
                            if (transportMessage != null)
                            {
                                var sendMessage = transportMessage.Query as TLMessagesSendMessage;
                                if (sendMessage != null)
                                {
                                    message = string.Format("{0} {1}", sendMessage.Message, sendMessage.RandomId);
                                }
                                else
                                {
                                    var invokeAfterMsg = transportMessage.Query as TLInvokeAfterMsg;
                                    if (invokeAfterMsg != null)
                                    {
                                        sendMessage = invokeAfterMsg.Query as TLMessagesSendMessage;
                                        if (sendMessage != null)
                                        {
                                            message = string.Format("{0} {1}", sendMessage.Message, sendMessage.RandomId);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                        info.AppendLine(string.Format("{0} send={1} before={2} msg=[{3}] error=[{4}]", count++, item.SendTime.ToString("H:mm:ss.fff"), sendBeforeTimeString, message, item.LastError));
                    }
                }

                callback.SafeInvoke(info.ToString());
            });
        }

    }
}
