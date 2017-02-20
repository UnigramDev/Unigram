#define LOG_CLIENTSEQ
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Security;
using Telegram.Api.Helpers;
using Telegram.Logs;
#if DEBUG
using System.Windows;
#endif
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Updates
{
    public class UpdatesService : IUpdatesService
    {
        public TLUserBase CurrentUser { get; set; }

        private readonly ICacheService _cacheService;

        private readonly ITelegramEventAggregator _eventAggregator;

        public Func<int> GetCurrentUserId { get; set; }

        public Action<Action<TLUpdatesState>, Action<TLRPCError>> GetStateAsync { get; set; }
        public GetDHConfigAction GetDHConfigAsync { get; set; }
        public GetDifferenceAction GetDifferenceAsync { get; set; }
        public AcceptEncryptionAction AcceptEncryptionAsync { get; set; }
        public SendEncryptedServiceAction SendEncryptedServiceAsync { get; set; }
        public SetMessageOnTimeAtion SetMessageOnTimeAsync { get; set; }
        public Action<long> RemoveFromQueue { get; set; }
        public UpdateChannelAction UpdateChannelAsync { get; set; }
        public GetParticipantAction GetParticipantAsync { get; set; }
        public GetFullChatAction GetFullChatAsync { get; set; }
        public GetFullUserAction GetFullUserAsync { get; set; }
        public GetChannelMessagesAction GetChannelMessagesAsync { get; set; }

        private readonly Timer _lostSeqTimer;

        private readonly Timer _lostPtsTimer;

        public UpdatesService(ICacheService cacheService, ITelegramEventAggregator eventAggregator)
        {
            _lostSeqTimer = new Timer(OnCheckLostSeq, this, Timeout.Infinite, Timeout.Infinite);
            _lostPtsTimer = new Timer(OnCheckLostPts, this, Timeout.Infinite, Timeout.Infinite);

            _cacheService = cacheService;
            _eventAggregator = eventAggregator;
        }

        private void StartLostSeqTimer()
        {
            _lostSeqTimer.Change(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0));
            TLUtils.WriteLine(DateTime.Now.ToString("  HH:mm:ss.fff", CultureInfo.InvariantCulture) + " Start lostSeqTimer", LogSeverity.Error);

        }

        private void StopLostSeqTimer()
        {
            _lostSeqTimer.Change(Timeout.Infinite, Timeout.Infinite);
            TLUtils.WriteLine(DateTime.Now.ToString("  HH:mm:ss.fff", CultureInfo.InvariantCulture) + " Stop lostSeqTimer", LogSeverity.Error);
        }

        private void StartLostPtsTimer()
        {
            _lostPtsTimer.Change(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0));
            TLUtils.WriteLine(DateTime.Now.ToString("  HH:mm:ss.fff", CultureInfo.InvariantCulture) + " Start lostPtsTimer", LogSeverity.Error);

        }

        private void StopLostPtsTimer()
        {
            _lostPtsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            TLUtils.WriteLine(DateTime.Now.ToString("  HH:mm:ss.fff", CultureInfo.InvariantCulture) + " Stop lostPtsTimer", LogSeverity.Error);
        }

        private void OnCheckLostSeq(object state)
        {
            TLUtils.WriteLine(DateTime.Now.ToString("  HH:mm:ss.fff", CultureInfo.InvariantCulture) + " OnCheck lostSeqTimer", LogSeverity.Error);
            var getDifference = false;
            var isLostSeqEmpty = true;
            var keyValuePair = default(KeyValuePair<int, Tuple<DateTime, TLUpdatesState>>);
            lock (_clientSeqLock)
            {
                foreach (var keyValue in _lostSeq.OrderBy(x => x.Key))
                {
                    isLostSeqEmpty = false;
                    if (DateTime.Now > keyValue.Value.Item1.AddSeconds(3.0))
                    {
                        getDifference = true;
                        keyValuePair = keyValue;
                        break;
                    }
                }
            }

            if (isLostSeqEmpty)
            {
                StopLostSeqTimer();
            }

            if (getDifference)
            {
                var seq = keyValuePair.Key;
                var pts = keyValuePair.Value.Item2.Pts;
                var date = keyValuePair.Value.Item2.Date;
                var qts = keyValuePair.Value.Item2.Qts;

                Helpers.Execute.ShowDebugMessage(string.Format("stub lostSeqTimer.getDifference(seq={0}, pts={1}, date={2}, qts={3}) localState=[seq={4}, pts={5}, date={6}, qts={7}]", seq, pts, date, qts, ClientSeq, _pts, _date, _qts));
                StopLostSeqTimer();

                lock (_clientSeqLock)
                {
                    _lostSeq.Clear();
                }
                //GetDifference(() =>
                //{

                //});
            }
        }

        private void OnCheckLostPts(object state)
        {
            TLUtils.WriteLine(DateTime.Now.ToString("  HH:mm:ss.fff", CultureInfo.InvariantCulture) + " OnCheck lostPtsTimer", LogSeverity.Error);
            var getDifference = false;
            var isLostPtsEmpty = true;
            var keyValuePair = default(KeyValuePair<int, Tuple<DateTime, TLUpdatesState>>);
            lock (_clientPtsLock)
            {
                foreach (var keyValue in _lostPts.OrderBy(x => x.Key))
                {
                    isLostPtsEmpty = false;
                    if (DateTime.Now > keyValue.Value.Item1.AddSeconds(3.0))
                    {
                        getDifference = true;
                        keyValuePair = keyValue;
                        break;
                    }
                }
            }

            if (isLostPtsEmpty)
            {
                StopLostPtsTimer();
            }

            if (getDifference)
            {
                var seq = keyValuePair.Value.Item2.Seq;
                var pts = keyValuePair.Key;
                var date = keyValuePair.Value.Item2.Date;
                var qts = keyValuePair.Value.Item2.Qts;

                Helpers.Execute.ShowDebugMessage(string.Format("stub lostSeqTimer.getDifference(seq={0}, pts={1}, date={2}, qts={3}) localState=[seq={4}, pts={5}, date={6}, qts={7}]", seq, pts, date, qts, ClientSeq, _pts, _date, _qts));
                StopLostPtsTimer();

                lock (_clientPtsLock)
                {
                    _lostPts.Clear();
                }
                //GetDifference(() =>
                //{

                //});
            }
        }

        public void SetState(ITLMultiPts multiPts, string caption)
        {
            var ptsList = TLUtils.GetPtsRange(multiPts);

            if (ptsList.Count == 0)
            {
                ptsList.Add(multiPts.Pts);
            }
#if LOG_CLIENTSEQ
            TLUtils.WriteLine(string.Format("{0} {1}\nclientSeq={2} newSeq={3}\npts={4} ptsList={5}\n", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), caption, ClientSeq != null ? ClientSeq.ToString() : "null", "null", _pts != null ? _pts.ToString() : "null", ptsList.Count > 0 ? string.Join(", ", ptsList) : "null"), LogSeverity.Error);
#endif
            UpdateLostPts(ptsList);
        }

        public int? ClientSeq { get; protected set; }

        private int? _dateInternal;

        private int? _date
        {
            get { return _dateInternal; }
            set
            {
                _dateInternal = value;
            }
        }

        private int? _pts;

        private int? _qts = 1;

        private int? _unreadCount;

        public void SetState(int? seq, int? pts, int? qts, int? date, int? unreadCount, string caption, bool cleanupMissingCounts = false)
        {
#if LOG_CLIENTSEQ
            TLUtils.WriteLine(string.Format("{0} {1}\nclientSeq={2} newSeq={3}\npts={4} newPts={5}\n", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), caption, ClientSeq != null ? ClientSeq.ToString() : "null", seq, _pts != null ? _pts.ToString() : "null", pts), LogSeverity.Error);
            //TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + caption + " clientSeq=" + ClientSeq + " newSeq=" + seq + " pts=" + pts, LogSeverity.Error);
