//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Telegram.Common;
using Telegram.Navigation;
using Windows.UI.Xaml;

namespace Telegram.Services
{
    /// <summary>
    ///   A marker interface for classes that subscribe to messages.
    /// </summary>
    public interface IHandle { }

    /// <summary>
    ///   Enables loosely-coupled publication of and subscription to events.
    /// </summary>
    public interface IEventAggregator
    {
        SubscriptionBuilder Subscribe<T>(object subscriber, Action<T> action);
        void Subscribe<T>(object subscriber, long token, UpdateHandler<T> action, bool fireAndForget);

        void Unsubscribe(object subscriber);
        void Unsubscribe<T>(object subscriber);
        void Unsubscribe(object subscriber, long token, bool fireAndForget);

        void Publish(object message);
        void Publish(object message, long token, bool forget);
    }

    public class EventAggregator : IEventAggregator
    {
        private static IEventAggregator _current;
        public static IEventAggregator Current => _current ??= new EventAggregator();

        #region By type

        private readonly ConcurrentDictionary<Type, TypeHandler> _typeHandlers = new();

        public SubscriptionBuilder Subscribe<T>(object subscriber, Action<T> action)
        {
            Add(subscriber, typeof(T), action);
            return new SubscriptionBuilder(this, subscriber);
        }

        public void Add(object subscriber, Type messageType, Delegate action)
        {
            var handler = _typeHandlers.GetOrAdd(messageType, x => new TypeHandler());
            handler.Subscribe(subscriber, action);
        }

        public virtual void Unsubscribe(object subscriber)
        {
            foreach (var item in _typeHandlers)
            {
                if (item.Value.Unsubscribe(subscriber))
                {
                    // TODO: is this safe for real? Can't be done with normal Dictionary
                    _typeHandlers.TryRemove(item.Key, out _);
                }
            }

            if (_longSubscribers.TryGetValue(subscriber, out var prev))
            {
                Unsubscribe(subscriber, prev.Token, true);
            }
        }

        public virtual void Unsubscribe<T>(object subscriber)
        {
            if (_typeHandlers.TryGetValue(typeof(T), out var handler))
            {
                if (handler.Unsubscribe(subscriber))
                {
                    _typeHandlers.TryRemove(typeof(T), out _);
                }
            }
        }

        public virtual void Publish(object message)
        {
            var messageType = message.GetType();

            if (_typeHandlers.TryGetValue(messageType, out TypeHandler handler))
            {
                if (handler.Handle(message, false))
                {
                    _typeHandlers.TryRemove(messageType, out _);
                }
            }
        }

        public class TypeHandler
        {
            protected readonly ConditionalWeakTable<object, Delegate> _delegates = new();

            // Count is expected to go out of sync if delegates get garbage
            // collected, so we resynchronize the amount on every handle.
            protected int _count;

            public virtual bool Handle(object message, bool forget)
            {
                var count = 0;

                foreach (var value in _delegates)
                {
                    value.Value.DynamicInvoke(message);
                    count++;
                }

                _count = count;
                return count == 0;
            }

            public void Subscribe(object subscriber, Delegate handler)
            {
                _count++;
                _delegates.AddOrUpdate(subscriber, handler);
            }

            public bool Unsubscribe(object subscriber)
            {
                if (_delegates.Remove(subscriber))
                {
                    _count--;
                }

                return _count <= 0;
            }
        }

        #endregion

        #region By token

        private readonly ConcurrentDictionary<long, LongHandler> _longHandlers = new();
        private readonly ConditionalWeakTable<object, Subscriber> _longSubscribers = new();

        class Subscriber
        {
            public long Token;

            public Subscriber(long token)
            {
                Token = token;
            }
        }

        public void Subscribe<T>(object subscriber, long token, UpdateHandler<T> action, bool fireAndForget)
        {
            if (fireAndForget && _longSubscribers.TryGetValue(subscriber, out Subscriber prev))
            {
                Unsubscribe(subscriber, prev.Token, true);
            }

            var handler = _longHandlers.GetOrAdd(token, x => new LongHandler());
            handler.Subscribe(subscriber, action);

            if (fireAndForget)
            {
                _longSubscribers.AddOrUpdate(subscriber, new Subscriber(token));
            }
        }

        public virtual void Unsubscribe(object subscriber, long token, bool fireAndForget)
        {
            if (fireAndForget)
            {
                _longSubscribers.Remove(subscriber);
            }

            if (_longHandlers.TryGetValue(token, out var handler))
            {
                if (handler.Unsubscribe(subscriber))
                {
                    _longHandlers.TryRemove(token, out _);
                }
            }
        }

        public virtual void Publish(object message, long token, bool forget)
        {
            if (forget)
            {
                if (_longHandlers.TryRemove(token, out LongHandler handler))
                {
                    handler.Handle(message, true);
                }
            }
            else if (_longHandlers.TryGetValue(token, out LongHandler handler))
            {
                if (handler.Handle(message, false))
                {
                    _longHandlers.TryRemove(token, out _);
                }
            }
        }

        public class LongHandler : TypeHandler
        {
            public override bool Handle(object message, bool forget)
            {
                var count = 0;

                foreach (var value in _delegates)
                {
                    var subscriber = value.Key;
                    var delegato = value.Value;

                    void DynamicInvoke(Delegate delegato, object subscriber, object message)
                    {
                        try
                        {
                            delegato.DynamicInvoke(subscriber, message);
                        }
                        catch
                        {
                            // Most likely Excep_InvalidComObject_NoRCW_Wrapper, so we can just ignore it
                            // TODO: would be great to remove the subscriber from the delegates here.
                            Unsubscribe(subscriber);
                        }
                    }

                    if (value.Key is FrameworkElement element)
                    {
                        element.BeginOnUIThread(() => DynamicInvoke(delegato, subscriber, message));
                    }
                    else if (value.Key is ViewModelBase navigable && navigable.Dispatcher != null)
                    {
                        navigable.BeginOnUIThread(() => DynamicInvoke(delegato, subscriber, message));
                    }
                    else
                    {
                        DynamicInvoke(delegato, subscriber, message);
                    }

                    count++;
                }

                if (forget && count > 0)
                {
                    _delegates.Clear();
                }

                _count = count;
                return count == 0;
            }
        }

        #endregion
    }

    public class SubscriptionBuilder
    {
        private readonly EventAggregator _aggregator;
        private readonly object _subscriber;

        public SubscriptionBuilder(EventAggregator aggregator, object subscriber)
        {
            _aggregator = aggregator;
            _subscriber = subscriber;
        }

        public SubscriptionBuilder Subscribe<T>(Action<T> action)
        {
            _aggregator.Add(_subscriber, typeof(T), action);
            return this;
        }
    }

    public delegate void UpdateHandler<T>(object target, T update);
}
