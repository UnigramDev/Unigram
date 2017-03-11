using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Extensions;
using Telegram.Api.TL;

namespace Unigram.Common.Dialogs
{
    public class InputTypingManager
    {
        private readonly Action _callback;
        private readonly Action<IList<Tuple<int, TLSendMessageActionBase>>> _typingCallback;

        private readonly Dictionary<int, Tuple<DateTime, TLSendMessageActionBase>> _typingUsersCache = new Dictionary<int, Tuple<DateTime, TLSendMessageActionBase>>();

        private readonly object _typingUsersSyncRoot = new object();

        private readonly Timer _typingUsersTimer;

        public InputTypingManager(Action<IList<Tuple<int, TLSendMessageActionBase>>> typingCallback, Action callback)
        {
            _typingUsersTimer = new Timer(new TimerCallback(UpdateTypingUsersCache), null, -1, -1);
            _typingCallback = typingCallback;
            _callback = callback;
        }

        public void AddTypingUser(int userId, TLSendMessageActionBase action)
        {
            var now = DateTime.Now;
            var max = DateTime.MaxValue;
            var typing = new List<Tuple<int, TLSendMessageActionBase>>();

            lock (_typingUsersSyncRoot)
            {
                _typingUsersCache[userId] = new Tuple<DateTime, TLSendMessageActionBase>(TillDate(now, action), action);

                foreach (var current in _typingUsersCache)
                {
                    if (current.Value.Item1 > now)
                    {
                        if (max > current.Value.Item1)
                        {
                            max = current.Value.Item1;
                        }

                        typing.Add(new Tuple<int, TLSendMessageActionBase>(current.Key, current.Value.Item2));
                    }
                }
            }

            if (typing.Count > 0)
            {
                StartTypingTimer((int)(max - now).TotalMilliseconds);
                _typingCallback?.Invoke(typing);
                return;
            }

            _callback?.Invoke();
        }

        public void RemoveTypingUser(int userId)
        {
            var typing = new List<Tuple<int, TLSendMessageActionBase>>();

            lock (_typingUsersSyncRoot)
            {
                _typingUsersCache.Remove(userId);

                foreach (var current in _typingUsersCache)
                {
                    if (current.Value.Item1 > DateTime.Now)
                    {
                        typing.Add(new Tuple<int, TLSendMessageActionBase>(current.Key, current.Value.Item2));
                    }
                }
            }

            if (typing.Count > 0)
            {
                _typingCallback?.Invoke(typing);
                return;
            }

            _callback?.Invoke();
        }

        public void Start()
        {
            StartTypingTimer(0);
        }

        private void StartTypingTimer(int dueTime)
        {
            if (_typingUsersTimer != null)
            {
                _typingUsersTimer.Change(dueTime, -1);
            }
        }

        public void Stop()
        {
            StopTypingTimer();
        }

        private void StopTypingTimer()
        {
            if (_typingUsersTimer != null)
            {
                _typingUsersTimer.Change(-1, -1);
            }
        }

        private static DateTime TillDate(DateTime now, TLSendMessageActionBase action)
        {
            var playGameAction = action as TLSendMessageGamePlayAction;
            if (playGameAction != null)
            {
                return now.AddSeconds(10.0);
            }

            return now.AddSeconds(5.0);
        }

        private void UpdateTypingUsersCache(object state)
        {
            var now = DateTime.Now;
            var max = DateTime.MaxValue;
            var typing = new List<Tuple<int, TLSendMessageActionBase>>();

            lock (_typingUsersSyncRoot)
            {
                if (_typingUsersCache.Count == 0)
                {
                    return;
                }

                var keys = _typingUsersCache.Keys.ToList();

                foreach (var current in keys)
                {
                    if (_typingUsersCache[current].Item1 <= now)
                    {
                        _typingUsersCache.Remove(current);
                    }
                    else
                    {
                        if (max > _typingUsersCache[current].Item1)
                        {
                            max = _typingUsersCache[current].Item1;
                        }

                        typing.Add(new Tuple<int, TLSendMessageActionBase>(current, _typingUsersCache[current].Item2));
                    }
                }
            }

            if (typing.Count > 0)
            {
                StartTypingTimer((int)(max - now).TotalMilliseconds);
                _typingCallback?.Invoke(typing);
                return;
            }

            StopTypingTimer();
            _callback?.Invoke();
        }
    }
}