#endif
            if (seq != null)
            {
                UpdateLostSeq(new[] { seq.Value }, cleanupMissingCounts);
            }

            _date = date ?? _date;

            if (pts != null)
            {
                UpdateLostPts(new[] { pts.Value }, cleanupMissingCounts);
            }

            _qts = qts ?? _qts;
            _unreadCount = unreadCount ?? _unreadCount;
        }

        public void SetState(TLUpdatesState state, string caption)
        {
            if (state == null) return;

            SetState(state.Seq, state.Pts, state.Qts, state.Date, state.UnreadCount, caption, true);
        }

        public void SetInitState()
        {
            GetStateAsync.SafeInvoke(
                result => SetState(result, "setInitState"),
                error => Execute.BeginOnThreadPool(TimeSpan.FromSeconds(5.0), SetInitState));
        }

        private readonly object _getDifferenceRequestRoot = new object();

        private readonly IList<int> _getDifferenceRequests = new List<int>();

        private bool RequestExists(int id)
        {
            var result = false;
            lock (_getDifferenceRequestRoot)
            {
                foreach (var differenceRequest in _getDifferenceRequests)
                {
                    if (differenceRequest == id)
                    {
                        result = true;
                        break;
                    }   
                }
            }

            return result;
        }

        private void AddRequest(int id)
        {
            lock (_getDifferenceRequestRoot)
            {
                _getDifferenceRequests.Add(id);
            }
        }

        private void RemoveRequest(int id)
        {
            lock (_getDifferenceRequestRoot)
            {
                _getDifferenceRequests.Remove(id);
            }
        }

        public void CancelUpdating()
        {
            lock (_getDifferenceRequestRoot)
            {
                _getDifferenceRequests.Clear();
            }
        }

        private void GetDifference(int id, Action callback)
        {
            if (_pts != null && _date != null && _qts != null)
            {
                GetDifference(id, _pts, _date, _qts, callback);
            }
            else
            {
                SetInitState();
                callback();
            }
        }

        private void GetDifference(int id, int? pts, int? date, int? qts, Action callback)
        {
            Logs.Log.Write(string.Format("UpdatesService.GetDifference {0} state=[p={1} d={2} q={3}]", id, _pts, _date, _qts));
            TLUtils.WritePerformance(string.Format("UpdatesService.GetDifference pts={0} date={1} qts={2}", _pts, _date, _qts));

            GetDifferenceAsync(pts.Value, date.Value, qts.Value,
                diff =>
                {
//#if DEBUG
//                    Execute.BeginOnThreadPool(TimeSpan.FromSeconds(5.0), () =>
//                    {
//#endif

                        var processDiffStopwatch = Stopwatch.StartNew();

                        var differenceEmpty = diff as TLUpdatesDifferenceEmpty;
                        if (differenceEmpty != null)
                        {
#if LOG_CLIENTSEQ
                            TLUtils.WriteLine(
                                string.Format("{0} {1} clientSeq={2} newSeq={3} pts={4}",
                                    DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                                    "processDiff empty", ClientSeq, differenceEmpty.Seq, _pts), LogSeverity.Error);
#endif
                            _date = differenceEmpty.Date;
                            lock (_clientSeqLock)
                            {
                                ClientSeq = differenceEmpty.Seq;
                            }

                            Logs.Log.Write(string.Format("UpdatesService.GetDifference {0} result {1} elapsed={2}", id,
                                diff, processDiffStopwatch.Elapsed));

                            TLUtils.WritePerformance("UpdateService.GetDifference empty result=" + differenceEmpty.Seq);

                            Execute.BeginOnThreadPool(() =>
                            {
                                var updateChannelTooLongList = new List<TLUpdateChannelTooLong>();

                                lock (_updateChannelTooLongSyncRoot)
                                {
                                    foreach (var keyValue in _updateChannelTooLongList)
                                    {
                                        updateChannelTooLongList.Add(keyValue.Value);
                                    }
                                    _updateChannelTooLongList.Clear();
                                }

                                _eventAggregator.Publish(new UpdateChannelsEventArgs
                                {
                                    UpdateChannelTooLongList = updateChannelTooLongList
                                });
                            });

                            callback();
                            return;
                        }

                        var difference = diff as TLUpdatesDifference;
                        if (difference != null)
                        {
                            //Logs.Log.Write("UpdatesService.Publish UpdatingEventArgs");
                            Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new UpdatingEventArgs()));

                            var resetEvent = new ManualResetEvent(false);

                            TLUtils.WritePerformance(
                                string.Format("UpdateService.GetDifference result=[Pts={0} Date={1} Qts={2}]",
                                    difference.State.Pts, difference.State.Date, difference.State.Qts));
                            lock (_clientSeqLock)
                            {
                                SetState(difference.State, "processDiff");
                            }
                            ProcessDifference(difference, () => resetEvent.Set());

#if DEBUG
                            resetEvent.WaitOne();
#else
                        resetEvent.WaitOne(10000);
#endif
                        }

                        var otherInfo = new StringBuilder();
                        if (difference != null && difference.OtherUpdates.Count > 0)
                        {
                            otherInfo.AppendLine();
                            for (var i = 0; i < difference.OtherUpdates.Count; i++)
                            {
                                otherInfo.AppendLine(difference.OtherUpdates[i].ToString());
                            }
                        }
                        Logs.Log.Write(string.Format("UpdatesService.GetDifference {0} result {1} elapsed={2}{3}", id,
                            diff, processDiffStopwatch.Elapsed, otherInfo));

                        var differenceSlice = diff as TLUpdatesDifferenceSlice;
                        if (differenceSlice != null)
                        {
                            GetDifference(id, callback);
                            //GetDifference(differenceSlice.State.Pts, differenceSlice.State.Date, differenceSlice.State.Qts, callback);
                        }
                        else
                        {
                            Logs.Log.Write(
                                string.Format("UpdatesService.GetDifference {0} publish UpdateCompletedEventArgs", id));

                            Execute.BeginOnThreadPool(() =>
                            {
                                var updateChannelTooLongList = new List<TLUpdateChannelTooLong>();

                                lock (_updateChannelTooLongSyncRoot)
                                {
                                    foreach (var keyValue in _updateChannelTooLongList)
                                    {
                                        updateChannelTooLongList.Add(keyValue.Value);
                                    }
                                    _updateChannelTooLongList.Clear();
                                }

                                _eventAggregator.Publish(new UpdateCompletedEventArgs
                                {
                                    UpdateChannelTooLongList = updateChannelTooLongList
                                });
                            });
                            callback();
                        }
//#if DEBUG
//                    });
//#endif
                },
                error =>
                {
                    Execute.BeginOnThreadPool(TimeSpan.FromSeconds(5.0), () =>
                    {
                        if (!RequestExists(id))
                        {
                            Logs.Log.Write(string.Format("UpdatesService.LoadStateAndUpdate {0} CancelGetDifference", id));
                            return;
                        }

                        GetDifference(id, callback);
                    });
                });
        }

        public bool IsCanceled { get; set; }

        private readonly List<ExceptionInfo> _syncDifferenceExceptions = new List<ExceptionInfo>();

        public IList<ExceptionInfo> SyncDifferenceExceptions
        {
            get { return _syncDifferenceExceptions; }
        }

        private void ProcessDifference(TLUpdatesDifference difference, System.Action callback)
        {
            // в первую очередь синхронизируем пользователей и чаты (секретный чат может создать пользователь, которого у нас нет на клиенте)
            _cacheService.SyncUsersAndChats(difference.Users, difference.Chats,
                result =>
                {

                    // сначала получаем апдейты, а только потом синхронизируем новые сообщения
                    // т.к. апдейт о создании секретного чата нада обрабатывать раньше, чем новые сообщения в нем
                    foreach (var update in difference.OtherUpdates)
                    {
                        try
                        {
                            ProcessUpdateInternal(update, false);
                        }
                        catch (Exception ex)
                        {
                            _syncDifferenceExceptions.Add(new ExceptionInfo
                            {
                                Caption = "UpdatesService.ProcessDifference OtherUpdates",
                                Exception = ex,
                                Timestamp = DateTime.Now
                            });

                            TLUtils.WriteException("UpdatesService.ProcessDifference OtherUpdates ex ", ex);
                        }
                    }

                    _cacheService.SyncDifferenceWithoutUsersAndChats(difference,
                        result2 =>
                        {
                            callback.SafeInvoke();
                        },
                        _syncDifferenceExceptions);
                });
        }

        private bool ProcessUpdatesInternal(TLUpdatesBase updatesBase, bool notifyNewMessage = true)
        {
            //ClientSeq = updates.GetSeq() ?? ClientSeq;

            var updatesShortSentMessage = updatesBase as TLUpdateShortSentMessage;
            if (updatesShortSentMessage != null)
            {
                //if (updatesShortSentMessage.Date.Value > 0)
                //{
                //    _date = updatesShortSentMessage.Date;
                //}

                Execute.ShowDebugMessage(string.Format("ProcessUpdatesInternal.UpdatesShortSentMessage: id={0}", updatesShortSentMessage.Id));

                return true;
            }

            // chat message
            var updatesShortChatMessage = updatesBase as TLUpdateShortChatMessage;
            if (updatesShortChatMessage != null)
            {
                var user = _cacheService.GetUser(updatesShortChatMessage.FromId);
                if (user == null)
                {
                    var logString = string.Format("ProcessUpdatesInternal.UpdatesShortChatMessage: user is missing (userId={0}, msgId={1})", updatesShortChatMessage.FromId, updatesShortChatMessage.Id);
                    Logs.Log.Write(logString);
                    Helpers.Execute.ShowDebugMessage(logString);
                    return false;
                }
                var chat = _cacheService.GetChat(updatesShortChatMessage.ChatId);
                if (chat == null)
                {
                    var logString = string.Format("ProcessUpdatesInternal.UpdatesShortChatMessage: chat is missing (chatId={0}, msgId={1})", updatesShortChatMessage.ChatId, updatesShortChatMessage.Id);
                    Logs.Log.Write(logString);
                    Helpers.Execute.ShowDebugMessage(logString);
                    return false;
                }

                if (updatesShortChatMessage.Date > 0 && (_date == null || _date.Value < updatesShortChatMessage.Date))
                {
                    _date = updatesShortChatMessage.Date;
                }

                ContinueShortChatMessage(updatesShortChatMessage, notifyNewMessage);

                return true;
            }

            // user message
            var updatesShortMessage = updatesBase as TLUpdateShortMessage;
            if (updatesShortMessage != null)
            {
                if (_cacheService.GetUser(updatesShortMessage.UserId) == null)
                {
                    var logString = string.Format("ProcessUpdatesInternal.UpdatesShortMessage: user is missing (userId={0}, msgId={1})", updatesShortMessage.UserId, updatesShortMessage.Id);
                    Logs.Log.Write(logString);
                    Helpers.Execute.ShowDebugMessage(logString);
                    return false;
                }

                if (updatesShortMessage.Date > 0 && (_date == null || _date.Value < updatesShortMessage.Date))
                {
                    _date = updatesShortMessage.Date;
                }

                ContinueShortMessage(updatesShortMessage, notifyNewMessage);

                return true;
            }

            var updatesShort = updatesBase as TLUpdateShort;
            if (updatesShort != null)
            {
                if (updatesShort.Date > 0 && (_date == null || _date.Value < updatesShort.Date))
                {
                    _date = updatesShort.Date;
                }
                return ProcessUpdateInternal(updatesShort.Update, notifyNewMessage);
            }

            var updatesCombined = updatesBase as TLUpdatesCombined;
            if (updatesCombined != null)
            {
                var resetEvent = new ManualResetEvent(false);
                var returnValue = true;

                _cacheService.SyncUsersAndChats(updatesCombined.Users, updatesCombined.Chats,
                    result =>
                    {
                        if (updatesCombined.Date > 0 && (_date == null || _date.Value < updatesCombined.Date))
                        {
                            _date = updatesCombined.Date;
                        }
                        //ClientSeq = combined.Seq;
                        foreach (var update in updatesCombined.Updates)
                        {
                            if (!ProcessUpdateInternal(update, notifyNewMessage))
                            {
                                returnValue = false;
                            }
                        }

                        resetEvent.Set();
                    });

                resetEvent.WaitOne(10000);

                return returnValue;
            }

            var updates = updatesBase as TLUpdates;
            if (updates != null)
            {
                var resetEvent = new ManualResetEvent(false);
                var returnValue = true;
#if WINDOWS_PHONE
                var currentThreadId = Thread.CurrentThread.ManagedThreadId;
#endif
                _cacheService.SyncUsersAndChats(updates.Users, updates.Chats,
                    result =>
                    {
                        if (updates.Date > 0 && (_date == null || _date.Value < updates.Date))
                        {
                            _date = updates.Date;
                        }
                        //ClientSeq = updatesFull.Seq;
                        foreach (var update in updates.Updates)
                        {
                            if (!ProcessUpdateInternal(update, notifyNewMessage))
                            {
                                returnValue = false;
                            }
                        }

                        resetEvent.Set();
                    });

                resetEvent.WaitOne(10000);
                return returnValue;
            }

            return false;
        }

        private void ContinueShortMessage(TLUpdateShortMessage updatesShortMessage, bool notifyNewMessage)
        {
            var message = TLUtils.GetShortMessage(
                updatesShortMessage.Id,
                updatesShortMessage.UserId,
                new TLPeerUser { Id = GetCurrentUserId() },
                updatesShortMessage.Date,
                updatesShortMessage.Message);

            var shortMessage40 = updatesShortMessage as TLUpdateShortMessage;
            if (shortMessage40 != null)
            {
                message.Flags = (TLMessage.Flag)(int)shortMessage40.Flags;
                message.FwdFrom = shortMessage40.FwdFrom;
                //message.FwdFromId = shortMessage25.FwdFromId;
                //message.FwdDate = shortMessage40.FwdDate;
                message.ReplyToMsgId = shortMessage40.ReplyToMsgId;
            }

            var shortMessage48 = updatesShortMessage as TLUpdateShortMessage;
            if (shortMessage48 != null)
            {
                // TODO: verify
                message.FwdFrom = shortMessage48.FwdFrom;
            }

            var shortMessage45 = updatesShortMessage as TLUpdateShortMessage;
            if (shortMessage45 != null)
            {
                message.ViaBotId = shortMessage45.ViaBotId;
            }

            var shortMessage34 = updatesShortMessage as TLUpdateShortMessage;
            if (shortMessage34 != null)
            {
                message.Entities = shortMessage34.Entities;
            }

            if (message.IsOut)
            {
                message.ToId = new TLPeerUser { Id = updatesShortMessage.UserId };
                message.FromId = GetCurrentUserId();
            }

            // set as read
            var readMaxId = _cacheService.GetUser(message.IsOut ? message.ToId.Id : message.FromId) as ITLReadMaxId;
            if (readMaxId != null)
            {
                var maxId = message.IsOut ? readMaxId.ReadOutboxMaxId : readMaxId.ReadInboxMaxId;
                if (maxId != null)
                {
                    if (maxId >= message.Id)
                    {
                        message.SetUnreadSilent(false);
                    }
                }
            }

            MTProtoService.ProcessSelfMessage(message);

            _cacheService.SyncMessage(message,
                cachedMessage =>
                {
                    if (notifyNewMessage)
                    {
                        _eventAggregator.Publish(cachedMessage);
                    }
                });
        }

        private void ContinueShortChatMessage(TLUpdateShortChatMessage updatesShortChatMessage, bool notifyNewMessage)
        {
            var message = TLUtils.GetShortMessage(
                updatesShortChatMessage.Id,
                updatesShortChatMessage.FromId,
                new TLPeerChat { Id = updatesShortChatMessage.ChatId },
                updatesShortChatMessage.Date,
                updatesShortChatMessage.Message);

            var shortChatMessage40 = updatesShortChatMessage as TLUpdateShortChatMessage;
            if (shortChatMessage40 != null)
            {
                message.Flags = (TLMessage.Flag)(int)shortChatMessage40.Flags;
                message.FwdFrom = shortChatMessage40.FwdFrom;
                //message.FwdFromId = shortChatMessage25.FwdFromId;
                //message.FwdDate = shortChatMessage40.FwdDate;
                message.ReplyToMsgId = shortChatMessage40.ReplyToMsgId;
            }

            var shortMessage48 = updatesShortChatMessage as TLUpdateShortChatMessage;
            if (shortMessage48 != null)
            {
                // TODO: verifyy
                message.FwdFrom = shortMessage48.FwdFrom;
            }

            var shortChatMessage45 = updatesShortChatMessage as TLUpdateShortChatMessage;
            if (shortChatMessage45 != null)
            {
                message.ViaBotId = shortChatMessage45.ViaBotId;
            }

            var shortChatMessage34 = updatesShortChatMessage as TLUpdateShortChatMessage;
            if (shortChatMessage34 != null)
            {
                message.Entities = shortChatMessage34.Entities;
            }

            // set as read
            var readMaxId = _cacheService.GetChat(message.ToId.Id) as ITLReadMaxId;
            if (readMaxId != null)
            {
                var maxId = message.IsOut ? readMaxId.ReadOutboxMaxId : readMaxId.ReadInboxMaxId;
                if (maxId != null)
                {
                    if (maxId >= message.Id)
                    {
                        message.SetUnreadSilent(false);
                    }
                }
            }

            _cacheService.SyncMessage(message,
                cachedMessage =>
                {
                    if (notifyNewMessage)
                    {
                        _eventAggregator.Publish(cachedMessage);
                    }
                });
        }

        public event EventHandler<DCOptionsUpdatedEventArgs> DCOptionsUpdated;

        protected virtual void RaiseDCOptionsUpdated(DCOptionsUpdatedEventArgs e)
        {
            DCOptionsUpdated?.Invoke(this, e);
        }

        // TODO: Encrypted
//        public static TLDecryptedMessageBase GetDecryptedMessage(int? currentUserId, TLEncryptedChat cachedChat, TLEncryptedMessageBase encryptedMessageBase, int? qts, out bool commitChat)
//        {
//            commitChat = false;

//            if (cachedChat == null) return null;
//            if (cachedChat.Key == null) return null;

//            TLDecryptedMessageBase decryptedMessage = null;
//            try
//            {
//                decryptedMessage = TLUtils.DecryptMessage(encryptedMessageBase.Bytes, cachedChat, out commitChat);
//            }
//            catch (Exception e)
//            {
//#if DEBUG
//                TLUtils.WriteException(e);
//#endif
//            }

//            if (decryptedMessage == null) return null;

//            var participantId = currentUserId.Value == cachedChat.ParticipantId.Value
//                ? cachedChat.AdminId
//                : cachedChat.ParticipantId;
//            var cachedUser = InMemoryCacheService.Instance.GetUser(participantId);
//            if (cachedUser == null) return null;

//            decryptedMessage.FromId = cachedUser.Id;
//            decryptedMessage.Out = false;
//            decryptedMessage.Unread = true;
//            decryptedMessage.RandomId = encryptedMessageBase.RandomId;
//            decryptedMessage.ChatId = encryptedMessageBase.ChatId;
//            decryptedMessage.Date = encryptedMessageBase.Date;
//            decryptedMessage.Qts = qts;

//            var message = decryptedMessage as TLDecryptedMessage;
//            if (message != null)
//            {
//                var encryptedMessage = encryptedMessageBase as TLEncryptedMessage;
//                if (encryptedMessage != null)
//                {
//                    message.Media.File = encryptedMessage.File;
//                    var document = message.Media as TLDecryptedMessageMediaDocument;
//                    if (document != null)
//                    {
//                        var file = document.File as TLEncryptedFile;
//                        if (file != null)
//                        {
//                            file.FileName = document.FileName;
//                        }
//                    }

//                    var video = message.Media as TLDecryptedMessageMediaVideo;
//                    if (video != null)
//                    {
//                        var file = video.File as TLEncryptedFile;
//                        if (file != null)
//                        {
//                            file.Duration = video.Duration;
//                        }
//                    }

//                    var audio = message.Media as TLDecryptedMessageMediaAudio;
//                    if (audio != null)
//                    {
//                        audio.UserId = decryptedMessage.FromId;
//                    }
//                }
//            }

//            return decryptedMessage;
//        }

        private Dictionary<int, int> _contactRegisteredList = new Dictionary<int, int>();

        private static readonly object _updateChannelTooLongSyncRoot = new object();

        private Dictionary<int, TLUpdateChannelTooLong> _updateChannelTooLongList = new Dictionary<int, TLUpdateChannelTooLong>();

        private bool ProcessUpdateInternal(TLUpdateBase update, bool notifyNewMessage = true)
        {
            var userStatus = update as TLUpdateUserStatus;
            if (userStatus != null)
            {
                var userBase = _cacheService.GetUser(userStatus.UserId);
                if (userBase == null)
                {
                    return false;
                }

                var user = userBase as TLUser;
                if (user != null)
                {
                    // TODO: user._status = userStatus.Status;    // not UI Thread
                    user.Status = userStatus.Status;    // not UI Thread
                }

                if (notifyNewMessage)
                {
                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(userStatus));
                }
                //Execute.BeginOnThreadPool(() => _eventAggregator.Publish(user));

                return true;
            }

            var userTyping = update as TLUpdateUserTyping;
            if (userTyping != null)
            {
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(userTyping));

                return true;
            }

            var chatUserTyping = update as TLUpdateChatUserTyping;
            if (chatUserTyping != null)
            {
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(chatUserTyping));

                return true;
            }


            var updateServiceNotification = update as TLUpdateServiceNotification;
            if (updateServiceNotification != null)
            {
                Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateServiceNotification));

                return true;
            }

            var updatePrivacy = update as TLUpdatePrivacy;
            if (updatePrivacy != null)
            {
                Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updatePrivacy));

                return true;
            }

            var updateUserBlocked = update as TLUpdateUserBlocked;
            if (updateUserBlocked != null)
            {
                var user = _cacheService.GetUser(updateUserBlocked.UserId);
                if (user != null)
                {
                    user.Blocked = updateUserBlocked.Blocked;
                    _cacheService.Commit();
                }
                Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateUserBlocked));

                return true;
            }

            var processed = ProcessEncryptedChatUpdate(update);
            if (processed != null)
            {
                return processed.Value;
            }

            var updateDCOptions = update as TLUpdateDCOptions;
            if (updateDCOptions != null)
            {
                RaiseDCOptionsUpdated(new DCOptionsUpdatedEventArgs { Update = updateDCOptions });

                return true;
            }

            var updateChannelTooLong = update as TLUpdateChannelTooLong;
            if (updateChannelTooLong != null)
            {
                if (notifyNewMessage)
                {
                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannelTooLong));
                }
                else
                {
                    lock (_updateChannelTooLongSyncRoot)
                    {
                        _updateChannelTooLongList[updateChannelTooLong.ChannelId] = updateChannelTooLong;
                    }
                }

                //var updateChannelTooLong49 = update as TLUpdateChannelTooLong49;
                //if (updateChannelTooLong49 != null)
                //{
                //    Execute.ShowDebugMessage(string.Format("updateChannelTooLong channel_id={0} channel_pts={1}", updateChannelTooLong49.ChannelId, updateChannelTooLong49.ChannelPts));
                //}
                //else
                //{
                //    Execute.ShowDebugMessage(string.Format("updateChannelTooLong channel_id={0}", updateChannelTooLong.ChannelId));
                //}
