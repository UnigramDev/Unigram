//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Telegram.Navigation.Services;
using Windows.System;
using static Telegram.Services.EventAggregator;

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
        /// <summary>
        ///   Subscribes an instance to all events declared through implementations of <see cref = "IHandle" />
        /// </summary>
        /// <param name = "subscriber">The instance to subscribe for event publication.</param>
        SubscriptionBuilder Subscribe<T>(object subscriber, Action<T> action);

        /// <summary>
        ///   Unsubscribes the instance from all events.
        /// </summary>
        /// <param name = "subscriber">The instance to unsubscribe.</param>
        void Unsubscribe(object subscriber);

        /// <summary>
        ///   Publishes a message.
        /// </summary>
        /// <param name = "message">The message instance.</param>
        /// <remarks>
        ///   Uses the default thread marshaller during publication.
        /// </remarks>
        void Publish(object message);

        #region IMessenger

        /// <summary>
        /// Registers a recipient for a type of message TMessage. The action
        /// parameter will be executed when a corresponding message is sent.
        /// <para>Registering a recipient does not create a hard reference to it,
        /// so if this recipient is deleted, no memory leak is caused.</para>
        /// </summary>
        /// <typeparam name="TMessage">The type of message that the recipient registers
        /// for.</typeparam>
        /// <param name="recipient">The recipient that will receive the messages.</param>
        /// <param name="action">The action that will be executed when a message
        /// of type TMessage is sent. IMPORTANT: Note that closures are not supported at the moment
        /// due to the use of WeakActions (see http://stackoverflow.com/questions/25730530/). </param>
        /// <param name="keepTargetAlive">If true, the target of the Action will
        /// be kept as a hard reference, which might cause a memory leak. You should only set this
        /// parameter to true if the action is using closures. See
        /// http://galasoft.ch/s/mvvmweakaction. </param>
        void Register<TMessage>(
            object recipient,
            UpdateHandler<TMessage> action,
            bool keepTargetAlive = false,
            bool completionOnly = false);

        /// <summary>
        /// Registers a recipient for a type of message TMessage.
        /// The action parameter will be executed when a corresponding 
        /// message is sent. See the receiveDerivedMessagesToo parameter
        /// for details on how messages deriving from TMessage (or, if TMessage is an interface,
        /// messages implementing TMessage) can be received too.
        /// <para>Registering a recipient does not create a hard reference to it,
        /// so if this recipient is deleted, no memory leak is caused.</para>
        /// </summary>
        /// <typeparam name="TMessage">The type of message that the recipient registers
        /// for.</typeparam>
        /// <param name="recipient">The recipient that will receive the messages.</param>
        /// <param name="token">A token for a messaging channel. If a recipient registers
        /// using a token, and a sender sends a message using the same token, then this
        /// message will be delivered to the recipient. Other recipients who did not
        /// use a token when registering (or who used a different token) will not
        /// get the message. Similarly, messages sent without any token, or with a different
        /// token, will not be delivered to that recipient.</param>
        /// <param name="action">The action that will be executed when a message
        /// of type TMessage is sent.</param>
        /// <param name="keepTargetAlive">If true, the target of the Action will
        /// be kept as a hard reference, which might cause a memory leak. You should only set this
        /// parameter to true if the action is using closures. See
        /// http://galasoft.ch/s/mvvmweakaction. </param>
        void Register<TMessage>(
            object recipient,
            object token,
            UpdateHandler<TMessage> action,
            bool keepTargetAlive = false,
            bool completionOnly = false);

        /// <summary>
        /// Sends a message to registered recipients. The message will
        /// reach all recipients that registered for this message type
        /// using one of the Register methods.
        /// </summary>
        /// <typeparam name="TMessage">The type of message that will be sent.</typeparam>
        /// <param name="message">The message to send to registered recipients.</param>
        void Send<TMessage>(TMessage message, bool completion = false);

        /// <summary>
        /// Sends a message to registered recipients. The message will
        /// reach only recipients that registered for this message type
        /// using one of the Register methods, and that are
        /// of the targetType.
        /// </summary>
        /// <typeparam name="TMessage">The type of message that will be sent.</typeparam>
        /// <typeparam name="TTarget">The type of recipients that will receive
        /// the message. The message won't be sent to recipients of another type.</typeparam>
        /// <param name="message">The message to send to registered recipients.</param>
        void Send<TMessage, TTarget>(TMessage message, bool completion = false);

        /// <summary>
        /// Sends a message to registered recipients. The message will
        /// reach only recipients that registered for this message type
        /// using one of the Register methods, and that are
        /// of the targetType.
        /// </summary>
        /// <typeparam name="TMessage">The type of message that will be sent.</typeparam>
        /// <param name="message">The message to send to registered recipients.</param>
        /// <param name="token">A token for a messaging channel. If a recipient registers
        /// using a token, and a sender sends a message using the same token, then this
        /// message will be delivered to the recipient. Other recipients who did not
        /// use a token when registering (or who used a different token) will not
        /// get the message. Similarly, messages sent without any token, or with a different
        /// token, will not be delivered to that recipient.</param>
        void Send<TMessage>(TMessage message, object token, bool completion = false);

        /// <summary>
        /// Unregisters a messager recipient completely. After this method
        /// is executed, the recipient will not receive any messages anymore.
        /// </summary>
        /// <param name="recipient">The recipient that must be unregistered.</param>
        void Unregister(object recipient);

        /// <summary>
        /// Unregisters a message recipient for a given type of messages only. 
        /// After this method is executed, the recipient will not receive messages
        /// of type TMessage anymore, but will still receive other message types (if it
        /// registered for them previously).
        /// </summary>
        /// <typeparam name="TMessage">The type of messages that the recipient wants
        /// to unregister from.</typeparam>
        /// <param name="recipient">The recipient that must be unregistered.</param>
        void Unregister<TMessage>(object recipient);

        /// <summary>
        /// Unregisters a message recipient for a given type of messages only and for a given token. 
        /// After this method is executed, the recipient will not receive messages
        /// of type TMessage anymore with the given token, but will still receive other message types
        /// or messages with other tokens (if it registered for them previously).
        /// </summary>
        /// <param name="recipient">The recipient that must be unregistered.</param>
        /// <param name="token">The token for which the recipient must be unregistered.</param>
        /// <typeparam name="TMessage">The type of messages that the recipient wants
        /// to unregister from.</typeparam>
        void Unregister<TMessage>(object recipient, object token);

        /// <summary>
        /// Unregisters a message recipient for a given type of messages and for
        /// a given action. Other message types will still be transmitted to the
        /// recipient (if it registered for them previously). Other actions that have
        /// been registered for the message type TMessage and for the given recipient (if
        /// available) will also remain available.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages that the recipient wants
        /// to unregister from.</typeparam>
        /// <param name="recipient">The recipient that must be unregistered.</param>
        /// <param name="action">The action that must be unregistered for
        /// the recipient and for the message type TMessage.</param>
        void Unregister<TMessage>(object recipient, Action<TMessage> action);

        /// <summary>
        /// Unregisters a message recipient for a given type of messages, for
        /// a given action and a given token. Other message types will still be transmitted to the
        /// recipient (if it registered for them previously). Other actions that have
        /// been registered for the message type TMessage, for the given recipient and other tokens (if
        /// available) will also remain available.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages that the recipient wants
        /// to unregister from.</typeparam>
        /// <param name="recipient">The recipient that must be unregistered.</param>
        /// <param name="token">The token for which the recipient must be unregistered.</param>
        /// <param name="action">The action that must be unregistered for
        /// the recipient and for the message type TMessage.</param>
        void Unregister<TMessage>(object recipient, object token, Action<TMessage> action);

        #endregion
    }

    /// <summary>
    ///   Enables loosely-coupled publication of and subscription to events.
    /// </summary>
    public class EventAggregator : IEventAggregator
    {
        private readonly ConcurrentDictionary<Type, Handler> _handlers = new();

        /// <summary>
        ///   Subscribes an instance to all events declared through implementations of <see cref = "IHandle{T}" />
        /// </summary>
        /// <param name = "subscriber">The instance to subscribe for event publication.</param>
        public SubscriptionBuilder Subscribe<T>(object subscriber, Action<T> action)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException("subscriber");
            }

            Add(subscriber, typeof(T), action);
            return new SubscriptionBuilder(this, subscriber);
        }

        public void Add(object subscriber, Type messageType, Delegate action)
        {
            var handler = _handlers.GetOrAdd(messageType, x => new Handler());
            handler.Subscribe(subscriber, action);
        }

        /// <summary>
        ///   Unsubscribes the instance from all events.
        /// </summary>
        /// <param name = "subscriber">The instance to unsubscribe.</param>
        public virtual void Unsubscribe(object subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException("subscriber");
            }

            foreach (var item in _handlers)
            {
                item.Value.Unsubscribe(subscriber);
            }
        }

        /// <summary>
        ///   Unsubscribes the instance from all events.
        /// </summary>
        /// <param name = "subscriber">The instance to unsubscribe.</param>
        public virtual void Unsubscribe<T>(object subscriber)
        {
            if (_handlers.TryGetValue(typeof(T), out var handler))
            {
                handler.Unsubscribe(subscriber);
            }
        }

        /// <summary>
        ///   Publishes a message.
        /// </summary>
        /// <param name = "message">The message instance.</param>
        public virtual void Publish(object message)
        {
            var messageType = message.GetType();

            if (_handlers.TryGetValue(messageType, out Handler handler))
            {
                if (handler.Handle(message))
                {
                    _handlers.TryRemove(messageType, out _);
                }
            }
        }

        public class Handler
        {
            private readonly ConditionalWeakTable<object, Delegate> _delegates = new();

            public bool Handle(object message)
            {
                var empty = true;

                foreach (var value in _delegates)
                {
                    value.Value.DynamicInvoke(message);
                    empty = false;
                }

                return empty;
            }

            public void Subscribe(object subscriber, Delegate handler)
            {
                _delegates.AddOrUpdate(subscriber, handler);
            }

            public void Unsubscribe(object subscriber)
            {
                _delegates.Remove(subscriber);
            }
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

        #region IMessenger

        private static readonly object CreationLock = new object();
        private static IEventAggregator _defaultInstance;
        private readonly object _registerLock = new object();
        private Dictionary<Type, List<WeakActionAndToken>> _recipientsStrictAction;

#if PORTABLE
        private readonly SynchronizationContext _context = SynchronizationContext.Current;
#endif

        /// <summary>
        /// Gets the Messenger's default instance, allowing
        /// to register and send messages in a static manner.
        /// </summary>
        public static IEventAggregator Default
        {
            get
            {
                if (_defaultInstance == null)
                {
                    lock (CreationLock)
                    {
                        if (_defaultInstance == null)
                        {
                            _defaultInstance = new EventAggregator();
                        }
                    }
                }

                return _defaultInstance;
            }
        }

        #region IMessenger Members

        /// <summary>
        /// Registers a recipient for a type of message TMessage. The action
        /// parameter will be executed when a corresponding message is sent.
        /// <para>Registering a recipient does not create a hard reference to it,
        /// so if this recipient is deleted, no memory leak is caused.</para>
        /// </summary>
        /// <typeparam name="TMessage">The type of message that the recipient registers
        /// for.</typeparam>
        /// <param name="recipient">The recipient that will receive the messages.</param>
        /// <param name="action">The action that will be executed when a message
        /// of type TMessage is sent. IMPORTANT: If the action causes a closure,
        /// you must set keepTargetAlive to true to avoid side effects. </param>
        /// <param name="keepTargetAlive">If true, the target of the Action will
        /// be kept as a hard reference, which might cause a memory leak. You should only set this
        /// parameter to true if the action is using closures. See
        /// http://galasoft.ch/s/mvvmweakaction. </param>
        public virtual void Register<TMessage>(
            object recipient,
            UpdateHandler<TMessage> action,
            bool keepTargetAlive = false,
            bool completionOnly = false)
        {
            Register(recipient, null, action, keepTargetAlive, completionOnly);
        }

        /// <summary>
        /// Registers a recipient for a type of message TMessage.
        /// The action parameter will be executed when a corresponding 
        /// message is sent. See the receiveDerivedMessagesToo parameter
        /// for details on how messages deriving from TMessage (or, if TMessage is an interface,
        /// messages implementing TMessage) can be received too.
        /// <para>Registering a recipient does not create a hard reference to it,
        /// so if this recipient is deleted, no memory leak is caused.</para>
        /// <para>However if you use closures and set keepTargetAlive to true, you might
        /// cause a memory leak if you don't call <see cref="Unregister"/> when you are cleaning up.</para>
        /// </summary>
        /// <typeparam name="TMessage">The type of message that the recipient registers
        /// for.</typeparam>
        /// <param name="recipient">The recipient that will receive the messages.</param>
        /// <param name="token">A token for a messaging channel. If a recipient registers
        /// using a token, and a sender sends a message using the same token, then this
        /// message will be delivered to the recipient. Other recipients who did not
        /// use a token when registering (or who used a different token) will not
        /// get the message. Similarly, messages sent without any token, or with a different
        /// token, will not be delivered to that recipient.</param>
        /// <param name="action">The action that will be executed when a message
        /// of type TMessage is sent. IMPORTANT: If the action causes a closure,
        /// you must set keepTargetAlive to true to avoid side effects. </param>
        /// <param name="keepTargetAlive">If true, the target of the Action will
        /// be kept as a hard reference, which might cause a memory leak. You should only set this
        /// parameter to true if the action is using closures. See
        /// http://galasoft.ch/s/mvvmweakaction. </param>
        public virtual void Register<TMessage>(
            object recipient,
            object token,
            UpdateHandler<TMessage> action,
            bool keepTargetAlive = false,
            bool completionOnly = false)
        {
            lock (_registerLock)
            {
                var messageType = typeof(TMessage);

                Dictionary<Type, List<WeakActionAndToken>> recipients;

                if (_recipientsStrictAction == null)
                {
                    _recipientsStrictAction = new Dictionary<Type, List<WeakActionAndToken>>();
                }

                recipients = _recipientsStrictAction;

                lock (recipients)
                {
                    List<WeakActionAndToken> list;

                    if (!recipients.ContainsKey(messageType))
                    {
                        list = new List<WeakActionAndToken>();
                        recipients.Add(messageType, list);
                    }
                    else
                    {
                        list = recipients[messageType];
                    }

                    var weakAction = new WeakAction<TMessage>(recipient, action, keepTargetAlive);

                    var item = new WeakActionAndToken
                    {
                        Action = weakAction,
                        Token = token,
                        CompletionOnly = completionOnly
                    };

                    list.Add(item);
                }
            }

            RequestCleanup();
        }

        private bool _isCleanupRegistered;
        private DispatcherQueueController _cleanupQueue;

        /// <summary>
        /// Sends a message to registered recipients. The message will
        /// reach all recipients that registered for this message type
        /// using one of the Register methods.
        /// </summary>
        /// <typeparam name="TMessage">The type of message that will be sent.</typeparam>
        /// <param name="message">The message to send to registered recipients.</param>
        public virtual void Send<TMessage>(TMessage message, bool completion = false)
        {
            SendToTargetOrType(message, null, null, completion);
        }

        /// <summary>
        /// Sends a message to registered recipients. The message will
        /// reach only recipients that registered for this message type
        /// using one of the Register methods, and that are
        /// of the targetType.
        /// </summary>
        /// <typeparam name="TMessage">The type of message that will be sent.</typeparam>
        /// <typeparam name="TTarget">The type of recipients that will receive
        /// the message. The message won't be sent to recipients of another type.</typeparam>
        /// <param name="message">The message to send to registered recipients.</param>
        public virtual void Send<TMessage, TTarget>(TMessage message, bool completion = false)
        {
            SendToTargetOrType(message, typeof(TTarget), null, completion);
        }

        /// <summary>
        /// Sends a message to registered recipients. The message will
        /// reach only recipients that registered for this message type
        /// using one of the Register methods, and that are
        /// of the targetType.
        /// </summary>
        /// <typeparam name="TMessage">The type of message that will be sent.</typeparam>
        /// <param name="message">The message to send to registered recipients.</param>
        /// <param name="token">A token for a messaging channel. If a recipient registers
        /// using a token, and a sender sends a message using the same token, then this
        /// message will be delivered to the recipient. Other recipients who did not
        /// use a token when registering (or who used a different token) will not
        /// get the message. Similarly, messages sent without any token, or with a different
        /// token, will not be delivered to that recipient.</param>
        public virtual void Send<TMessage>(TMessage message, object token, bool completion = false)
        {
            SendToTargetOrType(message, null, token, completion);
        }

        /// <summary>
        /// Unregisters a messager recipient completely. After this method
        /// is executed, the recipient will not receive any messages anymore.
        /// </summary>
        /// <param name="recipient">The recipient that must be unregistered.</param>
        public virtual void Unregister(object recipient)
        {
            UnregisterFromLists(recipient, _recipientsStrictAction);
        }

        /// <summary>
        /// Unregisters a message recipient for a given type of messages only. 
        /// After this method is executed, the recipient will not receive messages
        /// of type TMessage anymore, but will still receive other message types (if it
        /// registered for them previously).
        /// </summary>
        /// <param name="recipient">The recipient that must be unregistered.</param>
        /// <typeparam name="TMessage">The type of messages that the recipient wants
        /// to unregister from.</typeparam>
        public virtual void Unregister<TMessage>(object recipient)
        {
            Unregister<TMessage>(recipient, null, null);
        }

        /// <summary>
        /// Unregisters a message recipient for a given type of messages only and for a given token. 
        /// After this method is executed, the recipient will not receive messages
        /// of type TMessage anymore with the given token, but will still receive other message types
        /// or messages with other tokens (if it registered for them previously).
        /// </summary>
        /// <param name="recipient">The recipient that must be unregistered.</param>
        /// <param name="token">The token for which the recipient must be unregistered.</param>
        /// <typeparam name="TMessage">The type of messages that the recipient wants
        /// to unregister from.</typeparam>
        public virtual void Unregister<TMessage>(object recipient, object token)
        {
            Unregister<TMessage>(recipient, token, null);
        }

        /// <summary>
        /// Unregisters a message recipient for a given type of messages and for
        /// a given action. Other message types will still be transmitted to the
        /// recipient (if it registered for them previously). Other actions that have
        /// been registered for the message type TMessage and for the given recipient (if
        /// available) will also remain available.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages that the recipient wants
        /// to unregister from.</typeparam>
        /// <param name="recipient">The recipient that must be unregistered.</param>
        /// <param name="action">The action that must be unregistered for
        /// the recipient and for the message type TMessage.</param>
        public virtual void Unregister<TMessage>(object recipient, Action<TMessage> action)
        {
            Unregister(recipient, null, action);
        }

        /// <summary>
        /// Unregisters a message recipient for a given type of messages, for
        /// a given action and a given token. Other message types will still be transmitted to the
        /// recipient (if it registered for them previously). Other actions that have
        /// been registered for the message type TMessage, for the given recipient and other tokens (if
        /// available) will also remain available.
        /// </summary>
        /// <typeparam name="TMessage">The type of messages that the recipient wants
        /// to unregister from.</typeparam>
        /// <param name="recipient">The recipient that must be unregistered.</param>
        /// <param name="token">The token for which the recipient must be unregistered.</param>
        /// <param name="action">The action that must be unregistered for
        /// the recipient and for the message type TMessage.</param>
        public virtual void Unregister<TMessage>(object recipient, object token, Action<TMessage> action)
        {
            UnregisterFromLists(recipient, token, action, _recipientsStrictAction);
            RequestCleanup();
        }

        #endregion

        /// <summary>
        /// Provides a way to override the Messenger.Default instance with
        /// a custom instance, for example for unit testing purposes.
        /// </summary>
        /// <param name="newMessenger">The instance that will be used as Messenger.Default.</param>
        public static void OverrideDefault(IEventAggregator newMessenger)
        {
            _defaultInstance = newMessenger;
        }

        /// <summary>
        /// Sets the Messenger's default (static) instance to null.
        /// </summary>
        public static void Reset()
        {
            _defaultInstance = null;
        }

        /// <summary>
        /// Provides a non-static access to the static <see cref="Reset"/> method.
        /// Sets the Messenger's default (static) instance to null.
        /// </summary>
        public void ResetAll()
        {
            Reset();
        }

        private static void CleanupList(IDictionary<Type, List<WeakActionAndToken>> lists)
        {
            if (lists == null)
            {
                return;
            }

            lock (lists)
            {
                var listsToRemove = new List<Type>();
                foreach (var list in lists)
                {
                    var recipientsToRemove = list.Value
                        .Where(item => item.Action == null || !item.Action.IsAlive)
                        .ToList();

                    foreach (var recipient in recipientsToRemove)
                    {
                        list.Value.Remove(recipient);
                    }

                    if (list.Value.Count == 0)
                    {
                        listsToRemove.Add(list.Key);
                    }
                }

                foreach (var key in listsToRemove)
                {
                    lists.Remove(key);
                }
            }
        }

        private static void SendToList<TMessage>(
            TMessage message,
            IList<WeakActionAndToken> weakActionsAndTokens,
            Type messageTargetType,
            object token,
            bool completion)
        {
            if (weakActionsAndTokens != null)
            {
                // Clone to protect from people registering in a "receive message" method
                // Correction Messaging BL0004.007
                //var list = weakActionsAndTokens.ToList();
                //var listClone = list.Take(list.Count()).ToList();

                // The list should be a clone already.
                var listClone = weakActionsAndTokens;

                foreach (var item in listClone)
                {
                    if (item.CompletionOnly && !completion)
                    {
                        continue;
                    }

                    var executeAction = item.Action as IExecuteWithObject;

                    if (executeAction != null
                        && item.Action.IsAlive
                        && item.Action.Target != null
                        && (messageTargetType == null
                            || item.Action.Target.GetType() == messageTargetType
                            || messageTargetType.IsAssignableFrom(item.Action.Target.GetType()))
                        && ((item.Token == null && token == null)
                            || item.Token != null && item.Token.Equals(token)))
                    {
                        if (item.Action.DispatcherQueue != null)
                        {
                            item.Action.DispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    executeAction.ExecuteWithObject(message);
                                }
                                catch { }
                            });
                        }
                        else
                        {
                            executeAction.ExecuteWithObject(message);
                        }
                    }
                }
            }
        }

        private static void UnregisterFromLists(object recipient, Dictionary<Type, List<WeakActionAndToken>> lists)
        {
            if (recipient == null
                || lists == null
                || lists.Count == 0)
            {
                return;
            }

            lock (lists)
            {
                foreach (var messageType in lists.Keys)
                {
                    foreach (var item in lists[messageType])
                    {
                        var weakAction = (IExecuteWithObject)item.Action;

                        if (weakAction != null
                            && recipient == weakAction.Target)
                        {
                            weakAction.MarkForDeletion();
                        }
                    }
                }
            }
        }

        private static void UnregisterFromLists<TMessage>(
            object recipient,
            object token,
            Action<TMessage> action,
            Dictionary<Type, List<WeakActionAndToken>> lists)
        {
            var messageType = typeof(TMessage);

            if (recipient == null
                || lists == null
                || lists.Count == 0)
            {
                return;
            }

            lock (lists)
            {
                if (lists.TryGetValue(messageType, out var value))
                {
                    foreach (var item in value)
                    {
                        var weakActionCasted = item.Action as WeakAction<TMessage>;

                        if (weakActionCasted != null
                            && recipient == weakActionCasted.Target
                            && (action == null
                                || action.Method.Name == weakActionCasted.MethodName)
                            && (token == null
                                || token.Equals(item.Token)))
                        {
                            item.Action.MarkForDeletion();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Notifies the Messenger that the lists of recipients should
        /// be scanned and cleaned up.
        /// Since recipients are stored as <see cref="WeakReference"/>, 
        /// recipients can be garbage collected even though the Messenger keeps 
        /// them in a list. During the cleanup operation, all "dead"
        /// recipients are removed from the lists. Since this operation
        /// can take a moment, it is only executed when the application is
        /// idle. For this reason, a user of the Messenger class should use
        /// <see cref="RequestCleanup"/> instead of forcing one with the 
        /// <see cref="Cleanup" /> method.
        /// </summary>
        public void RequestCleanup()
        {
            if (!_isCleanupRegistered)
            {
                DispatcherQueueHandler cleanupAction = Cleanup;

                _isCleanupRegistered = true;
                _cleanupQueue ??= DispatcherQueueController.CreateOnDedicatedThread();
                _cleanupQueue.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, cleanupAction);
            }
        }

        /// <summary>
        /// Scans the recipients' lists for "dead" instances and removes them.
        /// Since recipients are stored as <see cref="WeakReference"/>, 
        /// recipients can be garbage collected even though the Messenger keeps 
        /// them in a list. During the cleanup operation, all "dead"
        /// recipients are removed from the lists. Since this operation
        /// can take a moment, it is only executed when the application is
        /// idle. For this reason, a user of the Messenger class should use
        /// <see cref="RequestCleanup"/> instead of forcing one with the 
        /// <see cref="Cleanup" /> method.
        /// </summary>
        public void Cleanup()
        {
            CleanupList(_recipientsStrictAction);
            _isCleanupRegistered = false;
        }

        private void SendToTargetOrType<TMessage>(TMessage message, Type messageTargetType, object token, bool completion = false)
        {
            var messageType = typeof(TMessage);

            if (_recipientsStrictAction != null)
            {
                List<WeakActionAndToken> list = null;

                lock (_recipientsStrictAction)
                {
                    // Trying to improve this routine a bit.
                    if (_recipientsStrictAction.TryGetValue(messageType, out var strictList))
                    {
                        //list = _recipientsStrictAction[messageType]
                        //    .Take(_recipientsStrictAction[messageType].Count())
                        //    .ToList();
                        list = strictList.ToList();
                    }
                }

                if (list != null)
                {
                    SendToList(message, list, messageTargetType, token, completion);
                }
            }

            RequestCleanup();
        }

        #region Nested type: WeakActionAndToken

        private struct WeakActionAndToken
        {
            public WeakAction Action;

            public object Token;

            public bool CompletionOnly;
        }

        #endregion

        #endregion
    }

    public delegate void UpdateHandler<T>(object target, T update);

    /// <summary>
    /// Stores an <see cref="Action" /> without causing a hard reference
    /// to be created to the Action's owner. The owner can be garbage collected at any time.
    /// </summary>
    public class WeakAction
    {
        private Action _staticAction;

        /// <summary>
        /// Gets or sets the <see cref="MethodInfo" /> corresponding to this WeakAction's
        /// method passed in the constructor.
        /// </summary>
        protected MethodInfo Method
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the name of the method that this WeakAction represents.
        /// </summary>
        public virtual string MethodName
        {
            get
            {
                if (_staticAction != null)
                {
                    return _staticAction.Method.Name;
                }

                return Method.Name;
            }
        }

        /// <summary>
        /// Gets or sets a WeakReference to this WeakAction's action's target.
        /// This is not necessarily the same as
        /// <see cref="Reference" />, for example if the
        /// method is anonymous.
        /// </summary>
        protected WeakReference ActionReference
        {
            get;
            set;
        }

        /// <summary>
        /// Saves the <see cref="ActionReference"/> as a hard reference. This is
        /// used in relation with this instance's constructor and only if
        /// the constructor's keepTargetAlive parameter is true.
        /// </summary>
        protected object LiveReference
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a WeakReference to the target passed when constructing
        /// the WeakAction. This is not necessarily the same as
        /// <see cref="ActionReference" />, for example if the
        /// method is anonymous.
        /// </summary>
        protected WeakReference Reference
        {
            get;
            set;
        }

        /// <summary>
        /// Gets a value indicating whether the WeakAction is static or not.
        /// </summary>
        public bool IsStatic
        {
            get
            {
                return _staticAction != null;
            }
        }

        public DispatcherQueue DispatcherQueue { get; set; }

        /// <summary>
        /// Initializes an empty instance of the <see cref="WeakAction" /> class.
        /// </summary>
        protected WeakAction()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WeakAction" /> class.
        /// </summary>
        /// <param name="action">The action that will be associated to this instance.</param>
        /// <param name="keepTargetAlive">If true, the target of the Action will
        /// be kept as a hard reference, which might cause a memory leak. You should only set this
        /// parameter to true if the action is using closures. See
        /// http://galasoft.ch/s/mvvmweakaction. </param>
        public WeakAction(Action action, bool keepTargetAlive = false)
            : this(action == null ? null : action.Target, action, keepTargetAlive)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WeakAction" /> class.
        /// </summary>
        /// <param name="target">The action's owner.</param>
        /// <param name="action">The action that will be associated to this instance.</param>
        /// <param name="keepTargetAlive">If true, the target of the Action will
        /// be kept as a hard reference, which might cause a memory leak. You should only set this
        /// parameter to true if the action is using closures. See
        /// http://galasoft.ch/s/mvvmweakaction. </param>
        public WeakAction(object target, Action action, bool keepTargetAlive = false)
        {
            // A parameterized approach might might be better
            if (target is Windows.UI.Xaml.UIElement or INavigable)
            {
                DispatcherQueue = DispatcherQueue.GetForCurrentThread();
            }

            if (action.Method.IsStatic)
            {
                _staticAction = action;

                if (target != null)
                {
                    // Keep a reference to the target to control the
                    // WeakAction's lifetime.
                    Reference = new WeakReference(target);
                }

                return;
            }

            Method = action.Method;
            ActionReference = new WeakReference(action.Target);

            LiveReference = keepTargetAlive ? action.Target : null;
            Reference = new WeakReference(target);

#if DEBUG
            if (ActionReference != null
                && ActionReference.Target != null
                && !keepTargetAlive)
            {
                var type = ActionReference.Target.GetType();

                if (type.Name.StartsWith("<>")
                    && type.Name.Contains("DisplayClass"))
                {
                    System.Diagnostics.Debug.WriteLine(
                        "You are attempting to register a lambda with a closure without using keepTargetAlive. Are you sure? Check http://galasoft.ch/s/mvvmweakaction for more info.");
                }
            }
#endif
        }

        /// <summary>
        /// Gets a value indicating whether the Action's owner is still alive, or if it was collected
        /// by the Garbage Collector already.
        /// </summary>
        public virtual bool IsAlive
        {
            get
            {
                if (_staticAction == null
                    && Reference == null
                    && LiveReference == null)
                {
                    return false;
                }

                if (_staticAction != null)
                {
                    if (Reference != null)
                    {
                        return Reference.IsAlive;
                    }

                    return true;
                }

                // Non static action

                if (LiveReference != null)
                {
                    return true;
                }

                if (Reference?.Target != null)
                {
                    return Reference.IsAlive;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the Action's owner. This object is stored as a 
        /// <see cref="WeakReference" />.
        /// </summary>
        public object Target
        {
            get
            {
                if (Reference == null)
                {
                    return null;
                }

                return Reference.Target;
            }
        }

        /// <summary>
        /// The target of the weak reference.
        /// </summary>
        protected object ActionTarget
        {
            get
            {
                if (LiveReference != null)
                {
                    return LiveReference;
                }

                if (ActionReference == null)
                {
                    return null;
                }

                return ActionReference.Target;
            }
        }

        /// <summary>
        /// Executes the action. This only happens if the action's owner
        /// is still alive.
        /// </summary>
        public void Execute()
        {
            if (_staticAction != null)
            {
                _staticAction();
                return;
            }

            var actionTarget = ActionTarget;

            if (IsAlive)
            {
                if (Method != null
                    && (LiveReference != null
                        || ActionReference != null)
                    && actionTarget != null)
                {
                    Method.Invoke(actionTarget, null);

                    // ReSharper disable RedundantJumpStatement
                    return;
                    // ReSharper restore RedundantJumpStatement
                }
            }
        }

        /// <summary>
        /// Sets the reference that this instance stores to null.
        /// </summary>
        public void MarkForDeletion()
        {
            Reference = null;
            ActionReference = null;
            LiveReference = null;
            Method = null;
            _staticAction = null;
        }
    }

    /// <summary>
    /// Stores an Action without causing a hard reference
    /// to be created to the Action's owner. The owner can be garbage collected at any time.
    /// </summary>
    /// <typeparam name="T">The type of the Action's parameter.</typeparam>
    public class WeakAction<T> : WeakAction, IExecuteWithObject
    {
        private UpdateHandler<T> _staticAction;

        /// <summary>
        /// Gets the name of the method that this WeakAction represents.
        /// </summary>
        public override string MethodName
        {
            get
            {
                if (_staticAction != null)
                {
                    return _staticAction.Method.Name;
                }

                return Method.Name;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Action's owner is still alive, or if it was collected
        /// by the Garbage Collector already.
        /// </summary>
        public override bool IsAlive
        {
            get
            {
                if (_staticAction == null
                    && Reference == null)
                {
                    return false;
                }

                if (_staticAction != null)
                {
                    if (Reference != null)
                    {
                        return Reference.IsAlive;
                    }

                    return true;
                }

                // Accessing Target seems to be mandatory
                // to retrieve the correct reference state.
                return Reference.Target != null
                    && Reference.IsAlive;
            }
        }

        /// <summary>
        /// Initializes a new instance of the WeakAction class.
        /// </summary>
        /// <param name="action">The action that will be associated to this instance.</param>
        /// <param name="keepTargetAlive">If true, the target of the Action will
        /// be kept as a hard reference, which might cause a memory leak. You should only set this
        /// parameter to true if the action is using closures. See
        /// http://galasoft.ch/s/mvvmweakaction. </param>
        public WeakAction(UpdateHandler<T> action, bool keepTargetAlive = false)
            : this(action == null ? null : action.Target, action, keepTargetAlive)
        {
        }

        /// <summary>
        /// Initializes a new instance of the WeakAction class.
        /// </summary>
        /// <param name="target">The action's owner.</param>
        /// <param name="action">The action that will be associated to this instance.</param>
        /// <param name="keepTargetAlive">If true, the target of the Action will
        /// be kept as a hard reference, which might cause a memory leak. You should only set this
        /// parameter to true if the action is using closures. See
        /// http://galasoft.ch/s/mvvmweakaction. </param>
        public WeakAction(object target, UpdateHandler<T> action, bool keepTargetAlive = false)
        {
            // A parameterized approach might might be better
            if (target is Windows.UI.Xaml.UIElement or INavigable)
            {
                DispatcherQueue = DispatcherQueue.GetForCurrentThread();
            }

            if (action.Method.IsStatic)
            {
                _staticAction = action;

                if (target != null)
                {
                    // Keep a reference to the target to control the
                    // WeakAction's lifetime.
                    Reference = new WeakReference(target);
                }

                return;
            }

            Method = action.Method;
            ActionReference = new WeakReference(action.Target);

            LiveReference = keepTargetAlive ? action.Target : null;
            Reference = new WeakReference(target);

#if DEBUG
            if (ActionReference != null
                && ActionReference.Target != null
                && !keepTargetAlive)
            {
                var type = ActionReference.Target.GetType();

                if (type.Name.StartsWith("<>")
                    && type.Name.Contains("DisplayClass"))
                {
                    System.Diagnostics.Debug.WriteLine(
                        "You are attempting to register a lambda with a closure without using keepTargetAlive. Are you sure? Check http://galasoft.ch/s/mvvmweakaction for more info.");
                }
            }
#endif
        }

        /// <summary>
        /// Executes the action. This only happens if the action's owner
        /// is still alive. The action's parameter is set to default(T).
        /// </summary>
        public new void Execute()
        {
            Execute(default(T));
        }

        /// <summary>
        /// Executes the action. This only happens if the action's owner
        /// is still alive.
        /// </summary>
        /// <param name="parameter">A parameter to be passed to the action.</param>
        public void Execute(T parameter)
        {
            if (_staticAction != null)
            {
                _staticAction(null, parameter);
                return;
            }

            var actionTarget = ActionTarget;

            if (IsAlive)
            {
                if (Method != null
                    && (LiveReference != null
                        || ActionReference != null)
                    && actionTarget != null)
                {
                    Method.Invoke(
                        actionTarget,
                        new object[]
                        {
                            Target,
                            parameter
                        });
                }
            }
        }

        /// <summary>
        /// Executes the action with a parameter of type object. This parameter
        /// will be casted to T. This method implements <see cref="IExecuteWithObject.ExecuteWithObject" />
        /// and can be useful if you store multiple WeakAction{T} instances but don't know in advance
        /// what type T represents.
        /// </summary>
        /// <param name="parameter">The parameter that will be passed to the action after
        /// being casted to T.</param>
        public void ExecuteWithObject(object parameter)
        {
            var parameterCasted = (T)parameter;
            Execute(parameterCasted);
        }

        /// <summary>
        /// Sets all the actions that this WeakAction contains to null,
        /// which is a signal for containing objects that this WeakAction
        /// should be deleted.
        /// </summary>
        public new void MarkForDeletion()
        {
            _staticAction = null;
            base.MarkForDeletion();
        }
    }

    public interface IExecuteWithObject
    {
        /// <summary>
        /// The target of the WeakAction.
        /// </summary>
        object Target
        {
            get;
        }

        /// <summary>
        /// Executes an action.
        /// </summary>
        /// <param name="parameter">A parameter passed as an object,
        /// to be casted to the appropriate type.</param>
        void ExecuteWithObject(object parameter);

        /// <summary>
        /// Deletes all references, which notifies the cleanup method
        /// that this entry must be deleted.
        /// </summary>
        void MarkForDeletion();
    }
}
