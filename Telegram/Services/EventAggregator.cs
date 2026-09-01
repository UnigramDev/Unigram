//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Telegram.Common;

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
        SubscriptionBuilder Subscribe<T>(object subscriber, Action<T> action, EventType type = EventType.None, long id = 0) where T : class;

        void Unsubscribe(object subscriber);
        void Unsubscribe<T>(object subscriber, EventType type = EventType.None, long id = 0);

        void Publish(object message, EventType type = EventType.None, long id = 0);
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

        public SubscriptionBuilder Subscribe<T>(object subscriber, Action<T> action, EventType type = EventType.None, long id = 0) where T : class
        {
            Add(subscriber, type, id, action);
            return new SubscriptionBuilder(this, subscriber, type, id);
        }

        public void Add<T>(object subscriber, EventType type, long id, Action<T> action) where T : class
        {
            var key = new SubscriptionKey(typeof(T), type, id);
            var handler = _typeHandlers.GetOrAdd(key, static _ => new TypeHandler<T>());

            // The key carries typeof(T), so the only handler that can be under it is this one.
            ((TypeHandler<T>)handler).Subscribe(subscriber, action);
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

        /// <summary>
        /// The subscribers of one message type, minus the type - so that one dictionary can hold
        /// every message type while the invoke below stays typed.
        ///
        /// It used to be one non-generic class holding <see cref="Delegate"/>, invoked through
        /// Delegate.DynamicInvoke: reflection and an object[] per subscriber per update, on the
        /// TDLib thread, for every update the app publishes. Nothing about the subscription needed
        /// it - Subscribe already has the Action&lt;T&gt;.
        /// </summary>
        public abstract partial class TypeHandler
        {
            // Count is expected to go out of sync if delegates get garbage
            // collected, so we resynchronize the amount on every handle.
            protected int _count;

            public abstract bool Handle(object message);

            public abstract bool Unsubscribe(object subscriber);
        }

        // T is a reference type throughout - every message published here is a class, most of them
        // TDLib's projected runtime classes - which is the case generic sharing is meant for, so
        // the ~150 message types the app subscribes to do not each get their own copy of this.
        public sealed partial class TypeHandler<T> : TypeHandler where T : class
        {
            private readonly ConditionalWeakTable<object, Action<T>> _delegates = new();

            /// <param name="message">
            /// Always a T: Publish looks the handler up by the runtime type of the message, and the
            /// key it finds it under is typeof(T).
            /// </param>
            public override bool Handle(object message)
            {
                var count = 0;

                foreach (var value in _delegates)
                {
                    Invoke(value.Value, (T)message, value.Key);
                    count++;
                }

                _count = count;
                return count == 0;
            }

            private bool Invoke(Action<T> action, T message, object subscriber)
            {
                try
                {
                    action(message);
                    return true;
                }
                catch (Exception ex) when (ex.IsInvalidComObject())
                {
                    // The subscriber is gone, so drop it rather than throwing on every publish.
                    Unsubscribe(subscriber);
                    return false;
                }
                catch
                {
                    return true;
                }
            }

            public void Subscribe(object subscriber, Action<T> handler)
            {
                _count++;
                _delegates.AddOrUpdate(subscriber, handler);
            }

            public override bool Unsubscribe(object subscriber)
            {
                if (_delegates.Remove(subscriber))
                {
                    _count--;
                }

                return _count <= 0;
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

        public SubscriptionBuilder Subscribe<T>(Action<T> action) where T : class
        {
            _aggregator.Add(_subscriber, _type, _id, action);
            return this;
        }
    }

    public delegate void UpdateHandler<T>(T update);
}