//#if DEBUG
//                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannelTooLong));
//                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannelTooLong));
//                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannelTooLong));
//#endif

                //UpdateChannelAsync(updateChannelTooLong.ChannelId,
                //    result =>
                //    {
                //        var channel = result.Chats.FirstOrDefault();
                //        if (channel != null)
                //        {
                //            // replace with channels.getDifference and handling channelDifferenceTooLong
                //            GetHistoryAsync(channel.ToInputPeer(), 0, 0,
                //                new int?(Constants.CachedMessagesCount), 0, 0,
                //                result2 =>
                //                {

                //                },
                //                error2 =>
                //                {

                //                });
                //        }
                //        else
                //        {
                            
                //        }
                //    },
                //    error =>
                //    {
                //        Execute.ShowDebugMessage("updateChannel getFullChannel error " + error);
                //    });


                return true;
            }

            var updateChannel = update as TLUpdateChannel;
            if (updateChannel != null)
            {
                Execute.ShowDebugMessage("TLUpdateChannel channel_id=" + updateChannel.ChannelId);
                UpdateChannelAsync(updateChannel.ChannelId, 
                    result =>
                    {
                        var channel = result.Chats.FirstOrDefault() as TLChannel;
                        if (channel != null)
                        {
                            GetParticipantAsync(channel.ToInputChannel(), new TLInputUserSelf(), 
                            result2 => 
                            {
                                // sync users
                                var inviter = result2.Participant as ITLChannelInviter;
                                var inviterId = inviter != null ? inviter.InviterId.ToString() : "unknown";
                                var date = inviter != null ? inviter.Date.ToString() : "unknown";
                                Execute.ShowDebugMessage(string.Format("updateChannel [channel_id={0} creator={1} kicked={2} left={3} editor={4} moderator={5} broadcast={6} public={7} verified={8} inviter=[id={9} date={10}]]", channel.Id, channel.IsCreator, channel.IsKicked, channel.IsLeft, channel.IsEditor, channel.IsModerator, channel.IsBroadcast, "channel.IsPublic", channel.IsVerified, inviterId, date));

                                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannel));
                            }, 
                            error2 =>
                            {
                                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannel));

                                Execute.ShowDebugMessage("updateChannel getParticipant error " + error2);
                            });

                            //Execute.ShowDebugMessage(string.Format("updateChannel [channel_id={0} creator={1} kicked={2} left={3} editor={4} moderator={5} broadcast={6} public={7} verified={8} inviter_id={9}]", channel.Id, channel.Creator, channel.IsKicked, channel.Left, channel.IsEditor, channel.IsModerator, channel.IsBroadcast, channel.IsPublic, channel.IsVerified, channel.ExportedInvite));
                        }
                        else
                        {
                            Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannel));

                            Execute.ShowDebugMessage("updateChannel empty");
                        }
                    },
                    error =>
                    {
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannel));

                        Execute.ShowDebugMessage("updateChannel getFullChannel error " + error);
                    });

                return true;
            }

            // TODO: Layer 56, when available check if exists
            //var updateChannelGroup = update as TLUpdateChannelGroup;
            //if (updateChannelGroup != null)
            //{
            //    Execute.ShowDebugMessage(string.Format("updateChannelGroup channel_id={0} min_id={1} max_id={2} count={3} date={4}", updateChannelGroup.ChannelId, updateChannelGroup.Group.MinId, updateChannelGroup.Group.MaxId, updateChannelGroup.Group.Count, updateChannelGroup.Group.Date));

            //    return true;
            //}

            var updateChannelPinnedMessage = update as TLUpdateChannelPinnedMessage;
            if (updateChannelPinnedMessage != null)
            {
                var channel = _cacheService.GetChat(updateChannelPinnedMessage.ChannelId) as TLChannel;
                if (channel != null)
                {
                    channel.PinnedMsgId = updateChannelPinnedMessage.Id;
                    channel.HiddenPinnedMsgId = null;
                    _cacheService.Commit();

                    var message = _cacheService.GetMessage(updateChannelPinnedMessage.Id, updateChannelPinnedMessage.ChannelId);
                    if (message == null)
                    {
                        GetChannelMessagesAsync(channel.ToInputChannel(),
                            new TLVector<int> {updateChannelPinnedMessage.Id},
                            messagesBase =>
                            {
                                _cacheService.AddMessagesToContext(messagesBase, result =>
                                {
                                    if (notifyNewMessage)
                                    {
                                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannelPinnedMessage));
                                    }
                                });
                            },
                            error =>
                            {
                                if (notifyNewMessage)
                                {
                                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannelPinnedMessage));
                                }
                            });
                    }
                    else
                    {
                        if (notifyNewMessage)
                        {
                            Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannelPinnedMessage));
                        }
                    }
                }

                return true;
            }

            var updateEditMessage = update as TLUpdateEditMessage;
            if (updateEditMessage != null)
            {
                //uExecute.ShowDebugMessage(string.Format("updateEditMessage pts={0} pts_count={1} message={2}", updateEditMessage.Pts, updateEditMessage.PtsCount, updateEditMessage.Message));

                _cacheService.SyncEditedMessage(updateEditMessage.Message, notifyNewMessage, notifyNewMessage,
                    cachedMessage =>
                    {
                        if (notifyNewMessage)
                        {
                            updateEditMessage.Message = cachedMessage;
                            Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateEditMessage));
                        }
                    });

                return true;
            }

            var updateEditChannelMessage = update as TLUpdateEditChannelMessage;
            if (updateEditChannelMessage != null)
            {
                Execute.ShowDebugMessage(string.Format("updateEditChannelMessage channel_pts={0} channel_ptscount={1} message={2}", updateEditChannelMessage.Pts, updateEditChannelMessage.PtsCount, updateEditChannelMessage.Message));
                var commonMessage = updateEditChannelMessage.Message as TLMessageCommonBase;
                if (commonMessage != null)
                {
                    var peer = commonMessage.ToId;

                    var channel = _cacheService.GetChat(commonMessage.ToId.Id) as TLChannel;
                    if (channel != null)
                    {
                        if (channel.Pts == null || (channel.Pts < updateEditChannelMessage.Pts && channel.Pts + updateEditChannelMessage.PtsCount != updateEditChannelMessage.Pts))
                        {
                            Execute.ShowDebugMessage(string.Format("channel_id={0} channel_pts={1} updateEditChannelMessage[channel_pts={2} channel_pts_count={3}]", peer.Id, channel.Pts, updateEditChannelMessage.Pts, updateEditChannelMessage.PtsCount));
                        }
                        channel.Pts = new int?(updateEditChannelMessage.Pts);
                    }

                    _cacheService.SyncEditedMessage(updateEditChannelMessage.Message, notifyNewMessage, notifyNewMessage,
                        cachedMessage =>
                        {
                            if (notifyNewMessage)
                            {
                                updateEditChannelMessage.Message = cachedMessage;
                                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateEditChannelMessage));
                            }
                        });
                }

                return true;
            }

            var updateNewChannelMessage = update as TLUpdateNewChannelMessage;
            if (updateNewChannelMessage != null)
            {
                var commonMessage = updateNewChannelMessage.Message as TLMessageCommonBase;
                if (commonMessage != null)
                {
                    var peer = commonMessage.ToId;

                    var channel = _cacheService.GetChat(commonMessage.ToId.Id) as TLChannel;
                    if (channel != null)
                    {
                        if (channel.Pts == null
                            || (channel.Pts < updateNewChannelMessage.Pts
                                && channel.Pts + updateNewChannelMessage.PtsCount != updateNewChannelMessage.Pts))
                        {
                            //Execute.ShowDebugMessage(string.Format("channel_id={0} channel_pts={1} updateNewChannelMessage[channel_pts={2} channel_pts_count={3}]", peer.Id, channel.Pts, updateNewChannelMessage.ChannelPts, updateNewChannelMessage.ChannelPtsCount));
                        }
                        channel.Pts = updateNewChannelMessage.Pts;

                        if (!commonMessage.IsOut)
                        {
                            var readInboxMaxId = channel.ReadInboxMaxId ?? 0;

                            if (commonMessage.Id <= readInboxMaxId)
                            {
                                commonMessage.SetUnreadSilent(false);
                            }
                            else
                            {
                                commonMessage.SetUnreadSilent(true);
                            }
                        }
                        else
                        {
                            var readOutboxMaxId = channel.ReadOutboxMaxId ?? 0;

                            if (commonMessage.Id <= readOutboxMaxId)
                            {
                                commonMessage.SetUnreadSilent(false);
                            }
                            else
                            {
                                commonMessage.SetUnreadSilent(true);
                            }
                        }
                    }

                    _cacheService.SyncMessage(updateNewChannelMessage.Message, notifyNewMessage, notifyNewMessage,
                        cachedMessage =>
                        {
                            if (notifyNewMessage)
                            {
                                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(cachedMessage));
                            }
                        });
                }

                return true;
            }

            var updateNewMessage = update as TLUpdateNewMessage;
            if (updateNewMessage != null)
            {
                var commonMessage = updateNewMessage.Message as TLMessageCommonBase;
                if (commonMessage != null)
                {
                    MTProtoService.ProcessSelfMessage(commonMessage);

                    TLPeerBase peer;
                    ITLReadMaxId readMaxId;
                    if (commonMessage.ToId is TLPeerChat)
                    {
                        peer = commonMessage.ToId;
                        readMaxId = _cacheService.GetChat(peer.Id) as ITLReadMaxId;
                    }
                    else
                    {
                        peer = commonMessage.IsOut ? commonMessage.ToId : new TLPeerUser { Id = commonMessage.FromId ?? 0 };
                        readMaxId = _cacheService.GetUser(peer.Id) as ITLReadMaxId;
                    }

                    // TODO: is this right?
                    if (readMaxId != null)
                    {
                        if (!commonMessage.IsOut)
                        {
                            var readInboxMaxId = readMaxId.ReadInboxMaxId;

                            if (commonMessage.Id <= readInboxMaxId)
                            {
                                commonMessage.SetUnreadSilent(false);
                            }
                            else
                            {
                                commonMessage.SetUnreadSilent(true);
                            }
                        }
                        else
                        {
                            var readOutboxMaxId = readMaxId.ReadOutboxMaxId;

                            if (commonMessage.Id <= readOutboxMaxId)
                            {
                                commonMessage.SetUnreadSilent(false);
                            }
                            else
                            {
                                commonMessage.SetUnreadSilent(true);
                            }
                        }
                    }

                    if (commonMessage.RandomId.HasValue && commonMessage.RandomId != 0)
                    {
#if DEBUG
                        Log.Write("TLUpdateNewMessage " + updateNewMessage.Message);
#endif
                        _cacheService.SyncSendingMessage(commonMessage, null,
                            cachedMessage =>
                            {
                                if (notifyNewMessage)
                                {
                                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(cachedMessage));
                                }
                            });
                    }
                    else
                    {
#if DEBUG
                        Log.Write("TLUpdateNewMessage " + updateNewMessage.Message);
#endif

                        _cacheService.SyncMessage(updateNewMessage.Message,
                            cachedMessage =>
                            {
                                if (notifyNewMessage)
                                {
                                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(cachedMessage));
                                }
                            });
                    }
                }

                return true;
            }

            var updateMessageId = update as TLUpdateMessageID;
            if (updateMessageId != null)
            {
                _cacheService.SyncSendingMessageId(updateMessageId.RandomId, updateMessageId.Id, m => { });
                RemoveFromQueue(updateMessageId.RandomId);

                return true;
            }

            var updatedReadMessagesContents = update as TLUpdateReadMessagesContents;
            if (updatedReadMessagesContents != null)
            {
                var messages = new List<TLMessage>(updatedReadMessagesContents.Messages.Count);
                foreach (var readMessageId in updatedReadMessagesContents.Messages)
                {
                    var message = _cacheService.GetMessage(readMessageId) as TLMessage;
                    if (message != null)
                    {
                        messages.Add(message);
                    }
                }

                Execute.BeginOnUIThread(() =>
                {
                    foreach (var message in messages)
                    {
                        message.IsMediaUnread = false;
                        message.RaisePropertyChanged(() => message.IsMediaUnread);

                        // TODO: Verify
                        //message.SetListened();
                        //if (message.Media != null)
                        //{
                        //    message.Media.NotListened = false;
                        //    message.Media.RaisePropertyChanged(() => message.Media.NotListened);
                        //}
                    }
                });

                return true;
            }

            var updateChannelMessageViews = update as TLUpdateChannelMessageViews;
            if (updateChannelMessageViews != null)
            {
                //Execute.ShowDebugMessage(string.Format("updateChannelMessageViews channel_id={0} id={1} views={2}", updateChannelMessageViews.ChannelId, updateChannelMessageViews.Id, updateChannelMessageViews.Views));

                var message = _cacheService.GetMessage(updateChannelMessageViews.Id, updateChannelMessageViews.ChannelId) as TLMessage;
                if (message != null)
                {
                    if (message.Views == null || message.Views.Value < updateChannelMessageViews.Views)
                    {
                        message.Views = updateChannelMessageViews.Views;

                        Execute.BeginOnUIThread(() =>
                        {
                            message.RaisePropertyChanged(() => message.Views);
                        });
                    }
                }

                return true;
            }


            var updateReadHistory = update as TLUpdateBase;
            if (update is TLUpdateReadHistoryInbox || update is TLUpdateReadHistoryOutbox)
            {
                var outbox = update is TLUpdateReadHistoryOutbox;

                int maxId;
                TLPeerBase peer;
                if (updateReadHistory is TLUpdateReadHistoryInbox)
                {
                    maxId = ((TLUpdateReadHistoryInbox)updateReadHistory).MaxId;
                    peer = ((TLUpdateReadHistoryInbox)updateReadHistory).Peer;
                }
                else
                {
                    maxId = ((TLUpdateReadHistoryOutbox)updateReadHistory).MaxId;
                    peer = ((TLUpdateReadHistoryOutbox)updateReadHistory).Peer;
                }

                ITLReadMaxId readMaxId = null;
                if (peer is TLPeerUser)
                {
                    readMaxId = _cacheService.GetUser(peer.Id) as ITLReadMaxId;
                }
                else if (peer is TLPeerChat)
                {
                    readMaxId = _cacheService.GetChat(peer.Id) as ITLReadMaxId;
                }
                SetReadMaxId(readMaxId, maxId, outbox);

                var dialog = _cacheService.GetDialog(peer);
                if (dialog != null)
                {
                    var dialog53 = dialog as TLDialog;
                    if (dialog53 != null)
                    {
                        SetReadMaxId(dialog53, maxId, outbox);
                        SetReadMaxId(dialog53.With as ITLReadMaxId, maxId, outbox);
                    }

                    var notifyMessages = new List<TLMessageCommonBase>();
                    for (int i = 0; i < dialog.Messages.Count; i++)
                    {
                        var message = dialog.Messages[i] as TLMessageCommonBase;
                        if (message != null)
                        {
                            if (message.Id != 0
                                && message.Id <= maxId
                                && message.IsOut == outbox)
                            {
                                if (message.IsUnread)
                                {
                                    message.SetUnread(false);
                                    notifyMessages.Add(message);
                                    //message.RaisePropertyChanged(() => message.Unread);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }

                    var topMessage = dialog.TopMessageItem as TLMessageCommonBase;
                    if (topMessage != null)
                    {
                        if (topMessage.Id <= maxId)
                        {
                            if (topMessage.Id != 0
                                && topMessage.IsUnread
                                && topMessage.IsOut == outbox)
                            {
                                topMessage.SetUnread(false);
                                notifyMessages.Add(topMessage);
                                //topMessage.RaisePropertyChanged(() => topMessage.Unread);
                            }
                        }
                    }

                    var unreadCount = 0;
                    if (dialog.TopMessage != null && dialog.TopMessage > maxId)
                    {
                        unreadCount = dialog.UnreadCount;
                    }
                    if (outbox)
                    {
                        unreadCount = dialog.UnreadCount;
                    }
                    dialog.UnreadCount = unreadCount;

                    Execute.BeginOnUIThread(() =>
                    {
                        if (!notifyNewMessage)
                        {
                            //Execute.ShowDebugMessage("UpdatesService.ProcessUpdateInternal cancel TLUpdateReadHistory");
                            return;
                        }

                        foreach (var message in notifyMessages)
                        {
                            message.RaisePropertyChanged(() => message.IsUnread);
                        }
                        dialog.RaisePropertyChanged(() => dialog.TopMessageItem);
                        dialog.RaisePropertyChanged(() => dialog.Self);
                        dialog.RaisePropertyChanged(() => dialog.UnreadCount);
                    });
                }

                return true;
            }

            var updateReadChannelOutbox = update as TLUpdateReadChannelOutbox;
            if (updateReadChannelOutbox != null)
            {
                //Execute.ShowDebugMessage(string.Format("TLUpdateReadChannelOutbox channel_id={0} max_id={1}", updateReadChannelOutbox.ChannelId, updateReadChannelOutbox.MaxId));

                var readMaxId = _cacheService.GetChat(updateReadChannelOutbox.ChannelId) as ITLReadMaxId;
                if (readMaxId != null)
                {
                    SetReadMaxId(readMaxId, updateReadChannelOutbox.MaxId, true);
                }

                var dialog = _cacheService.GetDialog(new TLPeerChannel { Id = updateReadChannelOutbox.ChannelId });
                if (dialog != null)
                {
                    var dialog53 = dialog as TLDialog;
                    if (dialog53 != null)
                    {
                        SetReadMaxId(dialog53, updateReadChannelOutbox.MaxId, true);
                        SetReadMaxId(dialog53.With as ITLReadMaxId, updateReadChannelOutbox.MaxId, true);
                    }

                    var messages = new List<TLMessageCommonBase>();

                    var topMessage = dialog.TopMessageItem as TLMessageCommonBase;
                    if (topMessage != null
                        && topMessage.IsOut
                        && topMessage.Id <= updateReadChannelOutbox.MaxId)
                    {
                        //dialog.UnreadCount = 0;
                        topMessage.SetUnread(false);
                        messages.Add(topMessage);
                    }

                    foreach (var messageBase in dialog.Messages)
                    {
                        var message = messageBase as TLMessageCommonBase;
                        if (message != null && message.IsUnread && message.IsOut)
                        {
                            if (message.Id != 0 && message.Id < updateReadChannelOutbox.MaxId)
                            {
                                message.SetUnread(false);
                                messages.Add(message);
                            }
                        }
                    }

                    if (notifyNewMessage)
                    {
                        Execute.BeginOnUIThread(() =>
                        {
                            foreach (var message in messages)
                            {
                                message.RaisePropertyChanged(() => message.IsUnread);
                            }

                            dialog.RaisePropertyChanged(() => dialog.TopMessageItem);
                            dialog.RaisePropertyChanged(() => dialog.Self);
                            dialog.RaisePropertyChanged(() => dialog.UnreadCount);
                        });
                    }
                }

                _cacheService.Commit();

                return true;
            }

            var updateReadChannelInbox = update as TLUpdateReadChannelInbox;
            if (updateReadChannelInbox != null)
            {
                //Execute.ShowDebugMessage(string.Format("TLUpdateReadChannelInbox channel_id={0} max_id={1}", updateReadChannelInbox.ChannelId, updateReadChannelInbox.MaxId));

                var messages = new List<TLMessageCommonBase>();

                var readMaxId = _cacheService.GetChat(updateReadChannelInbox.ChannelId) as ITLReadMaxId;
                if (readMaxId != null)
                {
                    SetReadMaxId(readMaxId, updateReadChannelInbox.MaxId, false);
                }

                var dialog = _cacheService.GetDialog(new TLPeerChannel { Id = updateReadChannelInbox.ChannelId });
                if (dialog != null)
                {
                    var dialog53 = dialog as TLDialog;
                    if (dialog53 != null)
                    {
                        SetReadMaxId(dialog53, updateReadChannelInbox.MaxId, false);
                        SetReadMaxId(dialog53.With as ITLReadMaxId, updateReadChannelInbox.MaxId, false);
                    }

                    var topMessage = dialog.TopMessageItem as TLMessageCommonBase;
                    if (topMessage != null
                        && !topMessage.IsOut
                        && topMessage.Id <= updateReadChannelInbox.MaxId)
                    {
                        dialog.UnreadCount = 0;
                        topMessage.SetUnread(false);
                        messages.Add(topMessage);
                    }

                    foreach (var messageBase in dialog.Messages)
                    {
                        var message = messageBase as TLMessageCommonBase;
                        if (message != null && message.IsUnread && !message.IsOut)
                        {
                            if (message.Id != 0 && message.Id < updateReadChannelInbox.MaxId)
                            {
                                message.SetUnread(false);
                                messages.Add(message);
                            }
                        }
                    }

                    if (notifyNewMessage)
                    {
                        Execute.BeginOnUIThread(() =>
                        {
                            foreach (var message in messages)
                            {
                                message.RaisePropertyChanged(() => message.IsUnread);
                            }

                            dialog.RaisePropertyChanged(() => dialog.TopMessageItem);
                            dialog.RaisePropertyChanged(() => dialog.Self);
                            dialog.RaisePropertyChanged(() => dialog.UnreadCount);
                        });
                    }
                }

                _cacheService.Commit();

                return true;
            }

            var updateReadMessages = update as TLUpdateReadMessagesContents;
            if (updateReadMessages != null)
            {
                var dialogs = new Dictionary<int, TLDialog>();
                var messages = new List<TLMessageCommonBase>(updateReadMessages.Messages.Count);
                foreach (var readMessageId in updateReadMessages.Messages)
                {
                    var message = _cacheService.GetMessage(readMessageId) as TLMessageCommonBase;
                    if (message != null)
                    {
                        messages.Add(message);

                        var dialog = _cacheService.GetDialog(message);
                        if (dialog != null && dialog.UnreadCount > 0)
                        {
                            dialog.UnreadCount = Math.Max(0, dialog.UnreadCount - 1);
                            var topMessage = dialog.TopMessageItem;
                            if (topMessage != null && topMessage.Id == readMessageId)
                            {
                                dialogs[dialog.ReadInboxMaxId] = dialog;
                            }
                        }
                    }
                }

                Execute.BeginOnUIThread(() =>
                {
                    foreach (var message in messages)
                    {
                        message.SetUnread(false);
                        message.RaisePropertyChanged(() => message.IsUnread);
                    }

                    foreach (var dialogBase in dialogs.Values)
                    {
                        var dialog = dialogBase as TLDialog;
                        if (dialog == null) continue;

                        dialog.RaisePropertyChanged(() => dialog.TopMessageItem);
                        dialog.RaisePropertyChanged(() => dialog.Self);
                        dialog.RaisePropertyChanged(() => dialog.UnreadCount);
                    }
                });

                return true;
            }

            var deleteMessages = update as TLUpdateDeleteMessages;
            if (deleteMessages != null)
            {
                _cacheService.DeleteMessages(deleteMessages.Messages);

                return true;
            }

            var updateDeleteChannelMessages = update as TLUpdateDeleteChannelMessages;
            if (updateDeleteChannelMessages != null)
            {
                Execute.ShowDebugMessage(string.Format("updateDeleteChannelMessages channel_id={0} msgs=[{1}] channel_pts={2} channel_pts_count={3}", updateDeleteChannelMessages.ChannelId, string.Join(", ", updateDeleteChannelMessages.Messages), updateDeleteChannelMessages.Pts, updateDeleteChannelMessages.PtsCount));

                var channel = _cacheService.GetChat(updateDeleteChannelMessages.ChannelId) as TLChannel;
                if (channel != null)
                {
                    if (channel.Pts == null || channel.Pts.Value + updateDeleteChannelMessages.PtsCount != updateDeleteChannelMessages.Pts)
                    {
                        Execute.ShowDebugMessage(string.Format("channel_id={0} channel_pts={1} updateDeleteChannelMessages[channel_pts={2} channel_pts_count={3}]", channel.Id, channel.Pts, updateDeleteChannelMessages.Pts, updateDeleteChannelMessages.PtsCount));
                    }
                    channel.Pts = updateDeleteChannelMessages.Pts;
                }

                _cacheService.DeleteChannelMessages(updateDeleteChannelMessages.ChannelId, updateDeleteChannelMessages.Messages);

                return true;
            }

            // TODO: No idea
            //var restoreMessages = update as TLUpdateRestoreMessages;
            //if (restoreMessages != null)
            //{
            //    return true;
            //}

            var updateChatAdmins = update as TLUpdateChatAdmins;
            if (updateChatAdmins != null)
            {
                var chat = _cacheService.GetChat(updateChatAdmins.ChatId) as TLChat;
                if (chat != null)
                {
                    chat.IsAdminsEnabled = updateChatAdmins.Enabled;
                    chat.Version = updateChatAdmins.Version;

                    _cacheService.Commit();
                }

                Execute.ShowDebugMessage(string.Format("TLUpdateChatAdmins chat_id={0} enabled={1} version={2}", updateChatAdmins.ChatId, updateChatAdmins.Enabled, updateChatAdmins.Version));

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChatAdmins));

                return true;
            }

            var updateChatParticipantAdmin = update as TLUpdateChatParticipantAdmin;
            if (updateChatParticipantAdmin != null)
            {
                var chat = _cacheService.GetChat(updateChatParticipantAdmin.ChatId) as TLChat;
                if (chat != null)
                {
                    var userId = GetCurrentUserId();
                    if (updateChatParticipantAdmin.UserId == userId)
                    {
                        chat.IsAdmin = updateChatParticipantAdmin.IsAdmin;
                        chat.Version = updateChatParticipantAdmin.Version;

                        _cacheService.Commit();
                    }
                }
                
                Execute.ShowDebugMessage(string.Format("TLUpdateChatParticipantAdmin chat_id={0} user_id={1} is_admin={2} version={3}", updateChatParticipantAdmin.ChatId, updateChatParticipantAdmin.UserId, updateChatParticipantAdmin.IsAdmin, updateChatParticipantAdmin.Version));

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChatParticipantAdmin));

                return true;
            }

            var updateChatParticipants = update as TLUpdateChatParticipants;
            if (updateChatParticipants != null)
            {
                var chat = _cacheService.GetChat(updateChatParticipants.Participants.ChatId) as TLChat;
                if (chat != null)
                {
                    chat.Participants = updateChatParticipants.Participants;
                    var participants = chat.Participants as TLChatParticipants;
                    if (participants != null)
                    {
                        chat.Version = participants.Version;
                    }

                    _cacheService.Commit();
                }

                Execute.ShowDebugMessage(string.Format("TLUpdateChatParticipants participants={0}", updateChatParticipants.Participants.GetType().Name));

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChatParticipants));

                return true;
            }

            var userName = update as TLUpdateUserName;
            if (userName != null)
            {
                var user = _cacheService.GetUser(userName.UserId) as TLUser;
                if (user == null)
                {
                    return false;
                }

                user.FirstName = userName.FirstName;
                user.LastName = userName.LastName;
                userName.Username = userName.Username;

                // TODO
                //var userWithUserName = user as IUserName;
                //if (userWithUserName != null)
                //{
                //}

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(userName));

                return true;
            }

            var userPhoto = update as TLUpdateUserPhoto;
            if (userPhoto != null)
            {
                if (userPhoto.Date > 0 && (_date == null || _date.Value < userPhoto.Date))
                {
                    _date = userPhoto.Date;
                }

                var user = _cacheService.GetUser(userPhoto.UserId) as TLUser;
                if (user == null)
                {
                    return false;
                }

                user.Photo = userPhoto.Photo;
                Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(userPhoto));
                //_cacheService.SyncUser(user, result => _eventAggregator.Publish(result));

                return true;
            }

            var userPhone = update as TLUpdateUserPhone;
            if (userPhone != null)
            {
                var user = _cacheService.GetUser(userPhone.UserId) as TLUser;
                if (user == null)
                {
                    return false;
                }

                user.Phone = userPhone.Phone;
                Helpers.Execute.BeginOnThreadPool(() => user.RaisePropertyChanged(() => user.Phone));

                return true;
            }

            var contactRegistered = update as TLUpdateContactRegistered;
            if (contactRegistered != null)
            {
                if (contactRegistered.Date > 0 && (_date == null || _date.Value < contactRegistered.Date))
                {
                    _date = contactRegistered.Date;
                }

                if (_contactRegisteredList.ContainsKey(contactRegistered.UserId))
                {
                    return true;
                }

                _contactRegisteredList[contactRegistered.UserId] = contactRegistered.UserId;

                var user = _cacheService.GetUser(contactRegistered.UserId);

                if (user == null)
                {
                    GetFullUserAsync(new TLInputUser { UserId = contactRegistered.UserId, AccessHash = 0 },
                        userFull =>
                        {
                            user = userFull.ToUser();
                            CreateContactRegisteredMessage(contactRegistered, notifyNewMessage);
                        },
                        error =>
                        {
                            
                        });
                }
                else
                {
                    CreateContactRegisteredMessage(contactRegistered, notifyNewMessage);
                }

                return true;
            }

            // TODO: 31/12/2016 removed?
            //var updateNewAuthorization = update as TLUpdateNewAuthorization;
            //if (updateNewAuthorization != null)
            //{
            //    if (updateNewAuthorization.Date > 0 && (_date == null || _date.Value < updateNewAuthorization.Date))
            //    {
            //        _date = updateNewAuthorization.Date;
            //    }

            //    Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateNewAuthorization));

            //    return true;
            //}

            var updateDialogPinned = update as TLUpdateDialogPinned;
            if (updateDialogPinned != null)
            {
                var dialog = _cacheService.GetDialog(updateDialogPinned.Peer);
                if (dialog != null)
                {
                    dialog.IsPinned = updateDialogPinned.IsPinned;
                    dialog.RaisePropertyChanged(() => dialog.IsPinned);
                    _cacheService.Commit();

                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateDialogPinned));
                }

                return true;
            }

            var updatePinnedDialogs = update as TLUpdatePinnedDialogs;
            if (updatePinnedDialogs != null)
            {
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updatePinnedDialogs));

                return true;
            }

            var updateContactLink = update as TLUpdateContactLink; // TODO: TLUpdateContactLinkBase;
            if (updateContactLink != null)
            {
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateContactLink));

                return true;
            }

            var updateChatParticipantAdd = update as TLUpdateChatParticipantAdd;
            if (updateChatParticipantAdd != null)
            {
                return true;
            }

            var updateChatParticipantDelete = update as TLUpdateChatParticipantDelete;
            if (updateChatParticipantDelete != null)
            {
                return true;
            }

            var updateNotifySettings = update as TLUpdateNotifySettings;
            if (updateNotifySettings != null)
            {
                var notifyPeer = updateNotifySettings.Peer as TLNotifyPeer;

                if (notifyPeer != null)
                {

                    var dialog = _cacheService.GetDialog(notifyPeer.Peer);
                    if (dialog != null)
                    {
                        dialog.NotifySettings = updateNotifySettings.NotifySettings;

                        var peerUser = dialog.Peer as TLPeerUser;
                        if (peerUser != null)
                        {
                            var user = _cacheService.GetUser(peerUser.Id);
                            if (user != null)
                            {
                                user.NotifySettings = updateNotifySettings.NotifySettings;
                                if (dialog.With != null)
                                {
                                    var dialogUser = dialog.With as TLUserBase;
                                    if (dialogUser != null)
                                    {
                                        dialogUser.NotifySettings = updateNotifySettings.NotifySettings;
                                    }
                                }
                            }
                        }

                        var peerChat = dialog.Peer as TLPeerChat;
                        if (peerChat != null)
                        {
                            var chat = _cacheService.GetChat(peerChat.Id);
                            if (chat != null)
                            {
                                chat.NotifySettings = updateNotifySettings.NotifySettings;
                                if (dialog.With != null)
                                {
                                    var dialogChat = dialog.With as TLChatBase;
                                    if (dialogChat != null)
                                    {
                                        dialogChat.NotifySettings = updateNotifySettings.NotifySettings;
                                    }
                                }
                            }
                        }

                        if (peerChat != null || peerUser != null)
                        {
                            _cacheService.Commit();
                        }

                        Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateNotifySettings));
                    }
                }
                else
                {
                    Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateNotifySettings));
                }

                return true;
            }

            var updateWebPage = update as TLUpdateWebPage;
            if (updateWebPage != null)
            {
                var message = _cacheService.GetMessage(updateWebPage.WebPage) as TLMessage;
                if (message != null)
                {
                    // TODO: message._media = new TLMessageMediaWebPage { Webpage = updateWebPage.Webpage };
                    message.Media = new TLMessageMediaWebPage { WebPage = updateWebPage.WebPage };

                    _cacheService.SyncMessage(message,
                        m =>
                        {
                            Helpers.Execute.BeginOnUIThread(() => message.RaisePropertyChanged(() => message.Media));
                        });
                }

                Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateWebPage));

                return true;
            }

            var updateNewStickerSet = update as TLUpdateNewStickerSet;
            if (updateNewStickerSet != null)
            {
                Execute.ShowDebugMessage("TLUpdateNewStickeSet");

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateNewStickerSet));
                
                return true;
            }

            var updateStickerSetsOrder = update as TLUpdateStickerSetsOrder;
            if (updateStickerSetsOrder != null)
            {
                Execute.ShowDebugMessage("TLUpdateStickerSetsOrder56");

                if (updateStickerSetsOrder.IsMasks)
                {
                    return true;
                }

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateStickerSetsOrder));

                return true;
            }

            var updateStickerSets = update as TLUpdateStickerSets;
            if (updateStickerSets != null)
            {
                Execute.ShowDebugMessage("TLUpdateStickerSets");

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateStickerSets));

                return true;
            }

            var updateReadFeaturedStickers = update as TLUpdateReadFeaturedStickers;
            if (updateReadFeaturedStickers != null)
            {
                Execute.ShowDebugMessage("TLUpdateReadFeaturedStickers");

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateReadFeaturedStickers));

                return true;
            }

            var updateRecentStickers = update as TLUpdateRecentStickers;
            if (updateRecentStickers != null)
            {
                Execute.ShowDebugMessage("TLUpdateRecentStickers");

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateRecentStickers));

                return true;
            }

            var updateSavedGifs = update as TLUpdateSavedGifs;
            if (updateSavedGifs != null)
            {
                Execute.ShowDebugMessage("TLUpdateSavedGifs");

                //Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateSavedGifs));

                return true;
            }

            var updateBotInlineQuery = update as TLUpdateBotInlineQuery;
            if (updateBotInlineQuery != null)
            {
                Execute.ShowDebugMessage("TLUpdateBotInlineQuery");

                //Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateSavedGifs));

                return true;
            }

            var updateBotCallbackQuery = update as TLUpdateBotCallbackQuery;
            if (updateBotCallbackQuery != null)
            {
                Execute.ShowDebugMessage("TLUpdateBotCallbackQuery");

                //Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateSavedGifs));

                return true;
            }

            var updateBotInlineSend = update as TLUpdateBotInlineSend;
            if (updateBotInlineSend != null)
            {
                Execute.ShowDebugMessage("TLUpdateBotInlineSend");

                //Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateSavedGifs));

                return true;
            }

            var updateInlineBotCallbackQuery = update as TLUpdateInlineBotCallbackQuery;
            if (updateInlineBotCallbackQuery != null)
            {
                Execute.ShowDebugMessage("TLUpdateInlineBotCallbackQuery");

                //Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateSavedGifs));

                return true;
            }

            var updateDraftMessage = update as TLUpdateDraftMessage;
            if (updateDraftMessage != null)
            {
                //Execute.ShowDebugMessage("TLUpdateDraftMessage draft=" + updateDraftMessage.Draft);

                var dialog = _cacheService.GetDialog(updateDraftMessage.Peer) as TLDialog;
                if (dialog != null)
                {
                    dialog.Draft = updateDraftMessage.Draft;

                    _cacheService.Commit();
                }

                if (notifyNewMessage)
                {
                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateDraftMessage));
                }

                return true;
            }

            return false;
        }

        private static void SetReadMaxId(ITLReadMaxId readMaxId, int maxId, bool outbox)
        {
            if (readMaxId == null) return;

            if (outbox)
            {
                if (readMaxId.ReadOutboxMaxId == null || readMaxId.ReadOutboxMaxId < maxId)
                {
                    readMaxId.ReadOutboxMaxId = maxId;
                }
            }
            else
            {
                if (readMaxId.ReadInboxMaxId == null || readMaxId.ReadInboxMaxId < maxId)
                {
                    readMaxId.ReadInboxMaxId = maxId;
                }
            }
        }

        private bool? ProcessEncryptedChatUpdate(TLUpdateBase update)
        {
            // typing
            var updateEncryptedChatTyping = update as TLUpdateEncryptedChatTyping;
            if (updateEncryptedChatTyping != null)
            {
                _eventAggregator.Publish(updateEncryptedChatTyping);

                return true;
            }

            // reading
            var updateEncryptedMessagesRead = update as TLUpdateEncryptedMessagesRead;
            if (updateEncryptedMessagesRead != null)
            {
                //Helpers.Execute.ShowDebugMessage(updateEncryptedMessagesRead.ToString());

                // TODO: Encrypted
                //var encryptedChat = _cacheService.GetEncryptedChat(updateEncryptedMessagesRead.ChatId) as TLEncryptedChat;
                //if (encryptedChat != null)
                //{
                //    var items = _cacheService.GetDecryptedHistory(encryptedChat.Id, 100);
                //    Execute.BeginOnUIThread(() =>
                //    {
                //        for (var i = 0; i < items.Count; i++)
                //        {
                //            if (items[i].Out.Value)
                //            {
                //                if (items[i].Status == TLMessageState.Confirmed)
                //                //&& Items[i].Date.Value <= update.MaxDate.Value) // здесь надо учитывать смещение по времени
                //                {
                //                    items[i].Status = TLMessageState.Read;
                //                    items[i].RaisePropertyChanged(() => items[i].Status);

                //                    if (items[i].TTL != null && items[i].TTL.Value > 0)
                //                    {
                //                        var decryptedMessage = items[i] as TLDecryptedMessage17;
                //                        if (decryptedMessage != null)
                //                        {
                //                            var decryptedPhoto = decryptedMessage.Media as TLDecryptedMessageMediaPhoto;
                //                            if (decryptedPhoto != null && items[i].TTL.Value <= 60.0)
                //                            {
                //                                continue;
                //                            }

                //                            var decryptedVideo17 = decryptedMessage.Media as TLDecryptedMessageMediaVideo17;
                //                            if (decryptedVideo17 != null && items[i].TTL.Value <= 60.0)
                //                            {
                //                                continue;
                //                            }

                //                            var decryptedAudio17 = decryptedMessage.Media as TLDecryptedMessageMediaAudio17;
                //                            if (decryptedAudio17 != null && items[i].TTL.Value <= 60.0)
                //                            {
                //                                continue;
                //                            }

                //                            var decryptedDocument45 = decryptedMessage.Media as TLDecryptedMessageMediaDocument45;
                //                            if (decryptedDocument45 != null && (items[i].IsVoice() || items[i].IsVideo()) && items[i].TTL.Value <= 60.0)
                //                            {
                //                                continue;
                //                            }
                //                        }

                //                        items[i].DeleteDate = new long?(DateTime.Now.Ticks + encryptedChat.MessageTTL.Value * TimeSpan.TicksPerSecond);
                //                    }
                //                }
                //                else if (items[i].Status == TLMessageState.Read)
                //                {
                //                    var message = items[i] as TLDecryptedMessage;
                //                    if (message != null)
                //                    {
                //                        break;
                //                    }
                //                }
                //            }
                //        }

                //        var dialog = _cacheService.GetEncryptedDialog(encryptedChat.Id) as TLEncryptedDialog;
                //        if (dialog != null)
                //        {
                //            //dialog.UnreadCount = new int?(dialog.UnreadCount.Value - 1);
                //            var topMessage = dialog.TopMessage;
                //            if (topMessage != null)
                //            {
                //                dialog.RaisePropertyChanged(() => dialog.TopMessage);
                //            }
                //        }
                //    });
                //}

                //_eventAggregator.Publish(updateEncryptedMessagesRead);

                return true;
            }

            // message
            var updateNewEncryptedMessage = update as TLUpdateNewEncryptedMessage;
            if (updateNewEncryptedMessage != null)
            {
                // TODO: Encryption
                //var encryptedMessageBase = updateNewEncryptedMessage.Message;
                //if (encryptedMessageBase != null)
                //{
                //    var encryptedChat = _cacheService.GetEncryptedChat(encryptedMessageBase.ChatId) as TLEncryptedChat;
                //    if (encryptedChat == null)
                //    {
                //        var chat = _cacheService.GetEncryptedChat(encryptedMessageBase.ChatId);
                //        if (chat is TLEncryptedChatWaiting)
                //        {

                //        }
                //        //Execute.ShowDebugMessage(string.Format("updateNewEncryptedMessage chat_id={0} is not TLEncryptedChat ({1})", encryptedMessageBase.ChatId, chat != null? chat.GetType() : null));

                //        return true;
                //    }

                //    TLDecryptedMessageBase decryptedMessage = null;
                //    try
                //    {
                //        bool commitChat;
                //        decryptedMessage = GetDecryptedMessage(MTProtoService.Instance.CurrentUserId, encryptedChat, encryptedMessageBase, updateNewEncryptedMessage.Qts, out commitChat);
                //        if (commitChat)
                //        {
                //            _cacheService.Commit();
                //        }
                //    }
                //    catch (Exception ex)
                //    {
                //        Helpers.Execute.ShowDebugMessage("ProcessUpdate(TLUpdateNewEncryptedMessage) ex " + ex);
                //    }

                //    if (decryptedMessage == null) return true;

                //    var hasMessagesGap = true;
                //    var decryptedMessage17 = decryptedMessage as ISeqNo;
                //    var decryptedMessageService = decryptedMessage as TLDecryptedMessageService;
                //    var encryptedChat17 = encryptedChat as TLEncryptedChat17;
                //    var encryptedChat20 = encryptedChat as TLEncryptedChat20;
                //    var encryptedChat8 = encryptedChat;


                //    if (decryptedMessageService != null)
                //    {
                //        var readMessagesAction = decryptedMessageService.Action as TLDecryptedMessageActionReadMessages;
                //        if (readMessagesAction != null)
                //        {
                //            var items = _cacheService.GetDecryptedHistory(encryptedChat.Id.Value, 100);
                //            Execute.BeginOnUIThread(() =>
                //            {
                //                foreach (var randomId in readMessagesAction.RandomIds)
                //                {
                //                    foreach (var item in items)
                //                    {
                //                        if (item.RandomId.Value == randomId.Value)
                //                        {
                //                            item.Status = TLMessageState.Read;
                //                            if (item.TTL != null && item.TTL.Value > 0)
                //                            {
                //                                item.DeleteDate = new long?(DateTime.Now.Ticks + encryptedChat8.MessageTTL.Value * TimeSpan.TicksPerSecond);
                //                            }

                //                            var message = item as TLDecryptedMessage17;
                //                            if (message != null)
                //                            {
                //                                var decryptedMediaPhoto = message.Media as TLDecryptedMessageMediaPhoto;
                //                                if (decryptedMediaPhoto != null)
                //                                {
                //                                    if (decryptedMediaPhoto.TTLParams == null)
                //                                    {
                //                                        var ttlParams = new TTLParams();
                //                                        ttlParams.IsStarted = true;
                //                                        ttlParams.Total = message.TTL.Value;
                //                                        ttlParams.StartTime = DateTime.Now;
                //                                        ttlParams.Out = message.Out.Value;

                //                                        decryptedMediaPhoto.TTLParams = ttlParams;
                //                                    }
                //                                }

                //                                var decryptedMediaVideo17 = message.Media as TLDecryptedMessageMediaVideo17;
                //                                if (decryptedMediaVideo17 != null)
                //                                {
                //                                    if (decryptedMediaVideo17.TTLParams == null)
                //                                    {
                //                                        var ttlParams = new TTLParams();
                //                                        ttlParams.IsStarted = true;
                //                                        ttlParams.Total = message.TTL.Value;
                //                                        ttlParams.StartTime = DateTime.Now;
                //                                        ttlParams.Out = message.Out.Value;

                //                                        decryptedMediaVideo17.TTLParams = ttlParams;
                //                                    }
                //                                }

                //                                var decryptedMediaAudio17 = message.Media as TLDecryptedMessageMediaAudio17;
                //                                if (decryptedMediaAudio17 != null)
                //                                {
                //                                    if (decryptedMediaAudio17.TTLParams == null)
                //                                    {
                //                                        var ttlParams = new TTLParams();
                //                                        ttlParams.IsStarted = true;
                //                                        ttlParams.Total = message.TTL.Value;
                //                                        ttlParams.StartTime = DateTime.Now;
                //                                        ttlParams.Out = message.Out.Value;

                //                                        decryptedMediaAudio17.TTLParams = ttlParams;
                //                                    }
                //                                }

                //                                var decryptedMediaDocument45 = message.Media as TLDecryptedMessageMediaDocument45;
                //                                if (decryptedMediaDocument45 != null && (message.IsVoice() || message.IsVideo()))
                //                                {
                //                                    if (decryptedMediaDocument45.TTLParams == null)
                //                                    {
                //                                        var ttlParams = new TTLParams();
                //                                        ttlParams.IsStarted = true;
                //                                        ttlParams.Total = message.TTL.Value;
                //                                        ttlParams.StartTime = DateTime.Now;
                //                                        ttlParams.Out = message.Out.Value;

                //                                        decryptedMediaDocument45.TTLParams = ttlParams;
                //                                    }

                //                                    var message45 = message as TLDecryptedMessage45;
                //                                    if (message45 != null)
                //                                    {
                //                                        message45.SetListened();
                //                                    }
                //                                    decryptedMediaDocument45.NotListened = false;
                //                                    decryptedMediaDocument45.RaisePropertyChanged(() => decryptedMediaDocument45.NotListened);
                //                                }
                //                            }
                //                            break;
                //                        }
                //                    }
                //                }
                //            });
                            
                //        }
                //    }

                //    var isDisplayedMessage = TLUtils.IsDisplayedDecryptedMessageInternal(decryptedMessage);
                //    if (!isDisplayedMessage)
                //    {
                //        decryptedMessage.Unread = false;
                //    }

                //    ProcessPFS(SendEncryptedServiceAsync, _cacheService, _eventAggregator, encryptedChat20, decryptedMessageService);

                //    if (decryptedMessage17 != null)
                //    {
                //        // если чат уже обновлен до нового слоя, то проверяем rawInSeqNo
                //        if (encryptedChat17 != null)
                //        {
                //            var chatRawInSeqNo = encryptedChat17.RawInSeqNo.Value;
                //            var messageRawInSeqNo = GetRawInFromReceivedMessage(MTProtoService.Instance.CurrentUserId, encryptedChat17, decryptedMessage17);

                //            if (messageRawInSeqNo == chatRawInSeqNo)
                //            {
                //                hasMessagesGap = false;
                //                encryptedChat17.RawInSeqNo = new int?(encryptedChat17.RawInSeqNo.Value + 1);
                //                _cacheService.SyncEncryptedChat(encryptedChat17, result => { });
                //            }
                //            else
                //            {
                //                Helpers.Execute.ShowDebugMessage(string.Format("TLUpdateNewEncryptedMessage messageRawInSeqNo != chatRawInSeqNo + 1 chatId={0} chatRawInSeqNo={1} messageRawInSeqNo={2}", encryptedChat17.Id, chatRawInSeqNo, messageRawInSeqNo));
                //            }
                //        }
                //        // обновляем до нового слоя при получении любого сообщения с более высоким слоем
                //        else if (encryptedChat8 != null)
                //        {
                //            hasMessagesGap = false;

                //            var newLayer = Constants.SecretSupportedLayer;
                //            if (decryptedMessageService != null)
                //            {
                //                var actionNotifyLayer = decryptedMessageService.Action as TLDecryptedMessageActionNotifyLayer;
                //                if (actionNotifyLayer != null)
                //                {
                //                    if (actionNotifyLayer.Layer.Value <= Constants.SecretSupportedLayer)
                //                    {
                //                        newLayer = actionNotifyLayer.Layer.Value;
                //                    }
                //                }
                //            }

                //            var layer = new int?(newLayer);
                //            var rawInSeqNo = 1;      // только что получил сообщение по новому слою
                //            var rawOutSeqNo = 0;

                //            UpgradeSecretChatLayerAndSendNotification(SendEncryptedServiceAsync, _cacheService, _eventAggregator, encryptedChat8, layer, rawInSeqNo, rawOutSeqNo);
                //        }
                //    }
                //    else if (decryptedMessageService != null)
                //    {
                //        hasMessagesGap = false;
                //        var notifyLayerAction = decryptedMessageService.Action as TLDecryptedMessageActionNotifyLayer;
                //        if (notifyLayerAction != null)
                //        {
                //            if (encryptedChat17 != null)
                //            {
                //                var newLayer = Constants.SecretSupportedLayer;
                //                if (notifyLayerAction.Layer.Value <= Constants.SecretSupportedLayer)
                //                {
                //                    newLayer = notifyLayerAction.Layer.Value;
                //                }

                //                var layer = new int?(newLayer);
                //                var rawInSeqNo = 0;
                //                var rawOutSewNo = 0;

                //                UpgradeSecretChatLayerAndSendNotification(SendEncryptedServiceAsync, _cacheService, _eventAggregator, encryptedChat17, layer, rawInSeqNo, rawOutSewNo);
                //            }
                //            else if (encryptedChat8 != null)
                //            {
                //                var newLayer = Constants.SecretSupportedLayer;
                //                if (notifyLayerAction.Layer.Value <= Constants.SecretSupportedLayer)
                //                {
                //                    newLayer = notifyLayerAction.Layer.Value;
                //                }

                //                var layer = new int?(newLayer);
                //                var rawInSeqNo = 0;
                //                var rawOutSewNo = 0;

                //                UpgradeSecretChatLayerAndSendNotification(SendEncryptedServiceAsync, _cacheService, _eventAggregator, encryptedChat8, layer, rawInSeqNo, rawOutSewNo);
                //            }
                //        }
                //    }
                //    else
                //    {
                //        hasMessagesGap = false;
                //    }

                //    if (hasMessagesGap)
                //    {
                //        Helpers.Execute.ShowDebugMessage("catch gap " + decryptedMessage);
                //        //return true;
                //    }

                //    var decryptedMessageService17 = decryptedMessage as TLDecryptedMessageService17;
                //    if (decryptedMessageService17 != null)
                //    {
                //        var resendAction = decryptedMessageService17.Action as TLDecryptedMessageActionResend;
                //        if (resendAction != null)
                //        {
                //            Helpers.Execute.ShowDebugMessage(string.Format("TLDecryptedMessageActionResend start_seq_no={0} end_seq_no={1}", resendAction.StartSeqNo, resendAction.EndSeqNo));

                //            //_cacheService.GetDecryptedHistory()
                //        }

                //    }

                //    var syncMessageFlag = IsSyncRequierd(decryptedMessage);

                //    _eventAggregator.Publish(decryptedMessage);

                //    if (syncMessageFlag)
                //    {
                //        _cacheService.SyncDecryptedMessage(decryptedMessage, encryptedChat, cachedMessage =>
                //        {
                //            SetState(null, null, updateNewEncryptedMessage.Qts, null, null, "TLUpdateNewEncryptedMessage");
                //        });
                //    }
                //    else
                //    {
                //        SetState(null, null, updateNewEncryptedMessage.Qts, null, null, "TLUpdateNewEncryptedMessage");
                //    }

                //    return true;
                //}
            }

            // creating, new layer
            var updateEncryption = update as TLUpdateEncryption;
            if (updateEncryption != null)
            {
                // TODO: Encryption
                //var chatRequested = updateEncryption.Chat as TLEncryptedChatRequested;

                //if (chatRequested != null)
                //{
                //    _cacheService.SyncEncryptedChat(updateEncryption.Chat, result => _eventAggregator.Publish(result));

                //    var message = new TLDecryptedMessageService
                //    {
                //        RandomId = TLLong.Random(),
                //        RandomBytes = TLString.Random(Constants.MinRandomBytesLength),
                //        ChatId = chatRequested.Id,
                //        Action = new TLDecryptedMessageActionEmpty(),
                //        FromId = MTProtoService.Instance.CurrentUserId,
                //        Date = chatRequested.Date,
                //        Out = new TLBool(false),
                //        Unread = new TLBool(false),
                //        Status = TLMessageState.Read
                //    };

                //    _cacheService.SyncDecryptedMessage(message, chatRequested, result => { });

                //    GetDHConfigAsync(0, 0,
                //        result =>
                //        {
                //            var dhConfig = (TLDHConfig)result;
                //            if (!TLUtils.CheckPrime(dhConfig.P.Data, dhConfig.G.Value))
                //            {
                //                return;
                //            }
                //            if (!TLUtils.CheckGaAndGb(chatRequested.GA.Data, dhConfig.P.Data))
                //            {
                //                return;
                //            }

                //            //TODO: precalculate gb to improve speed
                //            var bBytes = new byte[256];
                //            var random = new SecureRandom();
                //            random.NextBytes(bBytes);
                //            //var b = TLString.FromBigEndianData(bBytes);
                //            var p = dhConfig.P;
                //            var g = dhConfig.G;

                //            updateEncryption.Chat.P = p;
                //            updateEncryption.Chat.G = g;

                //            var gbBytes = MTProtoService.GetGB(bBytes, dhConfig.G, dhConfig.P);
                //            var gb = TLString.FromBigEndianData(gbBytes);

                //            var key = MTProtoService.GetAuthKey(bBytes, chatRequested.GA.ToBytes(), dhConfig.P.ToBytes());
                //            var keyHash = Utils.ComputeSHA1(key);
                //            var keyFingerprint = new long?(BitConverter.ToInt64(keyHash, 12));

                //            AcceptEncryptionAsync(
                //                new TLInputEncryptedChat
                //                {
                //                    AccessHash = chatRequested.AccessHash,
                //                    ChatId = chatRequested.Id
                //                },
                //                gb,
                //                keyFingerprint,
                //                chat =>
                //                {
                //                    chat.P = p;
                //                    chat.G = g;
                //                    chat.Key = TLString.FromBigEndianData(key);
                //                    chat.KeyFingerprint = keyFingerprint;

                //                    _cacheService.SyncEncryptedChat(chat, r2 => _eventAggregator.Publish(r2));
                //                },
                //                er =>
                //                {
                //                    Helpers.Execute.ShowDebugMessage("messages.acceptEncryption " + er);
                //                });

                //        },
                //        error =>
                //        {
                //            Helpers.Execute.ShowDebugMessage("messages.getDhConfig error " + error);
                //        });
                //}

                //var encryptedChat = updateEncryption.Chat as TLEncryptedChat;
                //if (encryptedChat != null)
                //{
                //    var waitingChat = _cacheService.GetEncryptedChat(encryptedChat.Id) as TLEncryptedChatWaiting;

                //    if (waitingChat != null)
                //    {
                //        var dialog = _cacheService.GetEncryptedDialog(encryptedChat.Id) as TLEncryptedDialog;
                //        if (dialog != null)
                //        {
                //            var serviceMessage = dialog.TopMessage as TLDecryptedMessageService;
                //            if (serviceMessage != null)
                //            {
                //                var action = serviceMessage.Action as TLDecryptedMessageActionEmpty;
                //                if (action != null)
                //                {
                //                    serviceMessage.Unread = new TLBool(true);
                //                    serviceMessage.Status = TLMessageState.Confirmed;
                //                }
                //            }
                //        }

                //        // уведомление о слое, если начали сами чат
                //        if (Constants.SecretSupportedLayer >= 17)
                //        {
                //            _cacheService.SyncEncryptedChat(encryptedChat,
                //                syncedChat =>
                //                {
                //                    var currentUserId = MTProtoService.Instance.CurrentUserId;
                //                    var clientTicksDelta = MTProtoService.Instance.ClientTicksDelta;

                //                    var notifyLayerAction = new TLDecryptedMessageActionNotifyLayer();
                //                    notifyLayerAction.Layer = new int?(Constants.SecretSupportedLayer);

                //                    // уведомляем в старом слое, чтобы не сломать предыдущие версии клиентов
                //                    var notifyLayerMessage = new TLDecryptedMessageService
                //                    {
                //                        Action = notifyLayerAction,
                //                        RandomId = TLLong.Random(),
                //                        RandomBytes = TLString.Random(Constants.MinRandomBytesLength),

                //                        FromId = currentUserId,
                //                        Out = true,
                //                        Unread = false,
                //                        Date = TLUtils.DateToUniversalTimeTLInt(clientTicksDelta, DateTime.Now),
                //                        Status = TLMessageState.Confirmed,

                //                        ChatId = encryptedChat.Id
                //                    };

                //                    _cacheService.SyncDecryptedMessage(notifyLayerMessage, syncedChat,
                //                        messageResult =>
                //                        {
                //                            SendEncryptedServiceAsync(
                //                                new TLInputEncryptedChat
                //                                {
                //                                    AccessHash = encryptedChat.AccessHash,
                //                                    ChatId = encryptedChat.Id
                //                                },
                //                                notifyLayerMessage.RandomId,
                //                                TLUtils.EncryptMessage(notifyLayerMessage, (TLEncryptedChat)syncedChat),
                //                                sentEncryptedMessage =>
                //                                {
                //                                    notifyLayerMessage.Status = TLMessageState.Confirmed;
                //                                    _cacheService.SyncSendingDecryptedMessage(encryptedChat.Id, sentEncryptedMessage.Date, notifyLayerMessage.RandomId, m => { });
                //                                },
                //                                error =>
                //                                {
                //                                    Helpers.Execute.ShowDebugMessage("messages.sendEncryptedService error " + error);
                //                                });
                //                        });
                //                });
                //        }
                //    }

                //    var encryptedChat17 = _cacheService.GetEncryptedChat(encryptedChat.Id) as TLEncryptedChat17;
                //    if (encryptedChat17 != null)
                //    {
                //        updateEncryption.Chat = encryptedChat17;
                //    }

                //    _cacheService.SyncEncryptedChat(updateEncryption.Chat,
                //        r =>
                //        {
                //            _eventAggregator.Publish(r);
                //        });
                //}
                //else
                //{
                //    _cacheService.SyncEncryptedChat(updateEncryption.Chat,
                //        r =>
                //        {
                //            _eventAggregator.Publish(r);
                //        });
                //}

                //return true;
            }

            return null;
        }

        private void CreateContactRegisteredMessage(TLUpdateContactRegistered updateContactRegistered, bool notifyNewMessage)
        {
            var user = _cacheService.GetUser(updateContactRegistered.UserId);

            if (user != null)
            {
                var currentUserId = MTProtoService.Current.CurrentUserId;
                var message = new TLMessageService
                {
                    Flags = 0,
                    Id = 0,
                    FromId = user.Id,
                    ToId = new TLPeerUser { Id = currentUserId },
                    State = TLMessageState.Confirmed,
                    IsOut = false,
                    IsUnread = false,
                    Date = updateContactRegistered.Date,
                    // TODO: local object 
                    // Action = new TLMessageActionContactRegistered { UserId = user.Id },
                    RandomId = TLLong.Random()
                };

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(user));

                var dialog = _cacheService.GetDialog(new TLPeerUser { Id = user.Id });
                if (dialog == null)
                {
                    _cacheService.SyncMessage(message, notifyNewMessage, notifyNewMessage,
                        cachedMessage =>
                        {
                            if (notifyNewMessage)
                            {
                                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(cachedMessage));
                            }
                        });
                }
            }
        }

        #region TODO: Encrypted
