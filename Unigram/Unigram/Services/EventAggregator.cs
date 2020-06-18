using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

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
        void Handle(TMessage update);
    }

    /// <summary>
    ///   Enables loosely-coupled publication of and subscription to events.
    /// </summary>
    public interface IEventAggregator
    {
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
    }

    /// <summary>
    ///   Enables loosely-coupled publication of and subscription to events.
    /// </summary>
    public class EventAggregator : IEventAggregator
    {
        readonly List<Handler> handlers = new List<Handler>();

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
            return handlers.Any(handler => handler.Handles(messageType) & !handler.IsDead);
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
            lock (handlers)
            {
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
            lock (handlers)
            {
                var found = handlers.FirstOrDefault(x => x.Matches(subscriber));

                if (found != null)
                {
                    handlers.Remove(found);
                }
            }
        }

        /// <summary>
        ///   Publishes a message.
        /// </summary>
        /// <param name = "message">The message instance.</param>
        public virtual void Publish(object message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            Handler[] toNotify;
            lock (handlers)
            {
                toNotify = handlers.ToArray();
            }

            var messageType = message.GetType();

            var dead = toNotify
                .Where(handler => !handler.Handle(messageType, message))
                .ToImmutableHashSet();

            if (dead.Any())
            {
                lock (handlers)
                {
                    handlers.RemoveAll(x => dead.Contains(x));
                }
            }
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

                var interfaces = handler.GetType().GetInterfaces()
                    .Where(x => typeof(IHandle).IsAssignableFrom(x) && x.IsConstructedGenericType);

                foreach (var @interface in interfaces)
                {
                    var type = @interface.GenericTypeArguments[0];
                    var method = @interface.GetMethod("Handle");
                    supportedHandlers[type] = method;
                }
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

                if (supportedHandlers.TryGetValue(messageType, out MethodInfo method))
                {
                    method.Invoke(target, new[] { message });
                }

                return true;
            }

            public bool Handles(Type messageType)
            {
                return supportedHandlers.ContainsKey(messageType);
            }
        }
    }
}
