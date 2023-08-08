using System;

namespace LibVLCSharp.Shared
{
    internal class RendererDiscovererEventManager : EventManager
    {
        EventHandler<RendererDiscovererItemAddedEventArgs>? _itemAdded;
        EventHandler<RendererDiscovererItemDeletedEventArgs>? _itemDeleted;

        internal RendererDiscovererEventManager(IntPtr ptr) : base(ptr)
        {
        }

        protected internal override void AttachEvent<T>(EventType eventType, EventHandler<T> eventHandler)
        {
            switch (eventType)
            {
                case EventType.RendererDiscovererItemAdded:
                    _itemAdded += eventHandler as EventHandler<RendererDiscovererItemAddedEventArgs>;
                    Attach(eventType, OnItemAdded);
                    break;
                case EventType.RendererDiscovererItemDeleted:
                    _itemDeleted += eventHandler as EventHandler<RendererDiscovererItemDeletedEventArgs>;
                    Attach(eventType, OnItemDeleted);
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
                case EventType.RendererDiscovererItemAdded:
                    _itemAdded -= eventHandler as EventHandler<RendererDiscovererItemAddedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.RendererDiscovererItemDeleted:
                    _itemDeleted -= eventHandler as EventHandler<RendererDiscovererItemDeletedEventArgs>;
                    Detach(eventType);
                    break;
                default:
                    OnEventUnhandled(this, eventType);
                    break;
            }
        }

        void OnItemDeleted(IntPtr args)
        {
            var rendererItem = RetrieveEvent(args).Union.RendererDiscovererItemDeleted;
            _itemDeleted?.Invoke(this, new RendererDiscovererItemDeletedEventArgs(new RendererItem(rendererItem.item)));
        }

        void OnItemAdded(IntPtr args)
        {
            var rendererItem = RetrieveEvent(args).Union.RendererDiscovererItemAdded;
            _itemAdded?.Invoke(this, new RendererDiscovererItemAddedEventArgs(new RendererItem(rendererItem.item)));
        }
    }
}