//        public static void ProcessPFS(SendEncryptedServiceAction sendEncryptedServiceActionAsync, ICacheService cacheService, ITelegramEventAggregator eventAggregator, TLEncryptedChat20 encryptedChat, TLDecryptedMessageService decryptedMessageService)
//        {
//            if (encryptedChat == null) return;
//            if (decryptedMessageService == null) return;

//            var abortKey = decryptedMessageService.Action as TLDecryptedMessageActionAbortKey;
//            if (abortKey != null)
//            {
//                encryptedChat.PFS_A = null;
//                encryptedChat.PFS_ExchangeId = null;
//                cacheService.SyncEncryptedChat(encryptedChat, cachedChat =>
//                {

//                });

//                return;
//            }

//            var noop = decryptedMessageService.Action as TLDecryptedMessageActionNoop;
//            if (noop != null)
//            {
//                return;
//            }

//            var commitKey = decryptedMessageService.Action as TLDecryptedMessageActionCommitKey;
//            if (commitKey != null)
//            {
//                encryptedChat.PFS_A = null;
//                encryptedChat.PFS_ExchangeId = null;
//                encryptedChat.Key = encryptedChat.PFS_Key;
//                encryptedChat.PFS_Key = null;
//                cacheService.SyncEncryptedChat(encryptedChat, cachedChat =>
//                {
//                    eventAggregator.Publish(encryptedChat);

