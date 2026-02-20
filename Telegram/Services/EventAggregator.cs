//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        SubscriptionBuilder Subscribe<T>(object subscriber, Action<T> action, EventType type = EventType.None, long id = 0);
        void Subscribe<T>(object subscriber, long token, UpdateHandler<T> action);

        void Unsubscribe(object subscriber);
        void Unsubscribe<T>(object subscriber, EventType type = EventType.None, long id = 0);
        void Unsubscribe(object subscriber, long token);

        void Publish(object message, EventType type = EventType.None, long id = 0);
        void Publish(object message, long token);
    }

    // TODO: Use in more places if possible
    public enum EventType
    {
        None,
        Chat,
        GroupCall
    }

    public partial class EventAggregator : IEventAggregator
    {
        private static IEventAggregator _current;
        public static IEventAggregator Current => _current ??= new EventAggregator();

        #region By type

        private readonly struct SubscriptionKey
        {
            public SubscriptionKey(Type messageType, EventType type, long id)
            {
                MessageType = messageType;
                Type = type;
                Id = id;
            }

            public readonly Type MessageType;

            public readonly EventType Type;

            public readonly long Id;

            public override bool Equals(object obj)
            {
                if (obj is SubscriptionKey other)
                {
                    return other.MessageType == MessageType
                        && other.Type == Type
                        && other.Id == Id;
                }

                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(MessageType, Type, Id);
            }
        }

        private readonly ConcurrentDictionary<SubscriptionKey, TypeHandler> _typeHandlers = new();

        public SubscriptionBuilder Subscribe<T>(object subscriber, Action<T> action, EventType type = EventType.None, long id = 0)
        {
            Add(subscriber, typeof(T), type, id, action);
            return new SubscriptionBuilder(this, subscriber, type, id);
        }

        public void Add(object subscriber, Type messageType, EventType type, long id, Delegate action)
        {
            var key = new SubscriptionKey(messageType, type, id);
            var handler = _typeHandlers.GetOrAdd(key, x => new TypeHandler());
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
        }

        public virtual void Unsubscribe<T>(object subscriber, EventType type = EventType.None, long id = 0)
        {
            var key = new SubscriptionKey(typeof(T), type, id);

            if (_typeHandlers.TryGetValue(key, out var handler))
            {
                if (handler.Unsubscribe(subscriber))
                {
                    _typeHandlers.TryRemove(key, out _);
                }
            }
        }

        public virtual void Publish(object message, EventType type = EventType.None, long id = 0)
        {
            var messageType = message.GetType();
            var key = new SubscriptionKey(messageType, type, id);

            if (_typeHandlers.TryGetValue(key, out TypeHandler handler))
            {
                if (handler.Handle(message))
                {
                    _typeHandlers.TryRemove(key, out _);
                }
            }
        }

        public partial class TypeHandler
        {
            protected readonly ConditionalWeakTable<object, Delegate> _delegates = new();

            // Count is expected to go out of sync if delegates get garbage
            // collected, so we resynchronize the amount on every handle.
            protected int _count;

            public virtual bool Handle(object message)
            {
                var count = 0;

                foreach (var value in _delegates)
                {
                    DynamicInvoke(value.Value, message);
                    count++;
                }

                _count = count;
                return count == 0;
            }

            protected bool DynamicInvoke(Delegate delegato, object message, object subscriber = null)
            {
                try
                {
                    if (subscriber != null)
                    {
                        delegato.DynamicInvoke(subscriber, message);
                    }
                    else
                    {
                        delegato.DynamicInvoke(message);
                    }
                    return true;
                }
                catch (InvalidComObjectException)
                {
                    // Most likely Excep_InvalidComObject_NoRCW_Wrapper, so we can just ignore it
                    // TODO: would be great to remove the subscriber from the delegates here.
                    Unsubscribe(subscriber);
                    return false;
                }
                catch
                {
                    return true;
                }
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

        public void Subscribe<T>(object subscriber, long token, UpdateHandler<T> action)
        {
            var handler = _longHandlers.GetOrAdd(token, x => new LongHandler());
            handler.Subscribe(subscriber, action);
        }

        public virtual void Unsubscribe(object subscriber, long token)
        {
            if (_longHandlers.TryGetValue(token, out var handler))
            {
                if (handler.Unsubscribe(subscriber))
                {
                    _longHandlers.TryRemove(token, out _);
                }
            }
        }

        public virtual void Publish(object message, long token)
        {
            if (_longHandlers.TryGetValue(token, out LongHandler handler))
            {
                if (handler.Handle(message))
                {
                    _longHandlers.TryRemove(token, out _);
                }
            }
        }

        public partial class LongHandler : TypeHandler
        {
            public override bool Handle(object message)
            {
                var count = 0;

                foreach (var value in _delegates)
                {
                    if (value.Key is FrameworkElement element)
                    {
                        element.BeginOnUIThread(() => DynamicInvoke(value.Value, message, value.Key));
                    }
                    else if (value.Key is ViewModelBase navigable && navigable.Dispatcher != null)
                    {
                        navigable.BeginOnUIThread(() => DynamicInvoke(value.Value, message, value.Key));
                    }
                    else
                    {
                        DynamicInvoke(value.Value, message, value.Key);
                    }

                    count++;
                }

                _count = count;
                return count == 0;
            }
        }

        #endregion
    }

    public partial class SubscriptionBuilder
    {
        private readonly EventAggregator _aggregator;
        private readonly object _subscriber;

        private readonly EventType _type;
        private readonly long _id;

        public SubscriptionBuilder(EventAggregator aggregator, object subscriber, EventType type, long id)
        {
            _aggregator = aggregator;
            _subscriber = subscriber;

            _type = type;
            _id = id;
        }

        public SubscriptionBuilder Subscribe<T>(Action<T> action)
        {
            _aggregator.Add(_subscriber, typeof(T), _type, _id, action);
            return this;
        }
    }

    public delegate void UpdateHandler<T>(object target, T update);
}
