using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Telegram.Td.Api;
using Unigram.Common;

namespace Unigram.Services
{
    /// <summary>
    ///   A marker interface for classes that subscribe to messages.
    /// </summary>
    public interface IHandle { }

    /// <summary>
    ///   Denotes a class which can handle a particular type of message.
    /// </summary>
    /// <typeparam name = "TMessage">The type of message to handle.</typeparam>
    public interface IHandle<TMessage> : IHandle
    {  //don't use contravariance here
        /// <summary>
        ///   Handles the message.
        /// </summary>
        /// <param name = "message">The message.</param>
        void Handle(TMessage message);
    }

    /// <summary>
    ///   Enables loosely-coupled publication of and subscription to events.
    /// </summary>
    public interface IEventAggregator
    {
        /// <summary>
        ///   Gets or sets the default publication thread marshaller.
        /// </summary>
        /// <value>
        ///   The default publication thread marshaller.
        /// </value>
        Action<System.Action> PublicationThreadMarshaller { get; set; }

        /// <summary>
        /// Searches the subscribed handlers to check if we have a handler for
        /// the message type supplied.
        /// </summary>
        /// <param name="messageType">The message type to check with</param>
        /// <returns>True if any handler is found, false if not.</returns>
        bool HandlerExistsFor(Type messageType);

        /// <summary>
        ///   Subscribes an instance to all events declared through implementations of <see cref = "IHandle{T}" />
        /// </summary>
        /// <param name = "subscriber">The instance to subscribe for event publication.</param>
        void Subscribe(object subscriber);
        void Subscribe(object subscriber, int fileId);

        /// <summary>
        ///   Unsubscribes the instance from all events.
        /// </summary>
        /// <param name = "subscriber">The instance to unsubscribe.</param>
        void Unsubscribe(object subscriber);
        void Unsubscribe(object subscriber, int fileId);

        /// <summary>
        ///   Publishes a message.
        /// </summary>
        /// <param name = "message">The message instance.</param>
        /// <remarks>
        ///   Uses the default thread marshaller during publication.
        /// </remarks>
        void Publish(object message);

        /// <summary>
        ///   Publishes a message.
        /// </summary>
        /// <param name = "message">The message instance.</param>
        /// <param name = "marshal">Allows the publisher to provide a custom thread marshaller for the message publication.</param>
        void Publish(object message, Action<System.Action> marshal);
    }

    /// <summary>
    ///   Enables loosely-coupled publication of and subscription to events.
    /// </summary>
    public class EventAggregator : IEventAggregator
    {
        readonly List<Handler> _handlers = new List<Handler>();
        readonly ConcurrentDictionary<int, List<Handler>> _handlersByFile = new ConcurrentDictionary<int, List<Handler>>();

        /// <summary>
        ///   The default thread marshaller used for publication;
        /// </summary>
        public static Action<System.Action> DefaultPublicationThreadMarshaller = action => action();

        /// <summary>
        /// Processing of handler results on publication thread.
        /// </summary>
        public static Action<object, object> HandlerResultProcessing = (target, result) => { };

        public static IEventAggregator Instance { get; protected set; }

        /// <summary>
        ///   Initializes a new instance of the <see cref = "EventAggregator" /> class.
        /// </summary>
        public EventAggregator()
        {
            PublicationThreadMarshaller = DefaultPublicationThreadMarshaller;

            Instance = this;
        }

        /// <summary>
        ///   Gets or sets the default publication thread marshaller.
        /// </summary>
        /// <value>
        ///   The default publication thread marshaller.
        /// </value>
        public Action<System.Action> PublicationThreadMarshaller { get; set; }

        /// <summary>
        /// Searches the subscribed handlers to check if we have a handler for
        /// the message type supplied.
        /// </summary>
        /// <param name="messageType">The message type to check with</param>
        /// <returns>True if any handler is found, false if not.</returns>
        public bool HandlerExistsFor(Type messageType)
        {
            return _handlers.Any(handler => handler.Handles(messageType) & !handler.IsDead);
        }