//                    var actionNoop = new TLDecryptedMessageActionNoop();

//                    SendEncryptedServiceActionAsync(sendEncryptedServiceActionAsync, cacheService, eventAggregator, encryptedChat, actionNoop,
//                        (message, result) =>
//                        {

//                        });
//                });

//                return;
//            }

//            var requestKey = decryptedMessageService.Action as TLDecryptedMessageActionRequestKey;
//            if (requestKey != null)
//            {
//                var bBytes = new byte[256];
//                var random = new SecureRandom();
//                random.NextBytes(bBytes);
//                var p = encryptedChat.P;
//                var g = encryptedChat.G;

//                var gbBytes = MTProtoService.GetGB(bBytes, g, p);
//                var gb = TLString.FromBigEndianData(gbBytes);

//                encryptedChat.PFS_A = TLString.FromBigEndianData(bBytes);
//                encryptedChat.PFS_ExchangeId = requestKey.ExchangeId;

//                if (!TLUtils.CheckGaAndGb(requestKey.GA.Data, encryptedChat.P.Data))
//                {
//                    return;
//                }

//                var key = MTProtoService.GetAuthKey(encryptedChat.PFS_A.Data, requestKey.GA.ToBytes(), encryptedChat.P.ToBytes());
//                var keyHash = Utils.ComputeSHA1(key);
//                var keyFingerprint = new long?(BitConverter.ToInt64(keyHash, 12));

