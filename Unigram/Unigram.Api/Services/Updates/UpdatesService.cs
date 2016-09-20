#define LOG_CLIENTSEQ
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        public Action<long?> RemoveFromQueue { get; set; }
        public UpdateChannelAction UpdateChannelAsync { get; set; }
        public GetParticipantAction GetParticipantAsync { get; set; }
        public GetFullChatAction GetFullChatAsync { get; set; }

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

                Execute.ShowDebugMessage(string.Format("stub lostSeqTimer.getDifference(seq={0}, pts={1}, date={2}, qts={3}) localState=[seq={4}, pts={5}, date={6}, qts={7}]", seq, pts, date, qts, ClientSeq, _pts, _date, _qts));
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

                Execute.ShowDebugMessage(string.Format("stub lostSeqTimer.getDifference(seq={0}, pts={1}, date={2}, qts={3}) localState=[seq={4}, pts={5}, date={6}, qts={7}]", seq, pts, date, qts, ClientSeq, _pts, _date, _qts));
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

        private int? _date;

        private int? _pts;

        private int? _qts = new int?(1);

        private int? _unreadCount;

        public void SetState(int? seq, int? pts, int? qts, int? date, int? unreadCount, string caption, bool cleanupMissingCounts = false)
        {
#if LOG_CLIENTSEQ
            TLUtils.WriteLine(string.Format("{0} {1}\nclientSeq={2} newSeq={3}\npts={4} newPts={5}\n", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), caption, ClientSeq != null ? ClientSeq.ToString() : "null", seq, _pts != null ? _pts.ToString() : "null", pts), LogSeverity.Error);
            //TLUtils.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + caption + " clientSeq=" + ClientSeq + " newSeq=" + seq + " pts=" + pts, LogSeverity.Error);
