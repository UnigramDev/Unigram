using System;

namespace LibVLCSharp.Shared
{
    internal class MediaDiscovererEventManager : EventManager
    {
        EventHandler<EventArgs>? _mediaDiscovererStarted;
        EventHandler<EventArgs>? _mediaDiscovererStopped;

        public MediaDiscovererEventManager(IntPtr ptr) : base(ptr)
        {
        }

        protected internal override void AttachEvent<T>(EventType eventType, EventHandler<T> eventHandler)
        {
            switch (eventType)
            {
                case EventType.MediaDiscovererStarted:
                    _mediaDiscovererStarted += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnStarted);
                    break;
                case EventType.MediaDiscovererStopped:
                    _mediaDiscovererStopped += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnStopped);
                    break;
                default:
                    OnEventUnhandled(this, eventType);
                    break;
            }
        }

        protected internal override void DetachEvent<T>(EventType eventType, EventHandler<T> eventHandler)
        {
            switch (eventType)
            {
                case EventType.MediaDiscovererStarted:
                    _mediaDiscovererStarted -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaDiscovererStopped:
                    _mediaDiscovererStopped -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                default:
                    OnEventUnhandled(this, eventType);
                    break;
            }
        }

        void OnStarted(IntPtr ptr)
        {
            _mediaDiscovererStarted?.Invoke(this, EventArgs.Empty);
        }

        void OnStopped(IntPtr ptr)
        {
            _mediaDiscovererStopped?.Invoke(this, EventArgs.Empty);
        }
    }
}