//                encryptedChat.PFS_Key = TLString.FromBigEndianData(key);
//                encryptedChat.PFS_KeyFingerprint = keyFingerprint;
//                cacheService.SyncEncryptedChat(encryptedChat, cachedChat =>
//                {
//                    var actionAcceptKey = new TLDecryptedMessageActionAcceptKey
//                    {
//                        ExchangeId = encryptedChat.PFS_ExchangeId,
//                        KeyFingerprint = keyFingerprint,
//                        GB = gb
//                    };

//                    SendEncryptedServiceActionAsync(sendEncryptedServiceActionAsync, cacheService, eventAggregator, encryptedChat, actionAcceptKey,
//                        (message, result) =>
//                        {

//                        });
//                });

//                return;
//            }

//            var acceptKey = decryptedMessageService.Action as TLDecryptedMessageActionAcceptKey;
//            if (acceptKey != null)
//            {
//                if (!TLUtils.CheckGaAndGb(acceptKey.GB.Data, encryptedChat.P.Data))
//                {
//                    return;
//                }

//                var key = MTProtoService.GetAuthKey(encryptedChat.PFS_A.Data, acceptKey.GB.ToBytes(), encryptedChat.P.ToBytes());
//                var keyHash = Utils.ComputeSHA1(key);
//                var keyFingerprint = new long?(BitConverter.ToInt64(keyHash, 12));

//                // abort for keyfingerprint != acceptKey.keyFingerprint
//                if (keyFingerprint.Value != acceptKey.KeyFingerprint.Value)
//                {
//                    var actionAbortKey = new TLDecryptedMessageActionAbortKey
//                    {
//                        ExchangeId = encryptedChat.PFS_ExchangeId
//                    };

//                    SendEncryptedServiceActionAsync(sendEncryptedServiceActionAsync, cacheService, eventAggregator, encryptedChat, actionAbortKey,
//                        (message, result) =>
//                        {
//                            encryptedChat.PFS_A = null;
//                            encryptedChat.PFS_ExchangeId = null;

//                            eventAggregator.Publish(encryptedChat);
//                            cacheService.Commit();
//                        });

//                    return;
//                }

//                encryptedChat.PFS_Key = TLString.FromBigEndianData(key);
//                encryptedChat.PFS_KeyFingerprint = keyFingerprint;
//                cacheService.SyncEncryptedChat(encryptedChat, cachedChat =>
//                {
//                    var actionCommitKey = new TLDecryptedMessageActionCommitKey
//                    {
//                        ExchangeId = encryptedChat.PFS_ExchangeId,
//                        KeyFingerprint = keyFingerprint
//                    };

//                    SendEncryptedServiceActionAsync(sendEncryptedServiceActionAsync, cacheService, eventAggregator, encryptedChat, actionCommitKey,
//                        (message, result) =>
//                        {
//                            encryptedChat.PFS_ExchangeId = null;
//                            if (encryptedChat.PFS_Key != null)
//                            {
//                                encryptedChat.Key = encryptedChat.PFS_Key;
//                            }
//                            encryptedChat.PFS_A = null;
//                            encryptedChat.PFS_KeyFingerprint = null;
//                            cacheService.SyncEncryptedChat(encryptedChat, cachedChat2 =>
//                            {
//                                eventAggregator.Publish(encryptedChat);
//                            });
//                        });
//                });

//                return;
//            }
//        }

//        private static void SendEncryptedServiceActionAsync(SendEncryptedServiceAction sendEncryptedServiceAsync, ICacheService cacheService, ITelegramEventAggregator eventAggregator, TLEncryptedChat20 encryptedChat, TLDecryptedMessageActionBase action, Action<TLDecryptedMessageBase, TLSentEncryptedMessage> callback)
//        {
//            if (encryptedChat == null) return;

//            var randomId = TLLong.Random();

//            var currentUserId = MTProtoService.Instance.CurrentUserId;
//            var clientTicksDelta = MTProtoService.Instance.ClientTicksDelta;

//            var inSeqNo = TLUtils.GetInSeqNo(currentUserId, encryptedChat);
//            var outSeqNo = TLUtils.GetOutSeqNo(currentUserId, encryptedChat);

//            encryptedChat.RawOutSeqNo = new int?(encryptedChat.RawOutSeqNo.Value + 1);