#endif
            if (seq != null)
            {
                UpdateLostSeq(new List<int> { seq.Value }, cleanupMissingCounts);
            }

            _date = date ?? _date;

            if (pts != null)
            {
                UpdateLostPts(new List<int> { pts.Value }, cleanupMissingCounts);
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
            Log.Write(string.Format("UpdatesService.GetDifference {0} state=[p={1} d={2} q={3}]", id, _pts, _date, _qts));
            TLUtils.WritePerformance(string.Format("UpdatesService.GetDifference pts={0} date={1} qts={2}", _pts, _date, _qts));

            GetDifferenceAsync(pts.Value, date.Value, qts.Value,
                diff =>
                {
                    var processDiffStopwatch = Stopwatch.StartNew();

                    var differenceEmpty = diff as TLUpdatesDifferenceEmpty;
                    if (differenceEmpty != null)
                    {
#if LOG_CLIENTSEQ
                        TLUtils.WriteLine(string.Format("{0} {1} clientSeq={2} newSeq={3} pts={4}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), "processDiff empty", ClientSeq, differenceEmpty.Seq, _pts), LogSeverity.Error);
#endif
                        _date = differenceEmpty.Date;
                        lock (_clientSeqLock)
                        {
                            ClientSeq = differenceEmpty.Seq;
                        }

                        Log.Write(string.Format("UpdatesService.GetDifference {0} result {1} elapsed={2}", id, diff, processDiffStopwatch.Elapsed));

                        TLUtils.WritePerformance("UpdateService.GetDifference empty result=" + differenceEmpty.Seq);
                        callback();
                        return;
                    }

                    var difference = diff as TLUpdatesDifference;
                    if (difference != null)
                    {
                        //Logs.Log.Write("UpdatesService.Publish UpdatingEventArgs");
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new UpdatingEventArgs()));

                        var resetEvent = new ManualResetEvent(false);

                        TLUtils.WritePerformance(string.Format("UpdateService.GetDifference result=[Pts={0} Date={1} Qts={2}]", difference.State.Pts, difference.State.Date, difference.State.Qts));
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

                    Log.Write(string.Format("UpdatesService.GetDifference {0} result {1} elapsed={2}", id, diff, processDiffStopwatch.Elapsed));

                    var differenceSlice = diff as TLUpdatesDifferenceSlice;
                    if (differenceSlice != null)
                    {
                        GetDifference(id, callback);
                        //GetDifference(differenceSlice.State.Pts, differenceSlice.State.Date, differenceSlice.State.Qts, callback);
                    }
                    else
                    {
                        //Logs.Log.Write("UpdatesService.Publish UpdateCompletedEventArgs");
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new UpdateCompletedEventArgs()));
                        callback();
                    }
                },
                error =>
                {
                    Execute.BeginOnThreadPool(TimeSpan.FromSeconds(5.0), () =>
                    {
                        if (!RequestExists(id))
                        {
                            Log.Write(string.Format("UpdatesService.LoadStateAndUpdate {0} CancelGetDifference", id));
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
                    Log.Write(logString);
                    Execute.ShowDebugMessage(logString);
                    return false;
                }
                var chat = _cacheService.GetChat(updatesShortChatMessage.ChatId);
                if (chat == null)
                {
                    var logString = string.Format("ProcessUpdatesInternal.UpdatesShortChatMessage: chat is missing (chatId={0}, msgId={1})", updatesShortChatMessage.ChatId, updatesShortChatMessage.Id);
                    Log.Write(logString);
                    Execute.ShowDebugMessage(logString);
                    return false;
                }

                if (updatesShortChatMessage.Date > 0)
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
                    Log.Write(logString);
                    Execute.ShowDebugMessage(logString);
                    return false;
                }

                if (updatesShortMessage.Date > 0)
                {
                    _date = updatesShortMessage.Date;
                }

                ContinueShortMessage(updatesShortMessage, notifyNewMessage);

                return true;
            }

            var updatesShort = updatesBase as TLUpdateShort;
            if (updatesShort != null)
            {
                if (updatesShort.Date > 0)
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
                        if (updatesCombined.Date > 0)
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

                _cacheService.SyncUsersAndChats(updates.Users, updates.Chats,
                    result =>
                    {
                        if (updates.Date > 0)
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
                // TODO: Verify: message.FwdDate = shortMessage40.FwdDate;
                message.ReplyToMsgId = shortMessage40.ReplyToMsgId;
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

            _cacheService.SyncMessage(message, new TLPeerUser { Id = updatesShortMessage.UserId },
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
                // TODO: Verify: message.FwdDate = shortChatMessage40.FwdDate;
                message.ReplyToMsgId = shortChatMessage40.ReplyToMsgId;
            }

            var shortChatMessage34 = updatesShortChatMessage as TLUpdateShortChatMessage;
            if (shortChatMessage34 != null)
            {
                message.Entities = shortChatMessage34.Entities;
            }

            _cacheService.SyncMessage(message, new TLPeerChat { Id = updatesShortChatMessage.ChatId },
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
            var handler = DCOptionsUpdated;
            if (handler != null) handler(this, e);
        }

        private bool ProcessUpdateInternal(TLUpdateBase update, bool notifyNewMessage = true)
        {
            var userStatus = update as TLUpdateUserStatus;
            if (userStatus != null)
            {
                var user = _cacheService.GetUser(userStatus.UserId) as TLUser;
                if (user == null)
                {
                    return false;
                }

                user.Status = userStatus.Status;    // UI Thread
                user.RaisePropertyChanged(() => user.Status);

#warning
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(user));
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(userStatus));

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
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateServiceNotification));

                return true;
            }

            var updatePrivacy = update as TLUpdatePrivacy;
            if (updatePrivacy != null)
            {
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updatePrivacy));

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
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateUserBlocked));

                return true;
            }

            // TODO: Secrets: 
            //var processed = ProcessEncryptedChatUpdate(update);
            //if (processed != null)
            //{
            //    return processed.Value;
            //}

            var updateDCOptions = update as TLUpdateDCOptions;
            if (updateDCOptions != null)
            {
                RaiseDCOptionsUpdated(new DCOptionsUpdatedEventArgs { Update = updateDCOptions });

                return true;
            }

            var updateChannel = update as TLUpdateChannel;
            if (updateChannel != null)
            {

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

                                // TODO
                                //var inviter = result2.Participant as IChannelInviter;
                                //var inviterId = inviter != null ? inviter.InviterId.ToString() : "unknown";
                                //var date = inviter != null ? inviter.Date.ToString() : "unknown";
                                //Execute.ShowDebugMessage(string.Format("updateChannel [channel_id={0} creator={1} kicked={2} left={3} editor={4} moderator={5} broadcast={6} public={7} verified={8} inviter=[id={9} date={10}]]", channel.Id, channel.IsCreator, channel.IsKicked, channel.IsLeft, channel.IsEditor, channel.IsModerator, channel.IsBroadcast, channel.IsPublic, channel.IsVerified, inviterId, date));

                                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateChannel));
                            }, 
                            error2 =>
                            {
                                Execute.ShowDebugMessage("updateChannel getParticipant error " + error2);
                            });

                            //Execute.ShowDebugMessage(string.Format("updateChannel [channel_id={0} creator={1} kicked={2} left={3} editor={4} moderator={5} broadcast={6} public={7} verified={8} inviter_id={9}]", channel.Id, channel.Creator, channel.IsKicked, channel.Left, channel.IsEditor, channel.IsModerator, channel.IsBroadcast, channel.IsPublic, channel.IsVerified, channel.ExportedInvite));
                        }
                        else
                        {
                            Execute.ShowDebugMessage("updateChannel empty");
                        }
                    },
                    error =>
                    {
                        Execute.ShowDebugMessage("updateChannel getFullChannel error " + error);
                    });

                return true;
            }

            // TODO:
            //var updateChannelGroup = update as TLUpdateChannelGroup;
            //if (updateChannelGroup != null)
            //{
            //    Execute.ShowDebugMessage(string.Format("updateChannelGroup channel_id={0} min_id={1} max_id={2} count={3} date={4}", updateChannelGroup.ChannelId, updateChannelGroup.Group.MinId, updateChannelGroup.Group.MaxId, updateChannelGroup.Group.Count, updateChannelGroup.Group.Date));

            //    return true;
            //}

            var updateChannelTooLong = update as TLUpdateChannelTooLong;
            if (updateChannelTooLong != null)
            {
                Execute.ShowDebugMessage(string.Format("updateChannelTooLong channel_id={0}", updateChannelTooLong.ChannelId));

                return true;
            }

            var updateEditMessage = update as TLUpdateEditMessage;
            if (updateEditMessage != null)
            {
                _cacheService.SyncEditedMessage(updateEditMessage.Message, notifyNewMessage, notifyNewMessage, (cachedMessage) =>
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

                var commonMessage = updateEditChannelMessage.Message as TLMessage;
                if (commonMessage != null)
                {
                    var toId = commonMessage.ToId;
                    var channel = _cacheService.GetChat(commonMessage.ToId.Id) as TLChannel;
                    if (channel != null)
                    {
                        if (channel.Pts == null || (channel.Pts < updateEditChannelMessage.Pts && channel.Pts + updateEditChannelMessage.PtsCount != updateEditChannelMessage.Pts))
                        {
                            Execute.ShowDebugMessage(string.Format("channel_id={0} channel_pts={1} updateEditChannelMessage[channel_pts={2} channel_pts_count={3}]", toId.Id, channel.Pts, updateEditChannelMessage.Pts, updateEditChannelMessage.PtsCount));
                        }
                        channel.Pts = updateEditChannelMessage.Pts;
                    }

                    _cacheService.SyncEditedMessage(updateEditChannelMessage.Message, notifyNewMessage, notifyNewMessage, (cachedMessage) =>
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
                var commonMessage = updateNewChannelMessage.Message as TLMessage;
                if (commonMessage != null)
                {
                    var peer = commonMessage.ToId;

                    var channel = _cacheService.GetChat(commonMessage.ToId.Id) as TLChannel;
                    if (channel != null)
                    {
                        if (channel.Pts == null || channel.Pts != updateNewChannelMessage.Pts + 1)
                        {
                            Execute.ShowDebugMessage(string.Format("channel_id={0} pts={1} updateNewChannelMessage[pts={2} pts_count={3}]", peer.Id, channel.Pts, updateNewChannelMessage.Pts, updateNewChannelMessage.PtsCount));
                        }
                        channel.Pts = updateNewChannelMessage.Pts + updateNewChannelMessage.PtsCount;

                        var readInboxMaxId = channel.ReadInboxMaxId != null ? channel.ReadInboxMaxId : 0;

                        if (!commonMessage.IsOut && commonMessage.Id > readInboxMaxId)
                        {
                            commonMessage.IsUnread = true;
                        }
                    }

                    _cacheService.SyncMessage(updateNewChannelMessage.Message, peer, (cachedMessage) =>
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
                var commonMessage = updateNewMessage.Message as TLMessage;
                if (commonMessage != null)
                {
                    TLPeerBase peer;
                    if (commonMessage.ToId is TLPeerChat)
                    {
                        peer = commonMessage.ToId;
                    }
                    else
                    {
                        peer = commonMessage.IsOut ? commonMessage.ToId : new TLPeerUser { Id = commonMessage.FromId.Value };
                    }

                    if (commonMessage.RandomId != null && commonMessage.RandomId.Value != 0)
                    {
#if DEBUG
                        Log.Write("TLUpdateNewMessage " + updateNewMessage.Message);
#endif
                        _cacheService.SyncSendingMessage(updateNewMessage.Message as TLMessage, null, peer,
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

                        _cacheService.SyncMessage(updateNewMessage.Message, peer,
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

            // TODO: Fix this shit.
            var updateReadHistory = update as TLUpdateBase;
            if (update.TypeId == TLType.UpdateReadHistoryInbox || update.TypeId == TLType.UpdateReadHistoryOutbox)
            {
                var outbox = update is TLUpdateReadHistoryOutbox;
                var dialog = _cacheService.GetDialog(updateReadHistory.Peer);
                if (dialog != null)
                {
                    var notifyMessages = new List<TLMessage>();
                    var maxId = updateReadHistory.MaxId;
                    for (int i = 0; i < dialog.Messages.Count; i++)
                    {
                        var message = dialog.Messages[i] as TLMessage;
                        if (message != null)
                        {
                            if (message.Id != 0 &&
                                message.Id <= maxId &&
                                message.IsOut == outbox)
                            {
                                if (message.IsUnread)
                                {
                                    message.IsUnread = false;
                                    notifyMessages.Add(message);
                                    //message.NotifyOfPropertyChange(() => message.Unread);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }

                    var topMessage = dialog.TopMessageItem as TLMessage;
                    if (topMessage != null)
                    {
                        if (topMessage.Id <= maxId)
                        {
                            if (topMessage.Id != 0
                                && topMessage.IsUnread
                                && topMessage.IsOut == outbox)
                            {
                                topMessage.IsUnread = false;
                                notifyMessages.Add(topMessage);
                                //topMessage.NotifyOfPropertyChange(() => topMessage.Unread);
                            }
                        }
                    }

                    var unreadCount = 0;
                    if (dialog.TopMessage != null && dialog.TopMessage > updateReadHistory.MaxId)
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
                        if (message.Media != null)
                        {
                            // TODO:
                            //message.Media.NotListened = false;
                            //message.Media.NotifyOfPropertyChange(() => message.Media.NotListened);
                        }
                    }
                });

                return true;
            }

            var updateChannelMessageViews = update as TLUpdateChannelMessageViews;
            if (updateChannelMessageViews != null)
            {
                Execute.ShowDebugMessage(string.Format("updateChannelMessageViews channel_id={0} id={1} views={2}", updateChannelMessageViews.ChannelId, updateChannelMessageViews.Id, updateChannelMessageViews.Views));

                var message = _cacheService.GetMessage(updateChannelMessageViews.Id, updateChannelMessageViews.ChannelId) as TLMessage;
                if (message != null)
                {
                    if (message.Views < updateChannelMessageViews.Views)
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

            var updateReadChannelInbox = update as TLUpdateReadChannelInbox;
            if (updateReadChannelInbox != null)
            {
                var messages = new List<TLMessage>();

                var channel = _cacheService.GetChat(updateReadChannelInbox.ChannelId) as TLChannel;
                if (channel != null)
                {
                    channel.ReadInboxMaxId = updateReadChannelInbox.MaxId;
                }

                var dialog = _cacheService.GetDialog(new TLPeerChannel { Id = updateReadChannelInbox.ChannelId });
                if (dialog != null)
                {
                    var topMessage = dialog.TopMessageItem;
                    if (topMessage != null && topMessage.Id <= updateReadChannelInbox.MaxId)
                    {
                        dialog.UnreadCount = 0;

                        var topMessageCommon = topMessage as TLMessage;
                        if (topMessageCommon != null)
                        {
                            messages.Add(topMessageCommon);
                        }
                    }

                    foreach (var messageBase in dialog.Messages)
                    {
                        var message = messageBase as TLMessage;
                        if (message != null && message.IsUnread && !message.IsOut)
                        {
                            if (message.Id != 0 && message.Id < updateReadChannelInbox.MaxId)
                            {
                                messages.Add(message);
                            }
                        }
                    }

                    Execute.BeginOnUIThread(() =>
                    {
                        foreach (var message in messages)
                        {
                            message.IsUnread = false;
                            message.RaisePropertyChanged(() => message.IsUnread);
                        }

                        dialog.RaisePropertyChanged(() => dialog.TopMessageItem);
                        dialog.RaisePropertyChanged(() => dialog.Self);
                        dialog.RaisePropertyChanged(() => dialog.UnreadCount);
                    });
                }

                _cacheService.Commit();

                return true;
            }

            // TODO: Dunno
            //var updateReadMessages = update as TLUpdateReadMessages;
            //if (updateReadMessages != null)
            //{
            //    var dialogs = new Dictionary<int, TLDialogBase>();
            //    var messages = new List<TLMessage>(updateReadMessages.Messages.Count);
            //    foreach (var readMessageId in updateReadMessages.Messages)
            //    {
            //        var message = _cacheService.GetMessage(readMessageId) as TLMessage;
            //        if (message != null)
            //        {
            //            messages.Add(message);

            //            var dialog = _cacheService.GetDialog(message);
            //            if (dialog != null && dialog.UnreadCount > 0)
            //            {
            //                dialog.UnreadCount = Math.Max(0, dialog.UnreadCount - 1);
            //                var topMessage = dialog.TopMessage;
            //                if (topMessage == readMessageId.Value)
            //                {
            //                    dialogs[dialog.Peer.Id] = dialog;
            //                }
            //            }
            //        }
            //    }

            //    Execute.BeginOnUIThread(() =>
            //    {
            //        foreach (var message in messages)
            //        {
            //            message.IsUnread = false;
            //            message.NotifyOfPropertyChange(() => message.IsUnread);
            //        }

            //        foreach (var dialogBase in dialogs.Values)
            //        {
            //            var dialog = dialogBase as TLDialog;
            //            if (dialog == null) continue;

            //            dialog.NotifyOfPropertyChange(() => dialog.TopMessage);
            //            dialog.NotifyOfPropertyChange(() => dialog.Self);
            //            dialog.NotifyOfPropertyChange(() => dialog.UnreadCount);
            //        }
            //    });

            //    return true;
            //}

            var deleteMessages = update as TLUpdateDeleteMessages;
            if (deleteMessages != null)
            {
                _cacheService.DeleteMessages(deleteMessages.Messages);

                return true;
            }

            var updateDeleteChannelMessages = update as TLUpdateDeleteChannelMessages;
            if (updateDeleteChannelMessages != null)
            {
                Execute.ShowDebugMessage(string.Format("updateDeleteChannelMessages channel_id={0} msgs=[{1}] pts={2} pts_count={3}", updateDeleteChannelMessages.ChannelId, string.Join(", ", updateDeleteChannelMessages.Messages), updateDeleteChannelMessages.Pts, updateDeleteChannelMessages.PtsCount));

                var channel = _cacheService.GetChat(updateDeleteChannelMessages.ChannelId) as TLChannel;
                if (channel != null)
                {
                    if (channel.Pts == null || channel.Pts != updateDeleteChannelMessages.Pts + 1)
                    {
                        Execute.ShowDebugMessage(string.Format("channel_id={0} pts={1} updateDeleteChannelMessages[pts={2} pts_count={3}]", channel.Id, channel.Pts, updateDeleteChannelMessages.Pts, updateDeleteChannelMessages.PtsCount));
                    }
                    channel.Pts = updateDeleteChannelMessages.Pts + updateDeleteChannelMessages.PtsCount;
                }

                _cacheService.DeleteChannelMessages(updateDeleteChannelMessages.ChannelId, updateDeleteChannelMessages.Messages);

                return true;
            }

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
                user.Username = userName.Username;

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(userName));

                return true;
            }

            var userPhoto = update as TLUpdateUserPhoto;
            if (userPhoto != null)
            {
                var user = _cacheService.GetUser(userPhoto.UserId) as TLUser;
                if (user == null)
                {
                    return false;
                }

                user.Photo = userPhoto.Photo;

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(userPhoto));
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

                Execute.BeginOnThreadPool(() => user.RaisePropertyChanged(() => user.Phone));

                return true;
            }

            var contactRegistered = update as TLUpdateContactRegistered;
            if (contactRegistered != null)
            {
                if (contactRegistered.Date > 0)
                {
                    _date = contactRegistered.Date;
                }
                
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(contactRegistered));

                return true;
            }

            var updateNewAuthorization = update as TLUpdateNewAuthorization;
            if (updateNewAuthorization != null)
            {
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateNewAuthorization));

                return true;
            }

            var contactLink = update as TLUpdateContactLink;
            if (contactLink != null)
            {
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

                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateNotifySettings));
                    }
                }

                return true;
            }

            var updateWebPage = update as TLUpdateWebPage;
            if (updateWebPage != null)
            {
                var message = _cacheService.GetMessage(updateWebPage.Webpage) as TLMessage;
                if (message != null)
                {
                    message.Media = new TLMessageMediaWebPage { Webpage = updateWebPage.Webpage };
                    message.HasMedia = true;

                    TLPeerBase peer;
                    if (message.ToId is TLPeerChat)
                    {
                        peer = message.ToId;
                    }
                    else
                    {
                        peer = message.IsOut ? message.ToId : new TLPeerUser { Id = message.FromId.Value };
                    }

                    _cacheService.SyncMessage(message, peer,
                        m =>
                        {
                            Execute.BeginOnUIThread(() => message.RaisePropertyChanged(() => message.Media));
                        });
                }
                
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(updateWebPage));

                return true;
            }

            var updateDraftMessage = update as TLUpdateDraftMessage;
            if (updateDraftMessage != null)
            {
                Execute.ShowDebugMessage("TLUpdateDraftMessage draft=" + updateDraftMessage.Draft);

                var dialog = _cacheService.GetDialog(updateDraftMessage.Peer);
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

        #region Secret chats

//        public static TLDecryptedMessageBase GetDecryptedMessage(TLEncryptedChat cachedChat, TLEncryptedMessageBase encryptedMessageBase, int? qts, out bool commitChat)
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

//            var currentUserId = MTProtoService.Instance.CurrentUserId;
//            var participantId = currentUserId.Value == cachedChat.ParticipantId.Value
//                ? cachedChat.AdminId
//                : cachedChat.ParticipantId;
//            var cachedUser = InMemoryCacheService.Instance.GetUser(participantId);
//            if (cachedUser == null) return null;

//            decryptedMessage.FromId = cachedUser.Id;
//            decryptedMessage.Out = new bool(false);
//            decryptedMessage.Unread = new bool(true);
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

//        private bool? ProcessEncryptedChatUpdate(TLUpdateBase update)
//        {
//            // typing
//            var updateEncryptedChatTyping = update as TLUpdateEncryptedChatTyping;
//            if (updateEncryptedChatTyping != null)
//            {
//                _eventAggregator.Publish(updateEncryptedChatTyping);

//                return true;
//            }

//            // reading
//            var updateEncryptedMessagesRead = update as TLUpdateEncryptedMessagesRead;
//            if (updateEncryptedMessagesRead != null)
//            {
//                //Helpers.Execute.ShowDebugMessage(updateEncryptedMessagesRead.ToString());

//                var encryptedChat = _cacheService.GetEncryptedChat(updateEncryptedMessagesRead.ChatId) as TLEncryptedChat;

//                if (encryptedChat != null)
//                {
//                    var items = _cacheService.GetDecryptedHistory(encryptedChat.Id, 100);
//                    Execute.BeginOnUIThread(() =>
//                    {
//                        for (var i = 0; i < items.Count; i++)
//                        {
//                            if (items[i].Out.Value)
//                            {
//                                if (items[i].Status == MessageStatus.Confirmed)
//                                //&& Items[i].Date.Value <= update.MaxDate.Value) // здесь надо учитывать смещение по времени
//                                {
//                                    items[i].Status = MessageStatus.Read;
//                                    items[i].NotifyOfPropertyChange(() => items[i].Status);

//                                    if (items[i].TTL != null && items[i].TTL.Value > 0)
//                                    {
//                                        var decryptedMessage = items[i] as TLDecryptedMessage17;
//                                        if (decryptedMessage != null)
//                                        {
//                                            var decryptedPhoto = decryptedMessage.Media as TLDecryptedMessageMediaPhoto;
//                                            if (decryptedPhoto != null && items[i].TTL.Value <= 60.0)
//                                            {
//                                                continue;
//                                            }

//                                            var decryptedVideo17 = decryptedMessage.Media as TLDecryptedMessageMediaVideo17;
//                                            if (decryptedVideo17 != null && items[i].TTL.Value <= 60.0)
//                                            {
//                                                continue;
//                                            }

//                                            var decryptedAudio17 = decryptedMessage.Media as TLDecryptedMessageMediaAudio17;
//                                            if (decryptedAudio17 != null && items[i].TTL.Value <= 60.0)
//                                            {
//                                                continue;
//                                            }
//                                        }

//                                        items[i].DeleteDate = new long?(DateTime.Now.Ticks + encryptedChat.MessageTTL.Value * TimeSpan.TicksPerSecond);
//                                    }
//                                }
//                                else if (items[i].Status == MessageStatus.Read)
//                                {
//                                    var message = items[i] as TLDecryptedMessage;
//                                    if (message != null)
//                                    {
//                                        break;
//                                    }
//                                }
//                            }
//                        }

//                        var dialog = _cacheService.GetEncryptedDialog(encryptedChat.Id) as TLEncryptedDialog;
//                        if (dialog != null)
//                        {
//                            //dialog.UnreadCount = new int?(dialog.UnreadCount.Value - 1);
//                            var topMessage = dialog.TopMessage;
//                            if (topMessage != null)
//                            {
//                                dialog.NotifyOfPropertyChange(() => dialog.TopMessage);
//                            }
//                        }
//                    });
//                }

//                //_eventAggregator.Publish(updateEncryptedMessagesRead);

//                return true;
//            }

//            // message
//            var updateNewEncryptedMessage = update as TLUpdateNewEncryptedMessage;
//            if (updateNewEncryptedMessage != null)
//            {
//                var encryptedMessageBase = updateNewEncryptedMessage.Message;
//                if (encryptedMessageBase != null)
//                {
//                    var encryptedChat = _cacheService.GetEncryptedChat(encryptedMessageBase.ChatId) as TLEncryptedChat;
//                    if (encryptedChat == null)
//                    {
//                        return true;
//                    }

//                    TLDecryptedMessageBase decryptedMessage = null;
//                    try
//                    {
//                        bool commitChat;
//                        decryptedMessage = GetDecryptedMessage(encryptedChat, encryptedMessageBase, updateNewEncryptedMessage.Qts, out commitChat);
//                        if (commitChat)
//                        {
//                            _cacheService.Commit();
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Execute.ShowDebugMessage("ProcessUpdate(TLUpdateNewEncryptedMessage) ex " + ex);
//                    }

//                    if (decryptedMessage == null) return true;

//                    var hasMessagesGap = true;
//                    var decryptedMessage17 = decryptedMessage as ISeqNo;
//                    var decryptedMessageService = decryptedMessage as TLDecryptedMessageService;
//                    var encryptedChat17 = encryptedChat as TLEncryptedChat17;
//                    var encryptedChat20 = encryptedChat as TLEncryptedChat20;
//                    var encryptedChat8 = encryptedChat;

//                    var isDisplayedMessage = TLUtils.IsDisplayedDecryptedMessageInternal(decryptedMessage);
//                    if (!isDisplayedMessage)
//                    {
//                        decryptedMessage.Unread = bool.False;
//                    }

//                    ProcessPFS(encryptedChat20, decryptedMessageService);

//                    if (decryptedMessage17 != null)
//                    {
//                        // если чат уже обновлен до нового слоя, то проверяем rawInSeqNo
//                        if (encryptedChat17 != null)
//                        {
//                            var chatRawInSeqNo = encryptedChat17.RawInSeqNo.Value;
//                            var messageRawInSeqNo = GetRawInFromReceivedMessage(MTProtoService.Instance.CurrentUserId, encryptedChat17, decryptedMessage17);

//                            if (messageRawInSeqNo == chatRawInSeqNo)
//                            {
//                                hasMessagesGap = false;
//                                encryptedChat17.RawInSeqNo = new int?(encryptedChat17.RawInSeqNo.Value + 1);
//                                _cacheService.SyncEncryptedChat(encryptedChat17, result => { });
//                            }
//                            else
//                            {
//                                Execute.ShowDebugMessage(string.Format("TLUpdateNewEncryptedMessage messageRawInSeqNo != chatRawInSeqNo + 1 chatId={0} chatRawInSeqNo={1} messageRawInSeqNo={2}", encryptedChat17.Id, chatRawInSeqNo, messageRawInSeqNo));
//                            }
//                        }
//                        // обновляем до нового слоя при получении любого сообщения с более высоким слоем
//                        else if (encryptedChat8 != null)
//                        {
//                            hasMessagesGap = false;

//                            var newLayer = Constants.SecretSupportedLayer;
//                            if (decryptedMessageService != null)
//                            {
//                                var actionNotifyLayer = decryptedMessageService.Action as TLDecryptedMessageActionNotifyLayer;
//                                if (actionNotifyLayer != null)
//                                {
//                                    if (actionNotifyLayer.Layer.Value <= Constants.SecretSupportedLayer)
//                                    {
//                                        newLayer = actionNotifyLayer.Layer.Value;
//                                    }
//                                }
//                            }

//                            var layer = new int?(newLayer);
//                            var rawInSeqNo = new int?(1);      // только что получил сообщение по новому слою
//                            var rawOutSeqNo = new int?(0);

//                            UpgradeSecretChatLayerAndSendNotification(encryptedChat8, layer, rawInSeqNo, rawOutSeqNo);
//                        }
//                    }
//                    else if (decryptedMessageService != null)
//                    {
//                        hasMessagesGap = false;
//                        var notifyLayerAction = decryptedMessageService.Action as TLDecryptedMessageActionNotifyLayer;
//                        if (notifyLayerAction != null)
//                        {
//                            if (encryptedChat17 != null)
//                            {
//                                // пропукаем апдейт, т.к. уже обновились
//                            }
//                            else if (encryptedChat8 != null)
//                            {
//                                var newLayer = Constants.SecretSupportedLayer;
//                                if (notifyLayerAction.Layer.Value <= Constants.SecretSupportedLayer)
//                                {
//                                    newLayer = notifyLayerAction.Layer.Value;
//                                }

//                                var layer = new int?(newLayer);
//                                var rawInSeqNo = new int?(0);
//                                var rawOutSewNo = new int?(0);

//                                UpgradeSecretChatLayerAndSendNotification(encryptedChat8, layer, rawInSeqNo, rawOutSewNo);
//                            }
//                        }
//                    }
//                    else
//                    {
//                        hasMessagesGap = false;
//                    }

//                    if (hasMessagesGap)
//                    {
//                        Execute.ShowDebugMessage("catch gap " + decryptedMessage);
//                        //return true;
//                    }

//                    var decryptedMessageService17 = decryptedMessage as TLDecryptedMessageService17;
//                    if (decryptedMessageService17 != null)
//                    {
//                        var resendAction = decryptedMessageService17.Action as TLDecryptedMessageActionResend;
//                        if (resendAction != null)
//                        {
//                            Execute.ShowDebugMessage(string.Format("TLDecryptedMessageActionResend start_seq_no={0} end_seq_no={1}", resendAction.StartSeqNo, resendAction.EndSeqNo));

//                            //_cacheService.GetDecryptedHistory()
//                        }
//                    }

//                    var syncMessageFlag = IsSyncRequierd(decryptedMessage);

//                    _eventAggregator.Publish(decryptedMessage);

//                    if (syncMessageFlag)
//                    {
//                        _cacheService.SyncDecryptedMessage(decryptedMessage, encryptedChat, cachedMessage =>
//                        {
//                            SetState(null, null, updateNewEncryptedMessage.Qts, null, null, "TLUpdateNewEncryptedMessage");
//                        });
//                    }
//                    else
//                    {
//                        SetState(null, null, updateNewEncryptedMessage.Qts, null, null, "TLUpdateNewEncryptedMessage");
//                    }

//                    return true;
//                }
//            }

//            // creating, new layer
//            var updateEncryption = update as TLUpdateEncryption;
//            if (updateEncryption != null)
//            {
//                var chatRequested = updateEncryption.Chat as TLEncryptedChatRequested;

//                if (chatRequested != null)
//                {
//                    _cacheService.SyncEncryptedChat(updateEncryption.Chat, result => _eventAggregator.Publish(result));

//                    var message = new TLDecryptedMessageService
//                    {
//                        RandomId = TLLong.Random(),
//                        RandomBytes = new string(""),
//                        ChatId = chatRequested.Id,
//                        Action = new TLDecryptedMessageActionEmpty(),
//                        FromId = MTProtoService.Instance.CurrentUserId,
//                        Date = chatRequested.Date,
//                        Out = new bool(false),
//                        Unread = new bool(false),
//                        Status = MessageStatus.Read
//                    };

//                    _cacheService.SyncDecryptedMessage(message, chatRequested, result => { });

//                    GetDHConfigAsync(new int?(0), new int?(0),
//                        result =>
//                        {
//                            var dhConfig = (TLDHConfig)result;
//                            if (!TLUtils.CheckPrime(dhConfig.P.Data, dhConfig.G.Value))
//                            {
//                                return;
//                            }
//                            if (!TLUtils.CheckGaAndGb(chatRequested.GA.Data, dhConfig.P.Data))
//                            {
//                                return;
//                            }

//                            //TODO: precalculate gb to improve speed
//                            var bBytes = new byte[256];
//                            var random = new SecureRandom();
//                            random.NextBytes(bBytes);
//                            //var b = string.FromBigEndianData(bBytes);
//                            var p = dhConfig.P;
//                            var g = dhConfig.G;

//                            updateEncryption.Chat.P = p;
//                            updateEncryption.Chat.G = g;

//                            var gbBytes = MTProtoService.GetGB(bBytes, dhConfig.G, dhConfig.P);
//                            var gb = string.FromBigEndianData(gbBytes);

//                            var key = MTProtoService.GetAuthKey(bBytes, chatRequested.GA.ToBytes(), dhConfig.P.ToBytes());
//                            var keyHash = Utils.ComputeSHA1(key);
//                            var keyFingerprint = new long?(BitConverter.ToInt64(keyHash, 12));

//                            AcceptEncryptionAsync(
//                                new TLInputEncryptedChat
//                                {
//                                    AccessHash = chatRequested.AccessHash,
//                                    ChatId = chatRequested.Id
//                                },
//                                gb,
//                                keyFingerprint,
//                                chat =>
//                                {
//                                    chat.P = p;
//                                    chat.G = g;
//                                    chat.Key = string.FromBigEndianData(key);
//                                    chat.KeyFingerprint = keyFingerprint;

//                                    _cacheService.SyncEncryptedChat(chat, r2 => _eventAggregator.Publish(r2));
//                                },
//                                er =>
//                                {
//                                    Execute.ShowDebugMessage("messages.acceptEncryption " + er);
//                                });

//                        },
//                        error =>
//                        {
//                            Execute.ShowDebugMessage("messages.getDhConfig error " + error);
//                        });
//                }

//                var encryptedChat = updateEncryption.Chat as TLEncryptedChat;
//                if (encryptedChat != null)
//                {
//                    var waitingChat = _cacheService.GetEncryptedChat(encryptedChat.Id) as TLEncryptedChatWaiting;

//                    if (waitingChat != null)
//                    {
//                        var dialog = _cacheService.GetEncryptedDialog(encryptedChat.Id) as TLEncryptedDialog;
//                        if (dialog != null)
//                        {
//                            var serviceMessage = dialog.TopMessage as TLDecryptedMessageService;
//                            if (serviceMessage != null)
//                            {
//                                var action = serviceMessage.Action as TLDecryptedMessageActionEmpty;
//                                if (action != null)
//                                {
//                                    serviceMessage.Unread = new bool(true);
//                                    serviceMessage.Status = MessageStatus.Confirmed;
//                                }
//                            }
//                        }


//                        // уведомление о слое, если начали сами чат
//                        if (Constants.SecretSupportedLayer >= 17)
//                        {
//                            var currentUserId = MTProtoService.Instance.CurrentUserId;
//                            var clientTicksDelta = MTProtoService.Instance.ClientTicksDelta;

//                            var notifyLayerAction = new TLDecryptedMessageActionNotifyLayer();
//                            notifyLayerAction.Layer = new int?(Constants.SecretSupportedLayer);

//                            // уведомляем в старом слое, чтобы не сломать предыдущие версии клиентов
//                            var notifyLayerMessage = new TLDecryptedMessageService
//                            {
//                                Action = notifyLayerAction,
//                                RandomId = TLLong.Random(),
//                                RandomBytes = string.Empty,

//                                FromId = currentUserId,
//                                Out = bool.True,
//                                Unread = bool.False,
//                                Date = TLUtils.DateToUniversalTimeTLInt(clientTicksDelta, DateTime.Now),
//                                Status = MessageStatus.Confirmed,

//                                ChatId = encryptedChat.Id
//                            };

//                            _cacheService.SyncEncryptedChat(encryptedChat,
//                                syncedChat =>
//                                {
//                                    _cacheService.SyncDecryptedMessage(notifyLayerMessage, syncedChat,
//                                        messageResult =>
//                                        {
//                                            SendEncryptedServiceAsync(
//                                                new TLInputEncryptedChat
//                                                {
//                                                    AccessHash = encryptedChat.AccessHash,
//                                                    ChatId = encryptedChat.Id
//                                                },
//                                                notifyLayerMessage.RandomId,
//                                                TLUtils.EncryptMessage(notifyLayerMessage, (TLEncryptedChat)syncedChat),
//                                                sentEncryptedMessage =>
//                                                {
//                                                    notifyLayerMessage.Status = MessageStatus.Confirmed;
//                                                    _cacheService.SyncSendingDecryptedMessage(encryptedChat.Id, sentEncryptedMessage.Date, notifyLayerMessage.RandomId, m => { });
//                                                },
//                                                error =>
//                                                {
//                                                    Execute.ShowDebugMessage("messages.sendEncryptedService error " + error);
//                                                });
//                                        });
//                                });
//                        }
//                    }

//                    var encryptedChat17 = _cacheService.GetEncryptedChat(encryptedChat.Id) as TLEncryptedChat17;
//                    if (encryptedChat17 != null)
//                    {
//                        updateEncryption.Chat = encryptedChat17;
//                    }

//                    _cacheService.SyncEncryptedChat(updateEncryption.Chat,
//                        r =>
//                        {
//                            _eventAggregator.Publish(r);
//                        });
//                }
//                else
//                {
//                    _cacheService.SyncEncryptedChat(updateEncryption.Chat,
//                        r =>
//                        {
//                            _eventAggregator.Publish(r);
//                        });
//                }

//                return true;
//            }

//            return null;
//        }

//        private void ProcessPFS(TLEncryptedChat20 encryptedChat, TLDecryptedMessageService decryptedMessageService)
//        {
//            if (encryptedChat == null) return;
//            if (decryptedMessageService == null) return;

//            var abortKey = decryptedMessageService.Action as TLDecryptedMessageActionAbortKey;
//            if (abortKey != null)
//            {
//                encryptedChat.PFS_A = null;
//                encryptedChat.PFS_ExchangeId = null;
//                _cacheService.SyncEncryptedChat(encryptedChat, cachedChat =>
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
//                _cacheService.SyncEncryptedChat(encryptedChat, cachedChat =>
//                {
//                    _eventAggregator.Publish(encryptedChat);

//                    var actionNoop = new TLDecryptedMessageActionNoop();

//                    SendEncryptedServiceActionAsync(encryptedChat, actionNoop,
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
//                var gb = string.FromBigEndianData(gbBytes);

//                encryptedChat.PFS_A = string.FromBigEndianData(bBytes);
//                encryptedChat.PFS_ExchangeId = requestKey.ExchangeId;

//                if (!TLUtils.CheckGaAndGb(requestKey.GA.Data, encryptedChat.P.Data))
//                {
//                    return;
//                }

//                var key = MTProtoService.GetAuthKey(encryptedChat.PFS_A.Data, requestKey.GA.ToBytes(), encryptedChat.P.ToBytes());
//                var keyHash = Utils.ComputeSHA1(key);
//                var keyFingerprint = new long?(BitConverter.ToInt64(keyHash, 12));

//                encryptedChat.PFS_Key = string.FromBigEndianData(key);
//                encryptedChat.PFS_KeyFingerprint = keyFingerprint;
//                _cacheService.SyncEncryptedChat(encryptedChat, cachedChat =>
//                {
//                    var actionAcceptKey = new TLDecryptedMessageActionAcceptKey
//                    {
//                        ExchangeId = encryptedChat.PFS_ExchangeId,
//                        KeyFingerprint = keyFingerprint,
//                        GB = gb
//                    };

//                    SendEncryptedServiceActionAsync(encryptedChat, actionAcceptKey,
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

//                    SendEncryptedServiceActionAsync(encryptedChat, actionAbortKey,
//                        (message, result) =>
//                        {
//                            encryptedChat.PFS_A = null;
//                            encryptedChat.PFS_ExchangeId = null;

//                            _eventAggregator.Publish(encryptedChat);
//                            _cacheService.Commit();
//                        });

//                    return;
//                }

//                encryptedChat.PFS_Key = string.FromBigEndianData(key);
//                encryptedChat.PFS_KeyFingerprint = keyFingerprint;
//                _cacheService.SyncEncryptedChat(encryptedChat, cachedChat =>
//                {
//                    var actionCommitKey = new TLDecryptedMessageActionCommitKey
//                    {
//                        ExchangeId = encryptedChat.PFS_ExchangeId,
//                        KeyFingerprint = keyFingerprint
//                    };

//                    SendEncryptedServiceActionAsync(encryptedChat, actionCommitKey,
//                        (message, result) =>
//                        {
//                            encryptedChat.PFS_ExchangeId = null;
//                            encryptedChat.Key = encryptedChat.PFS_Key;
//                            encryptedChat.PFS_A = null;
//                            encryptedChat.PFS_KeyFingerprint = null;
//                            _cacheService.SyncEncryptedChat(encryptedChat, cachedChat2 =>
//                            {
//                                _eventAggregator.Publish(encryptedChat);
//                            });
//                        });
//                });

//                return;
//            }
//        }

//        private void SendEncryptedServiceActionAsync(TLEncryptedChat20 encryptedChat, TLDecryptedMessageActionBase action, Action<TLDecryptedMessageBase, TLSentEncryptedMessage> callback)
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
//                RandomBytes = string.Empty,
//                ChatId = encryptedChat.Id,
//                FromId = currentUserId,
//                Out = bool.True,
//                Unread = bool.False,
//                Date = TLUtils.DateToUniversalTimeTLInt(clientTicksDelta, DateTime.Now),
//                Status = MessageStatus.Sending,
//                TTL = new int?(0),
//                InSeqNo = inSeqNo,
//                OutSeqNo = outSeqNo
//            };

//            var decryptedMessageLayer17 = new TLDecryptedMessageLayer17();
//            decryptedMessageLayer17.Layer = new int?(Constants.SecretSupportedLayer);
//            decryptedMessageLayer17.InSeqNo = inSeqNo;
//            decryptedMessageLayer17.OutSeqNo = outSeqNo;
//            decryptedMessageLayer17.RandomBytes = string.Empty;
//            decryptedMessageLayer17.Message = message;

//            _cacheService.SyncDecryptedMessage(
//                message,
//                encryptedChat,
//                messageResult =>
//                {
//                    SendEncryptedServiceAsync(
//                        new TLInputEncryptedChat
//                        {
//                            AccessHash = encryptedChat.AccessHash,
//                            ChatId = encryptedChat.Id
//                        },
//                        randomId,
//                        TLUtils.EncryptMessage(decryptedMessageLayer17, encryptedChat),
//                        result =>
//                        {
//                            message.Status = MessageStatus.Confirmed;
//                            _cacheService.SyncSendingDecryptedMessage(encryptedChat.Id, result.Date, message.RandomId,
//                                m =>
//                                {
//#if DEBUG
//                                    _eventAggregator.Publish(message);
//#endif
//                                    callback.SafeInvoke(message, result);
//                                });
//                        },
//                        error => { Execute.ShowDebugMessage("messages.sendEncryptedService error " + error); });
//                });
//        }

//        private void UpgradeSecretChatLayerAndSendNotification(TLEncryptedChat encryptedChat, int? layer, int? rawInSeqNo, int? rawOutSeqNo)
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

//            newEncryptedChat.Key = encryptedChat.Key;
//            newEncryptedChat.KeyFingerprint = encryptedChat.KeyFingerprint;
//            newEncryptedChat.P = encryptedChat.P;
//            newEncryptedChat.G = encryptedChat.G;
//            newEncryptedChat.A = encryptedChat.A;
//            newEncryptedChat.MessageTTL = encryptedChat.MessageTTL;
//            newEncryptedChat.FileName = encryptedChat.FileName;

//            _cacheService.SyncEncryptedChat(newEncryptedChat,
//                result =>
//                {
//                    _eventAggregator.Publish(newEncryptedChat);

//                    var currentUserId = MTProtoService.Instance.CurrentUserId;
//                    var clientTicksDelta = MTProtoService.Instance.ClientTicksDelta;

//                    var randomId = TLLong.Random();

//                    var notifyLayerAction = new TLDecryptedMessageActionNotifyLayer();
//                    notifyLayerAction.Layer = new int?(Constants.SecretSupportedLayer);

//                    var inSeqNo = TLUtils.GetInSeqNo(currentUserId, newEncryptedChat);
//                    var outSeqNo = TLUtils.GetOutSeqNo(currentUserId, newEncryptedChat);

//                    var newEncryptedChat17 = _cacheService.GetEncryptedChat(newEncryptedChat.Id) as TLEncryptedChat17;
//                    if (newEncryptedChat17 != null)
//                    {
//                        newEncryptedChat17.RawOutSeqNo = new int?(newEncryptedChat17.RawOutSeqNo.Value + 1);
//                    }

//                    var decryptedMessageService17 = new TLDecryptedMessageService17
//                    {
//                        Action = notifyLayerAction,
//                        RandomId = randomId,
//                        RandomBytes = string.Empty,

//                        ChatId = encryptedChat.Id,
//                        FromId = currentUserId,
//                        Out = bool.True,
//                        Unread = bool.False,
//                        Date = TLUtils.DateToUniversalTimeTLInt(clientTicksDelta, DateTime.Now),
//                        Status = MessageStatus.Sending,

//                        TTL = new int?(0),
//                        InSeqNo = inSeqNo,
//                        OutSeqNo = outSeqNo
//                    };

//                    var decryptedMessageLayer17 = new TLDecryptedMessageLayer17();
//                    decryptedMessageLayer17.Layer = new int?(Constants.SecretSupportedLayer);
//                    decryptedMessageLayer17.InSeqNo = inSeqNo;
//                    decryptedMessageLayer17.OutSeqNo = outSeqNo;
//                    decryptedMessageLayer17.RandomBytes = string.Empty;
//                    decryptedMessageLayer17.Message = decryptedMessageService17;

//                    _cacheService.SyncDecryptedMessage(
//                        decryptedMessageService17,
//                        encryptedChat,
//                        messageResult =>
//                        {
//                            SendEncryptedServiceAsync(
//                                new TLInputEncryptedChat
//                                {
//                                    AccessHash = encryptedChat.AccessHash,
//                                    ChatId = encryptedChat.Id
//                                },
//                                randomId,
//                                TLUtils.EncryptMessage(decryptedMessageLayer17, encryptedChat),
//                                sentEncryptedMessage =>
//                                {
//                                    decryptedMessageService17.Status = MessageStatus.Confirmed;
//                                    _cacheService.SyncSendingDecryptedMessage(encryptedChat.Id, sentEncryptedMessage.Date, decryptedMessageService17.RandomId,
//                                        m =>
//                                        {
//#if DEBUG
//                                            _eventAggregator.Publish(decryptedMessageService17);
//#endif
//                                        });
//                                },
//                                error =>
//                                {
//                                    Execute.ShowDebugMessage("messages.sendEncryptedService error " + error);
//                                });
//                        });
//                });
//        }

//        private static int GetRawInFromReceivedMessage(int? currentUserId, TLEncryptedChat17 chat, ISeqNo message)
//        {
//            var isAdmin = chat.AdminId.Value == currentUserId.Value;
//            var x = isAdmin ? 0 : 1;
//            return (message.OutSeqNo.Value - x) / 2;
//        }

//        public static TLDecryptedMessage ToDecryptedMessage17(TLDecryptedMessage decryptedMsg, TLEncryptedChat encryptedChat)
//        {
//            var decryptedMessage17 = new TLDecryptedMessage17();
//            decryptedMessage17.RandomId = decryptedMsg.RandomId;
//            decryptedMessage17.TTL = encryptedChat.MessageTTL;
//            decryptedMessage17.Message = decryptedMsg.Message;
//            decryptedMessage17.Media = decryptedMsg.Media;

//            decryptedMessage17.FromId = decryptedMsg.FromId;
//            decryptedMessage17.Out = decryptedMsg.Out;
//            decryptedMessage17.Unread = decryptedMsg.Unread;
//            decryptedMessage17.ChatId = decryptedMsg.ChatId;
//            decryptedMessage17.Date = decryptedMsg.Date;
//            decryptedMessage17.Qts = decryptedMsg.Qts;

//            return decryptedMessage17;
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
                            Execute.ShowDebugMessage("account.updateStatus error " + error);
                        });
#if LOG_CLIENTSEQ
                    Execute.ShowDebugMessage(string.Format("{0} updatesTooLong clientSeq={1} pts={2}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), ClientSeq, _pts));
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
                    Log.Write("UpdatesService.ProcessUpdates StartGetDifference");
                    GetDifference(1000, () =>
                    {
                        var elapsed = stopwatch.Elapsed;
                        Log.Write("UpdatesService.ProcessUpdates StopGetDifference time=" + elapsed);
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
                        Log.Write(logString);
                        getDifferenceRequired = true;
                        break;
                    }
                    var chat = _cacheService.GetChat(updatesShortChatMessage.ChatId);
                    if (chat == null)
                    {
                        var logString =
                            string.Format("ProcessUpdates.UpdatesShortChatMessage: chat is missing (chatId={0}, msgId={1})",
                                updatesShortChatMessage.ChatId, updatesShortChatMessage.Id);
                        Log.Write(logString);
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
                        Log.Write(logString);
                        getDifferenceRequired = true;
                        break;
                    }
                }
            }
            return getDifferenceRequired;
        }

        private void ProcessReading(IList<TLUpdatesBase> updatesList)
        {
            var readHistoryInboxList = new List<TLUpdateReadHistoryInbox>();

            var newChatMessageList = new List<TLUpdateNewMessage>();
            var newMessageList = new List<TLUpdateNewMessage>();
            var shortChatMessageList = new List<TLUpdateShortChatMessage>();
            var shortMessageList = new List<TLUpdateShortMessage>();
            foreach (var updatesBase in updatesList)
            {
                var updates = updatesBase as TLUpdates;
                if (updates != null)
                {
                    foreach (var updateBase in updates.Updates)
                    {
                        var readHistoryInbox = updateBase as TLUpdateReadHistoryInbox;
                        if (readHistoryInbox != null)
                        {
                            readHistoryInboxList.Add(readHistoryInbox);
                            continue;
                        }

                        var newMessage = updateBase as TLUpdateNewMessage;
                        if (newMessage != null)
                        {
                            var message = newMessage.Message as TLMessage;
                            if (message != null && !message.IsOut)
                            {
                                var peerChat = message.ToId as TLPeerChat;
                                if (peerChat != null)
                                {
                                    newChatMessageList.Add(newMessage);
                                    continue;
                                }

                                var peerUser = message.ToId as TLPeerUser;
                                if (peerUser != null)
                                {
                                    newMessageList.Add(newMessage);
                                    continue;
                                }
                            }
                        }
                    }

                    continue;
                }

                var shortChatMessage = updatesBase as TLUpdateShortChatMessage;
                if (shortChatMessage != null && !shortChatMessage.IsOut)
                {
                    shortChatMessageList.Add(shortChatMessage);
                    continue;
                }

                var shortMessage = updatesBase as TLUpdateShortMessage;
                if (shortMessage != null && !shortMessage.IsOut)
                {
                    shortMessageList.Add(shortMessage);
                    continue;
                }
            }

            if (readHistoryInboxList.Count > 0)
            {
                foreach (var readHistoryInbox in readHistoryInboxList)
                {
                    var peerChat = readHistoryInbox.Peer as TLPeerChat;
                    if (peerChat != null)
                    {
                        for (var i = 0; i < shortChatMessageList.Count; i++)
                        {
                            if (peerChat.Id == shortChatMessageList[i].ChatId &&
                                readHistoryInbox.MaxId >= shortChatMessageList[i].Id)
                            {
                                shortChatMessageList[i].IsUnread = false;
                                shortChatMessageList.RemoveAt(i--);
                            }
                        }

                        for (var i = 0; i < newChatMessageList.Count; i++)
                        {
                            var message = newChatMessageList[i].Message as TLMessage;
                            if (message != null)
                            {
                                if (peerChat.Id == message.ToId.Id &&
                                    readHistoryInbox.MaxId >= message.Id)
                                {
                                    message.IsUnread = false;
                                    newChatMessageList.RemoveAt(i--);
                                }
                            }
                        }
                        continue;
                    }

                    var peerUser = readHistoryInbox.Peer as TLPeerUser;
                    if (peerUser != null)
                    {
                        for (var i = 0; i < shortMessageList.Count; i++)
                        {
                            if (peerUser.Id == shortMessageList[i].UserId &&
                                readHistoryInbox.MaxId >= shortMessageList[i].Id)
                            {
                                shortMessageList[i].IsUnread = false;
                                shortMessageList.RemoveAt(i--);
                            }
                        }

                        for (var i = 0; i < newMessageList.Count; i++)
                        {
                            var message = newMessageList[i].Message as TLMessage;
                            if (message != null)
                            {
                                if (peerUser.Id == message.FromId &&
                                    readHistoryInbox.MaxId >= message.Id)
                                {
                                    message.IsUnread = false;
                                    newMessageList.RemoveAt(i--);
                                }
                            }
                        }
                        continue;
                    }
                }
            }
        }

        public void ProcessUpdates(TLUpdatesBase updates)
        {
            var updatesList = new List<TLUpdatesBase> { updates };

            var updatesTooLongList = new List<TLUpdatesTooLong>();
            var updatesTooLong = updates as TLUpdatesTooLong;
            if (updatesTooLong != null)
            {
                updatesTooLongList.Add(updatesTooLong);
            }

            ProcessUpdates(updatesList, updatesTooLongList, false);
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
                state.Pts = new int?(140000);
#endif

                SetState(state, "setFileState");
                TLUtils.WritePerformance("Current state: " + state);
            }

            Log.Write(string.Format("UpdatesService.LoadStateAndUpdate {0} client_state=[p={1} d={2} q={3}]", id, _pts, _date, _qts));

            LoadFileState();

            var stopwatch = Stopwatch.StartNew();
            Log.Write(string.Format("UpdatesService.LoadStateAndUpdate {0} start GetDifference", id));
            AddRequest(id);
            GetDifference(id, () =>
            {
                var elapsed = stopwatch.Elapsed;
                Log.Write(string.Format("UpdatesService.LoadStateAndUpdate {0} stop GetDifference elapsed={1}", id, elapsed));
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


            Log.Write("UpdatesService.LoadStateAndUpdate start LoadFileState");

            if (difference != null && difference.Count > 0)
            {
                var ptsList = string.Join(", ", difference.OfType<TLUpdatesDifference>().Select(x => x.State.Pts));
                Log.Write(string.Format("UpdatesService.LoadStateAndUpdate ptsList=[{0}]", ptsList));

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

                        Log.SyncWrite(string.Format("UpdatesService.LoadFileState processDiff state=[{0}] messages_count={1} elapsed={2}", diff.State, diff.NewMessages.Count, stopwatchProcessDiff.Elapsed));
                    }
                }

                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new UpdateCompletedEventArgs()));
            }

            Log.Write("UpdatesService.LoadStateAndUpdate stop LoadFileState elapsed=" + stopwatch.Elapsed);

            FileUtils.Delete(_differenceSyncRoot, Constants.DifferenceFileName);
            FileUtils.Delete(_differenceTimeSyncRoot, Constants.DifferenceTimeFileName);
        }


        // TODO: NULLABLE STATE

        public void SaveState()
        {
            TLUtils.WritePerformance("<<Saving current state");
            TLUtils.SaveObjectToMTProtoFile(_stateRoot, Constants.StateFileName, new TLUpdatesState { Date = _date ?? 0, Pts = _pts ?? 0, Qts = _qts ?? 0, Seq = ClientSeq ?? 0, UnreadCount = _unreadCount ?? 0 });
        }

        public TLUpdatesState GetState()
        {
            return new TLUpdatesState { Date = _date ?? 0, Pts = _pts ?? 0, Qts = _qts ?? 0, Seq = ClientSeq ?? 0, UnreadCount = _unreadCount ?? 0 };
        }

        public void SaveStateSnapshot(string toFileName)
        {
            TLUtils.SaveObjectToMTProtoFile(_stateRoot, toFileName, new TLUpdatesState { Date = _date ?? 0, Pts = _pts ?? 0, Qts = _qts ?? 0, Seq = ClientSeq ?? 0, UnreadCount = _unreadCount ?? 0 });
        }

        public void LoadStateSnapshot(string fromFileName)
        {
            var state = TLUtils.OpenObjectFromMTProtoFile<TLUpdatesState>(_stateRoot, fromFileName);
            if (state != null)
            {
                lock (_clientSeqLock)
                {
                    ClientSeq = state.Seq;
                }
                _date = state.Date;
                _pts = state.Pts;
                _qts = state.Qts;
                _unreadCount = state.UnreadCount;
                SaveState();
            }
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

    public class UpdateCompletedEventArgs : EventArgs { }
}