        /// <summary>
        ///   Subscribes an instance to all events declared through implementations of <see cref = "IHandle{T}" />
        /// </summary>
        /// <param name = "subscriber">The instance to subscribe for event publication.</param>
        public virtual void Subscribe(object subscriber)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException("subscriber");
            }
            lock (_handlers)
            {
                if (_handlers.Any(x => x.Matches(subscriber)))
                {
                    return;
                }

                _handlers.Add(new Handler(subscriber));
            }
        }

        public virtual void Subscribe(object subscriber, int fileId)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException("subscriber");
            }
            lock (_handlersByFile)
            {
                var handlers = _handlersByFile.GetOrAdd(fileId, x => new List<Handler>());
                if (handlers.Any(x => x.Matches(subscriber)))
                {
                    return;
                }

                handlers.Add(new Handler(subscriber));
            }
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
            lock (_handlers)
            {
                var found = _handlers.FirstOrDefault(x => x.Matches(subscriber));

                if (found != null)
                {
                    _handlers.Remove(found);
                }
            }
        }

        public virtual void Unsubscribe(object subscriber, int fileId)
        {
            if (subscriber == null)
            {
                throw new ArgumentNullException("subscriber");
            }
            lock (_handlersByFile)
            {
                if (_handlersByFile.TryGetValue(fileId, out List<Handler> handlers))
                {
                    var found = handlers.FirstOrDefault(x => x.Matches(subscriber));

                    if (found != null)
                    {
                        _handlers.Remove(found);
                    }
                }
            }
        }

        public static bool LogPublish { get; set; }

        /// <summary>
        ///   Publishes a message.
        /// </summary>
        /// <param name = "message">The message instance.</param>
        /// <remarks>
        ///   Does not marshall the the publication to any special thread by default.
        /// </remarks>
        [DebuggerStepThrough]
        public virtual void Publish(object message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

#if DEBUG
            if (LogPublish)
            {
                Debug.WriteLine("Publish " + message.GetType());
            }
#endif

            Publish(message, PublicationThreadMarshaller);
        }

        /// <summary>
        ///   Publishes a message.
        /// </summary>
        /// <param name = "message">The message instance.</param>
        /// <param name = "marshal">Allows the publisher to provide a custom thread marshaller for the message publication.</param>
        public virtual void Publish(object message, Action<System.Action> marshal)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }
            if (marshal == null)
            {
                throw new ArgumentNullException("marshal");
            }

            Handler[] toNotify;
            if (message is UpdateFile update)
            {
                lock (_handlersByFile)
                {
                    toNotify = _handlersByFile.GetOrAdd(update.File.Id, x => new List<Handler>()).ToArray();
                }
            }
            else
            {
                lock (_handlers)
                {
                    toNotify = _handlers.ToArray();
                }
            }

            marshal(() => {
                var messageType = message.GetType();

                var dead = toNotify
                    .Where(handler => !handler.Handle(messageType, message))
                    .ToList();

                if (dead.Any())
                {
                    lock (_handlers)
                    {
                        dead.Apply(x => _handlers.Remove(x));
                    }
                }
            });
        }

        class Handler
        {
            readonly WeakReference reference;
            readonly Dictionary<Type, MethodInfo> supportedHandlers = new Dictionary<Type, MethodInfo>();

            public bool IsDead
            {
                get { return reference.Target == null; }
            }

            public Handler(object handler)
            {
                reference = new WeakReference(handler);

#if WIN_RT
                var handlerInfo = typeof(IHandle).GetTypeInfo();
                var interfaces = handler.GetType().GetTypeInfo().ImplementedInterfaces
                    .Where(x => handlerInfo.IsAssignableFrom(x.GetTypeInfo()) && x.GetTypeInfo().IsGenericType);

                foreach (var @interface in interfaces)
                {
                    var type = @interface.GenericTypeArguments[0];
                    var method = @interface.GetTypeInfo().DeclaredMethods.First(x => x.Name == "Handle");
                    supportedHandlers[type] = method;
                }
#else
                var interfaces = handler.GetType().GetInterfaces()
                    .Where(x => typeof(IHandle).IsAssignableFrom(x) && x.IsConstructedGenericType);

                foreach(var @interface in interfaces) {
                    var type = @interface.GetGenericArguments()[0];
                    var method = @interface.GetMethod("Handle");
                    supportedHandlers[type] = method;
                }
#endif
            }

            public bool Matches(object instance)
            {
                return reference.Target == instance;
            }

            public bool Handle(Type messageType, object message)
            {
                var target = reference.Target;
                if (target == null)
                {
                    return false;
                }

                foreach (var pair in supportedHandlers)
                {
                    if (pair.Key.IsAssignableFrom(messageType))
                    {
                        var result = pair.Value.Invoke(target, new[] { message });
                        if (result != null)
                        {
                            HandlerResultProcessing(target, result);
                        }
                    }
                }

                return true;
            }

            public bool Handles(Type messageType)
            {
                return supportedHandlers.Any(pair => pair.Key.IsAssignableFrom(messageType));
            }
        }
    }
}