//            var message = new TLDecryptedMessageService17
//            {
//                Action = action,
//                RandomId = randomId,
//                RandomBytes = TLString.Random(Constants.MinRandomBytesLength),
//                ChatId = encryptedChat.Id,
//                FromId = currentUserId,
//                Out = true,
//                Unread = false,
//                Date = TLUtils.DateToUniversalTimeTLInt(clientTicksDelta, DateTime.Now),
//                Status = TLMessageState.Sending,
//                TTL = 0,
//                InSeqNo = inSeqNo,
//                OutSeqNo = outSeqNo
//            };

//            var decryptedMessageLayer17 = TLUtils.GetDecryptedMessageLayer(encryptedChat.Layer, inSeqNo, outSeqNo, message);

//            cacheService.SyncDecryptedMessage(
//                message,
//                encryptedChat,
//                messageResult =>
//                {
//                    sendEncryptedServiceAsync(
//                        new TLInputEncryptedChat
//                        {
//                            AccessHash = encryptedChat.AccessHash,
//                            ChatId = encryptedChat.Id
//                        },
//                        randomId,
//                        TLUtils.EncryptMessage(decryptedMessageLayer17, encryptedChat),
//                        result =>
//                        {
//                            message.Status = TLMessageState.Confirmed;
//                            cacheService.SyncSendingDecryptedMessage(encryptedChat.Id, result.Date, message.RandomId,
//                                m =>
//                                {
//#if DEBUG
//                                    eventAggregator.Publish(message);
//#endif
//                                    callback.SafeInvoke(message, result);
//                                });
//                        },
//                        error => { Helpers.Execute.ShowDebugMessage("messages.sendEncryptedService error " + error); });
//                });
//        }

//        public static void UpgradeSecretChatLayerAndSendNotification(SendEncryptedServiceAction sendEncryptedServiceAsync, ICacheService cacheService, ITelegramEventAggregator eventAggregator, TLEncryptedChat encryptedChat, int? layer, int? rawInSeqNo, int? rawOutSeqNo)
//        {
//            var newEncryptedChat = new TLEncryptedChat20();
//            newEncryptedChat.Layer = layer;

//            newEncryptedChat.RawInSeqNo = rawInSeqNo;
//            newEncryptedChat.RawOutSeqNo = rawOutSeqNo;

//            newEncryptedChat.Id = encryptedChat.Id;
//            newEncryptedChat.AccessHash = encryptedChat.AccessHash;
//            newEncryptedChat.Date = encryptedChat.Date;
//            newEncryptedChat.AdminId = encryptedChat.AdminId;
//            newEncryptedChat.ParticipantId = encryptedChat.ParticipantId;
//            newEncryptedChat.GAorB = encryptedChat.GAorB;

//            newEncryptedChat.CustomFlags = encryptedChat.CustomFlags;
//            if (encryptedChat.OriginalKey != null) newEncryptedChat.OriginalKey = encryptedChat.OriginalKey;
//            if (encryptedChat.ExtendedKey != null) newEncryptedChat.ExtendedKey = encryptedChat.ExtendedKey;
//            newEncryptedChat.Key = encryptedChat.Key;
//            newEncryptedChat.KeyFingerprint = encryptedChat.KeyFingerprint;
//            newEncryptedChat.P = encryptedChat.P;
//            newEncryptedChat.G = encryptedChat.G;
//            newEncryptedChat.A = encryptedChat.A;
//            newEncryptedChat.MessageTTL = encryptedChat.MessageTTL;

//            cacheService.SyncEncryptedChat(newEncryptedChat,
//                result =>
//                {
//                    eventAggregator.Publish(newEncryptedChat);

//                    var currentUserId = MTProtoService.Instance.CurrentUserId;
//                    var clientTicksDelta = MTProtoService.Instance.ClientTicksDelta;

//                    var randomId = TLLong.Random();

//                    var notifyLayerAction = new TLDecryptedMessageActionNotifyLayer();
//                    notifyLayerAction.Layer = new int?(Constants.SecretSupportedLayer);

//                    var inSeqNo = TLUtils.GetInSeqNo(currentUserId, newEncryptedChat);
//                    var outSeqNo = TLUtils.GetOutSeqNo(currentUserId, newEncryptedChat);

//                    var newEncryptedChat17 = cacheService.GetEncryptedChat(newEncryptedChat.Id) as TLEncryptedChat17;
//                    if (newEncryptedChat17 != null)
//                    {
//                        newEncryptedChat17.RawOutSeqNo = new int?(newEncryptedChat17.RawOutSeqNo.Value + 1);
//                    }

//                    var decryptedMessageService17 = new TLDecryptedMessageService17
//                    {
//                        Action = notifyLayerAction,
//                        RandomId = randomId,
//                        RandomBytes = TLString.Random(Constants.MinRandomBytesLength),

//                        ChatId = encryptedChat.Id,
//                        FromId = currentUserId,
//                        Out = true,
//                        Unread = false,
//                        Date = TLUtils.DateToUniversalTimeTLInt(clientTicksDelta, DateTime.Now),
//                        Status = TLMessageState.Sending,

//                        TTL = 0,
//                        InSeqNo = inSeqNo,
//                        OutSeqNo = outSeqNo
//                    };

//                    var decryptedMessageLayer17 = TLUtils.GetDecryptedMessageLayer(newEncryptedChat17.Layer, inSeqNo, outSeqNo, decryptedMessageService17);

//                    cacheService.SyncDecryptedMessage(
//                        decryptedMessageService17,
//                        encryptedChat,
//                        messageResult =>
//                        {
//                            sendEncryptedServiceAsync(
//                                new TLInputEncryptedChat
//                                {
//                                    AccessHash = encryptedChat.AccessHash,
//                                    ChatId = encryptedChat.Id
//                                },
//                                randomId,
//                                TLUtils.EncryptMessage(decryptedMessageLayer17, encryptedChat),
//                                sentEncryptedMessage =>
//                                {
//                                    decryptedMessageService17.Status = TLMessageState.Confirmed;
//                                    cacheService.SyncSendingDecryptedMessage(encryptedChat.Id, sentEncryptedMessage.Date, decryptedMessageService17.RandomId,
//                                        m =>
//                                        {
//#if DEBUG
//                                            eventAggregator.Publish(decryptedMessageService17);
//#endif
//                                        });
//                                },
//                                error =>
//                                {
//                                    Helpers.Execute.ShowDebugMessage("messages.sendEncryptedService error " + error);
//                                });
//                        });
//                });
//        }

//        public static int GetRawInFromReceivedMessage(int? currentUserId, TLEncryptedChat17 chat, ISeqNo message)
//        {
//            var isAdmin = chat.AdminId.Value == currentUserId.Value;
//            var x = isAdmin ? 0 : 1;
//            return (message.OutSeqNo.Value - x) / 2;
//        }

//        public static bool IsSyncRequierd(TLDecryptedMessageBase decryptedMessage)
//        {
//            return true;
//        }
        #endregion

        private readonly object _clientSeqLock = new object();

        private readonly Dictionary<int, Tuple<DateTime, TLUpdatesState>> _lostSeq = new Dictionary<int, Tuple<DateTime, TLUpdatesState>>();

        private void UpdateLostSeq(IList<int> seqList, bool cleanupMissingSeq = false)
        {
            lock (_clientSeqLock)
            {
                if (ClientSeq != null)
                {
                    if (seqList.Count > 0)
                    {
                        // add missing items
                        if (seqList[0] > ClientSeq.Value + 1)
                        {
                            for (var i = ClientSeq.Value + 1; i < seqList[0]; i++)
                            {
                                _lostSeq[i] = new Tuple<DateTime, TLUpdatesState>(DateTime.Now, new TLUpdatesState { Seq = ClientSeq.Value, Pts = _pts.Value, Date = _date.Value, Qts = _qts.Value });
                            }
                        }

                        // remove received items
                        for (var i = 0; i < seqList.Count; i++)
                        {
                            if (_lostSeq.ContainsKey(seqList[i]))
                            {
                                TLUtils.WriteLine(
                                    DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " remove from Missing Seq List seq=" +
                                    seqList[i] + " time=" + (DateTime.Now - _lostSeq[seqList[i]].Item1), LogSeverity.Error);
                                _lostSeq.Remove(seqList[i]);
                            }
                        }
                    }
                }

                // cleanup (updates.getDifference, set initState, etc)
                if (cleanupMissingSeq)
                {
                    _lostSeq.Clear();
                }

                if (seqList.Count > 0)
                {
                    var lastSeqValue = seqList.Last();
                    var maxSeqValue = Math.Max(lastSeqValue, ClientSeq != null ? ClientSeq.Value : -1);
                    ClientSeq = new int?(maxSeqValue);
                }

                if (_lostSeq.Count > 0)
                {
                    var missingSeqInfo = new StringBuilder();
                    foreach (var keyValue in _lostSeq)
                    {
                        missingSeqInfo.AppendLine(string.Format("seq={0}, date={1}", keyValue.Key,
                            keyValue.Value.Item1.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)));
                    }

                    StartLostSeqTimer();

                    TLUtils.WriteLine(
                        DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " Missing Seq List\n" +
                        missingSeqInfo, LogSeverity.Error);
                }
            }
        }

        private readonly object _clientPtsLock = new object();

        private readonly Dictionary<int, Tuple<DateTime, TLUpdatesState>> _lostPts = new Dictionary<int, Tuple<DateTime, TLUpdatesState>>();

        private void UpdateLostPts(IList<int> ptsList, bool cleanupMissingPts = false)
        {
            lock (_clientPtsLock)
            {
                if (_pts != null)
                {
                    if (ptsList.Count > 0)
                    {
                        // add missing items
                        if (ptsList[0] > _pts.Value + 1)
                        {
                            for (var i = _pts.Value + 1; i < ptsList[0]; i++)
                            {
                                _lostPts[i] = new Tuple<DateTime, TLUpdatesState>(DateTime.Now, new TLUpdatesState { Seq = ClientSeq.Value, Pts = _pts.Value, Date = _date.Value, Qts = _qts.Value });
                            }
                        }

                        // remove received items
                        for (var i = 0; i < ptsList.Count; i++)
                        {
                            if (_lostPts.ContainsKey(ptsList[i]))
                            {
                                TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " remove from Missing Pts List pts=" + ptsList[i] + " time=" + (DateTime.Now - _lostPts[ptsList[i]].Item1), LogSeverity.Error);
                                _lostPts.Remove(ptsList[i]);
                            }
                        }
                    }
                }

                // cleanup (updates.getDifference, set initState, etc)
                if (cleanupMissingPts)
                {
                    _lostPts.Clear();
                }

                if (ptsList.Count > 0)
                {
                    var lastPtsValue = ptsList.Last();
                    var maxPtsValue = Math.Max(lastPtsValue, _pts != null ? _pts.Value : -1);
                    _pts = new int?(maxPtsValue);
                }

                if (_lostPts.Count > 0)
                {
                    var missingPtsInfo = new StringBuilder();
                    foreach (var keyValue in _lostPts)
                    {
                        missingPtsInfo.AppendLine(string.Format("pts={0}, date={1}", keyValue.Key,
                            keyValue.Value.Item1.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)));
                    }

                    StartLostPtsTimer();

                    TLUtils.WriteLine(
                        DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " Missing Pts List\n" +
                        missingPtsInfo, LogSeverity.Error);
                }
            }
        }

        private void ProcessUpdates(IList<TLUpdatesBase> updatesList, IList<TLUpdatesTooLong> updatesTooLong = null, bool notifyNewMessage = true)
        {
            try
            {
#if DEBUG
                if (updatesTooLong != null && updatesTooLong.Count > 0)
                {
                    //NOTE to get AUTH_KEY_UNREGISTERED
                    GetStateAsync.SafeInvoke(
                        result =>
                        {

                        },
                        error =>
                        {
                            Helpers.Execute.ShowDebugMessage("account.updateStatus error " + error);
                        });
#if LOG_CLIENTSEQ
                    Helpers.Execute.ShowDebugMessage(string.Format("{0} updatesTooLong clientSeq={1} pts={2}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), ClientSeq, _pts));
                    TLUtils.WriteLine(string.Format("{0} updatesTooLong seq={1} pts={2}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), ClientSeq, _pts), LogSeverity.Error);
                    //TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " updatesTooLong clientSeq=" + ClientSeq, LogSeverity.Error);
#endif
                }
#endif

                var seqList = updatesList.SelectMany(updates => updates.GetSeq()).OrderBy(x => x).ToList();
                var ptsList = updatesList.SelectMany(updates => updates.GetPts()).OrderBy(x => x).ToList();

                /*#if DEBUG
                                if (seqList.Count > 0)
                                {
                                    var showDebugInfo = false;
                                    for (var i = 0; i < seqList.Count; i++)
                                    {
                                        if (seqList[i].Value == 0)
                                        {
                                            showDebugInfo = true;
                                            break;
                                        }
                                    }

                                    // only TLUpdateUserStatus here
                                    if (showDebugInfo)
                                    {
                                        var updateListInfo = new StringBuilder();
                                        foreach (var updatesBase in updateList)
                                        {
                                            updateListInfo.AppendLine(updatesBase.ToString());
                                        }
                                        Helpers.Execute.ShowDebugMessage("ProcessTransportMessage seqs=0 " + updateListInfo);
                                    }
                                }
                #endif*/

#if LOG_CLIENTSEQ
                if (ptsList.Count > 0 || seqList.Count > 0)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine(string.Format("{0} ProcessTransportMessage", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)));
                    builder.AppendLine(string.Format("clientSeq={0} seqList={1}", ClientSeq, seqList.Count == 0 ? "null" : string.Join(", ", seqList)));
                    builder.AppendLine(string.Format("pts={0} ptsList={1}", _pts, ptsList.Count == 0 ? "null" : string.Join(", ", ptsList)));
                    TLUtils.WriteLine(builder.ToString(), LogSeverity.Error);
                }
#endif
                if (GetDifferenceRequired(updatesList))
                {
                    var stopwatch = Stopwatch.StartNew();
                    Logs.Log.Write("UpdatesService.ProcessUpdates StartGetDifference");
                    GetDifference(1000, () =>
                    {
                        var elapsed = stopwatch.Elapsed;
                        Logs.Log.Write("UpdatesService.ProcessUpdates StopGetDifference time=" + elapsed);
                    });
                    return;
                }

                var processUpdates = false;
                if (updatesList.Count > 0)
                {
                    if (seqList.Count > 0)
                    {
                        UpdateLostSeq(seqList);
                    }

                    if (ptsList.Count > 0)
                    {
                        UpdateLostPts(ptsList);
                    }

                    processUpdates = true;
                }

                if (processUpdates)
                {
                    ProcessReading(updatesList);
                    foreach (var updatesItem in updatesList)
                    {
                        ProcessUpdatesInternal(updatesItem, notifyNewMessage);
                    }
                }

                return;
            }
            catch (Exception e)
            {
                TLUtils.WriteLine("Error during processing update: ", LogSeverity.Error);
                TLUtils.WriteException(e);
            }
        }

        private bool GetDifferenceRequired(IList<TLUpdatesBase> updatesList)
        {
            var getDifferenceRequired = false;
            for (int i = 0; i < updatesList.Count; i++)
            {
                var updatesShortChatMessage = updatesList[i] as TLUpdateShortChatMessage;
                if (updatesShortChatMessage != null)
                {
                    var user = _cacheService.GetUser(updatesShortChatMessage.FromId);
                    if (user == null)
                    {
                        var logString =
                            string.Format("ProcessUpdates.UpdatesShortChatMessage: user is missing (userId={0}, msgId={1})",
                                updatesShortChatMessage.FromId, updatesShortChatMessage.Id);
                        Logs.Log.Write(logString);
                        getDifferenceRequired = true;
                        break;
                    }
                    var chat = _cacheService.GetChat(updatesShortChatMessage.ChatId);
                    if (chat == null)
                    {
                        var logString =
                            string.Format("ProcessUpdates.UpdatesShortChatMessage: chat is missing (chatId={0}, msgId={1})",
                                updatesShortChatMessage.ChatId, updatesShortChatMessage.Id);
                        Logs.Log.Write(logString);
                        getDifferenceRequired = true;
                        break;
                    }
                }

                var updatesShortMessage = updatesList[i] as TLUpdateShortMessage;
                if (updatesShortMessage != null)
                {
                    var user = _cacheService.GetUser(updatesShortMessage.UserId);
                    if (user == null)
                    {
                        var logString =
                            string.Format("ProcessUpdates.UpdatesShortMessage: user is missing (userId={0}, msgId={1})",
                                updatesShortMessage.UserId, updatesShortMessage.Id);
                        Logs.Log.Write(logString);
                        getDifferenceRequired = true;
                        break;
                    }
                }
            }
            return getDifferenceRequired;
        }

        private void ProcessReading(IList<TLUpdatesBase> updatesList)
        {
            var readHistoryInboxList = new List<TLUpdateBase>();
            var readHistoryOutboxList = new List<TLUpdateBase>();

            var newChatMessageList = new List<TLUpdateNewMessage>();
            var newMessageList = new List<TLUpdateNewMessage>();
            var shortChatMessageList = new List<TLUpdateShortChatMessage>();
            var shortMessageList = new List<TLUpdateShortMessage>();

            foreach (var updatesBase in updatesList)
            {
                var updatesShort = updatesBase as TLUpdateShort;
                if (updatesShort != null)
                {
                    GetReadingUpdates(updatesShort.Update, readHistoryInboxList, readHistoryOutboxList, newChatMessageList, newMessageList);

                    continue;
                }

                var updates = updatesBase as TLUpdates;
                if (updates != null)
                {
                    foreach (var updateBase in updates.Updates)
                    {
                        GetReadingUpdates(updateBase, readHistoryInboxList, readHistoryOutboxList, newChatMessageList, newMessageList);
                    }

                    continue;
                }

                var shortChatMessage = updatesBase as TLUpdateShortChatMessage;
                if (shortChatMessage != null
                    && shortChatMessage.IsUnread)
                {
                    shortChatMessageList.Add(shortChatMessage);
                    continue;
                }

                var shortMessage = updatesBase as TLUpdateShortMessage;
                if (shortMessage != null
                    && shortMessage.IsUnread)
                {
                    shortMessageList.Add(shortMessage);
                    continue;
                }
            }

            ProcessReadingUpdates(false, readHistoryInboxList, shortChatMessageList, newChatMessageList, shortMessageList, newMessageList);
            ProcessReadingUpdates(true, readHistoryOutboxList, shortChatMessageList, newChatMessageList, shortMessageList, newMessageList);
        }

        private static void ProcessReadingUpdates(
            bool outbox, IList<TLUpdateBase> readHistoryList, 
            IList<TLUpdateShortChatMessage> shortChatMessageList, 
            IList<TLUpdateNewMessage> newChatMessageList,
            IList<TLUpdateShortMessage> shortMessageList, 
            IList<TLUpdateNewMessage> newMessageList)
        {
            if (readHistoryList.Count == 0) return;

            foreach (var readHistory in readHistoryList)
            {
                int maxId;
                TLPeerBase peer;
                if (readHistory is TLUpdateReadHistoryInbox)
                {
                    maxId = ((TLUpdateReadHistoryInbox)readHistory).MaxId;
                    peer = ((TLUpdateReadHistoryInbox)readHistory).Peer;
                }
                else
                {
                    maxId = ((TLUpdateReadHistoryOutbox)readHistory).MaxId;
                    peer = ((TLUpdateReadHistoryOutbox)readHistory).Peer;
                }

                var peerChat = peer as TLPeerChat;
                if (peerChat != null)
                {
                    for (var i = 0; i < shortChatMessageList.Count; i++)
                    {
                        if (shortChatMessageList[i].IsOut == outbox
                            && peerChat.Id == shortChatMessageList[i].ChatId
                            && maxId >= shortChatMessageList[i].Id)
                        {
                            shortChatMessageList[i].IsUnread = false;
                            shortChatMessageList.RemoveAt(i--);
                        }
                    }

                    for (var i = 0; i < newChatMessageList.Count; i++)
                    {
                        var message = newChatMessageList[i].Message as TLMessageCommonBase;
                        if (message != null && message.IsOut == outbox)
                        {
                            if (message.IsOut == outbox
                                && peerChat.Id == message.ToId.Id
                                && maxId >= message.Id)
                            {
                                message.SetUnreadSilent(false);
                                newChatMessageList.RemoveAt(i--);
                            }
                        }
                    }
                    continue;
                }

                var peerUser = peer as TLPeerUser;
                if (peerUser != null)
                {
                    for (var i = 0; i < shortMessageList.Count; i++)
                    {
                        if (shortMessageList[i].IsOut == outbox
                            && peerUser.Id == shortMessageList[i].UserId
                            && maxId >= shortMessageList[i].Id)
                        {
                            shortMessageList[i].IsUnread = false;
                            shortMessageList.RemoveAt(i--);
                        }
                    }

                    for (var i = 0; i < newMessageList.Count; i++)
                    {
                        var message = newMessageList[i].Message as TLMessageCommonBase;
                        if (message != null)
                        {
                            if (message.IsOut == outbox
                                && peerUser.Id == message.FromId.Value
                                && maxId >= message.Id)
                            {
                                message.SetUnreadSilent(false);
                                newMessageList.RemoveAt(i--);
                            }
                        }
                    }
                    continue;
                }
            }
        }

        private static void GetReadingUpdates(TLUpdateBase updateBase, 
            IList<TLUpdateBase> readHistoryInboxList, 
            IList<TLUpdateBase> readHistoryOutboxList,
            IList<TLUpdateNewMessage> newChatMessageList, 
            IList<TLUpdateNewMessage> newMessageList)
        {
            var readHistoryInbox = updateBase as TLUpdateReadHistoryInbox;
            if (readHistoryInbox != null)
            {
                readHistoryInboxList.Add(readHistoryInbox);
                return;
            }

            var readHistoryOutbox = updateBase as TLUpdateReadHistoryOutbox;
            if (readHistoryOutbox != null)
            {
                readHistoryOutboxList.Add(readHistoryOutbox);
                return;
            }

            var newMessage = updateBase as TLUpdateNewMessage;
            if (newMessage != null)
            {
                var message = newMessage.Message as TLMessageCommonBase;
                if (message != null
                    && message.IsUnread)
                {
                    var peerChat = message.ToId as TLPeerChat;
                    if (peerChat != null)
                    {
                        newChatMessageList.Add(newMessage);
                        return;
                    }

                    var peerUser = message.ToId as TLPeerUser;
                    if (peerUser != null)
                    {
                        newMessageList.Add(newMessage);
                        return;
                    }
                }
            }
        }

        public void ProcessUpdates(TLUpdatesBase updates, bool notifyNewMessages = false)
        {
            var updatesList = new List<TLUpdatesBase> { updates };

            var updatesTooLongList = new List<TLUpdatesTooLong>();
            var updatesTooLong = updates as TLUpdatesTooLong;
            if (updatesTooLong != null)
            {
                updatesTooLongList.Add(updatesTooLong);
            }

            ProcessUpdates(updatesList, updatesTooLongList, notifyNewMessages);
        }

        public void ProcessTransportMessage(TLTransportMessage transportMessage)
        {
            try
            {
                var isUpdating = false;
                lock (_getDifferenceRequestRoot)
                {
                    if (_getDifferenceRequests.Count > 0)
                    {
                        isUpdating = true;
                    }
                }

                if (isUpdating)
                {
                    //Execute.ShowDebugMessage("UpdatesService.ProcessTransportMessage Skip");
                    return;
                }

                var updatesList = TLUtils.FindInnerObjects<TLUpdatesBase>(transportMessage).ToList();
                var updatesTooLong = TLUtils.FindInnerObjects<TLUpdatesTooLong>(transportMessage).ToList();

                ProcessUpdates(updatesList, updatesTooLong);
            }
            catch (Exception e)
            {
                TLUtils.WriteLine("Error during processing update: ", LogSeverity.Error);
                TLUtils.WriteException(e);
            }
        }

        private readonly object _stateRoot = new object();

        public void LoadStateAndUpdate(Action callback)
        {
            var id = new Random().Next(999);
            TLUtils.WritePerformance(">>Loading current state and updating");

            if (_pts == null || _date == null || _qts == null)
            {
                var state = TLUtils.OpenObjectFromMTProtoFile<TLUpdatesState>(_stateRoot, Constants.StateFileName);
#if DEBUG_UPDATES
                state.Pts = 140000;
#endif

                SetState(state, "setFileState");
                TLUtils.WritePerformance("Current state: " + state);

                FileUtils.Copy(_stateRoot, Constants.StateFileName, Constants.TempStateFileName);
            }

            Logs.Log.Write(string.Format("UpdatesService.LoadStateAndUpdate {0} client_state=[p={1} d={2} q={3}]", id, _pts, _date, _qts));

            LoadFileState();

            var stopwatch = Stopwatch.StartNew();
            Logs.Log.Write(string.Format("UpdatesService.LoadStateAndUpdate {0} start GetDifference", id));
            AddRequest(id);
            //TLObject.LogNotify = true;
            //TelegramEventAggregator.LogPublish = true;

            GetDifference(id, () =>
            {
                var elapsed = stopwatch.Elapsed;
                Logs.Log.Write(string.Format("UpdatesService.LoadStateAndUpdate {0} stop GetDifference elapsed={1}", id, elapsed));


                //TLObject.LogNotify = false;
                //TelegramEventAggregator.LogPublish = false;
                RemoveRequest(id);
                callback.SafeInvoke();
            });
        }

        private readonly object _differenceSyncRoot = new object();

        private readonly object _differenceTimeSyncRoot = new object();

        private void LoadFileState()
        {
            var stopwatch = Stopwatch.StartNew();
            var difference = TLUtils.OpenObjectFromMTProtoFile<TLVector<TLUpdatesDifferenceBase>>(_differenceSyncRoot, Constants.DifferenceFileName);

            Logs.Log.Write("UpdatesService.LoadStateAndUpdate start LoadFileState");

            if (difference != null && difference.Count > 0)
            {
                CleanupDifference(difference);

                var ptsList = string.Join(", ", difference.OfType<TLUpdatesDifference>().Select(x => x.State.Pts));
                Logs.Log.Write(string.Format("UpdatesService.LoadStateAndUpdate ptsList=[{0}]", ptsList));

                foreach (var differenceBase in difference)
                {
                    var stopwatchProcessDiff = Stopwatch.StartNew();
                    var diff = differenceBase as TLUpdatesDifference;
                    if (diff != null)
                    {
                        var resetEvent = new ManualResetEvent(false);

                        lock (_clientSeqLock)
                        {
                            SetState(diff.State, "loadFileState");
                        }
                        ProcessDifference(diff, () => resetEvent.Set());
#if DEBUG
                        resetEvent.WaitOne();
#else
                        resetEvent.WaitOne(10000);
#endif

                        var otherInfo = new StringBuilder();
                        if (diff.OtherUpdates.Count > 0)
                        {
                            otherInfo.AppendLine();
                            for (var i = 0; i < diff.OtherUpdates.Count; i++)
                            {
                                otherInfo.AppendLine(diff.OtherUpdates[i].ToString());
                            }
                        }

                        Logs.Log.Write(string.Format("UpdatesService.LoadFileState processDiff state=[{0}] messages={1} other={2} elapsed={3}{4}", diff.State, diff.NewMessages.Count, diff.OtherUpdates.Count, stopwatchProcessDiff.Elapsed, otherInfo));
                    }
                }

                Logs.Log.Write("UpdatesService.LoadStateAndUpdate LoadFileState publish UpdateCompletedEventArgs");

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new UpdateCompletedEventArgs()));
            }

            Logs.Log.Write("UpdatesService.LoadStateAndUpdate stop LoadFileState elapsed=" + stopwatch.Elapsed);
            
            FileUtils.Copy(_differenceSyncRoot, Constants.DifferenceFileName, Constants.TempDifferenceFileName);
            FileUtils.Delete(_differenceSyncRoot, Constants.DifferenceFileName);
            FileUtils.Delete(_differenceTimeSyncRoot, Constants.DifferenceTimeFileName);
        }

        private void CleanupDifference(TLVector<TLUpdatesDifferenceBase> list)
        {
            var updateChannelTooLongCache = new Dictionary<int, int>();
            var updateChannelCache = new Dictionary<int, int>();

            foreach (var differenceBase in list)
            {
                var differenceSlice = differenceBase as TLUpdatesDifference;
                if (differenceSlice != null)
                {
                    var updates = differenceSlice.OtherUpdates;
                    for (var i = 0; i < updates.Count; i++)
                    {
                        var updateChannelTooLong = updates[i] as TLUpdateChannelTooLong;
                        if (updateChannelTooLong != null)
                        {
                            if (updateChannelTooLongCache.ContainsKey(updateChannelTooLong.ChannelId))
                            {
                                updates.RemoveAt(i--);
                            }
                            else
                            {
                                updateChannelTooLongCache[updateChannelTooLong.ChannelId] = updateChannelTooLong.ChannelId;
                            }
                        }
                    }
                }
            }

            foreach (var differenceBase in list)
            {
                var differenceSlice = differenceBase as TLUpdatesDifference;
                if (differenceSlice != null)
                {
                    var updates = differenceSlice.OtherUpdates;
                    for (var i = 0; i < updates.Count; i++)
                    {
                        var updateChannel = updates[i] as TLUpdateChannel;
                        if (updateChannel != null)
                        {
                            if (updateChannelTooLongCache.ContainsKey(updateChannel.ChannelId))
                            {
                                updates.RemoveAt(i--);
                            }
                            else if (updateChannelCache.ContainsKey(updateChannel.ChannelId))
                            {
                                updates.RemoveAt(i--);
                            }
                            else
                            {
                                updateChannelCache[updateChannel.ChannelId] = updateChannel.ChannelId;
                            }
                        }
                    }
                }
            }
        }

        public void SaveState()
        {
            TLUtils.WritePerformance("<<Saving current state");
            TLUtils.SaveObjectToMTProtoFile(_stateRoot, Constants.StateFileName, new TLUpdatesState { Date = _date ?? -1, Pts = _pts ?? -1, Qts = _qts ?? -1, Seq = ClientSeq ?? -1, UnreadCount = _unreadCount ?? -1 });
        }

        public TLUpdatesState GetState()
        {
            return new TLUpdatesState { Date = _date ?? -1, Pts = _pts ?? -1, Qts = _qts ?? -1, Seq = ClientSeq ?? -1, UnreadCount = _unreadCount ?? -1 };
        }

        public void SaveStateSnapshot(string toDirectoryName)
        {
            FileUtils.Copy(_differenceSyncRoot, Constants.TempStateFileName, Path.Combine(toDirectoryName, Constants.StateFileName));
            FileUtils.Copy(_differenceSyncRoot, Constants.TempDifferenceFileName, Path.Combine(toDirectoryName, Constants.DifferenceFileName));
        }

        public void LoadStateSnapshot(string fromDirectoryName)
        {
            var state = TLUtils.OpenObjectFromMTProtoFile<TLUpdatesState>(_stateRoot, Path.Combine(fromDirectoryName, Constants.StateFileName));
            if (state != null)
            {
                lock (_clientSeqLock)
                {
                    ClientSeq = state.Seq;
                }
                _date = state.Date < 0 ? _date : state.Date;
                _pts = state.Pts < 0 ? _pts : state.Pts;
                _qts = state.Qts < 0 ? _qts : state.Qts;
                _unreadCount = state.UnreadCount < 0 ? _unreadCount : state.UnreadCount;
                SaveState();
            }

            FileUtils.Copy(_differenceSyncRoot, Path.Combine(fromDirectoryName, Constants.DifferenceFileName), Constants.DifferenceFileName);
        }

        public void ClearState()
        {
            _date = null;
            _pts = null;
            _qts = null;
            ClientSeq = null;
            _unreadCount = null;
            FileUtils.Delete(_stateRoot, Constants.StateFileName);
        }
    }

    public class DCOptionsUpdatedEventArgs : EventArgs
    {
        public TLUpdateDCOptions Update { get; set; }
    }

    public class UpdatingEventArgs : EventArgs { }

    public class UpdateCompletedEventArgs : EventArgs
    {
        public IList<TLUpdateChannelTooLong> UpdateChannelTooLongList { get; set; } 
    }

    public class UpdateChannelsEventArgs : EventArgs
    {
        public IList<TLUpdateChannelTooLong> UpdateChannelTooLongList { get; set; }
    }

    public class ChannelUpdateCompletedEventArgs
    {
        public int ChannelId { get; set; }
    }
}
