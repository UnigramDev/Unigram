using System;

namespace LibVLCSharp.Shared
{
    internal class MediaListEventManager : EventManager
    {
        EventHandler<MediaListItemAddedEventArgs>? _mediaListItemAdded;
        EventHandler<MediaListWillAddItemEventArgs>? _mediaListWillAddItem;
        EventHandler<MediaListItemDeletedEventArgs>? _mediaListItemDeleted;
        EventHandler<MediaListWillDeleteItemEventArgs>? _mediaListWillDeleteItem;
        EventHandler<EventArgs>? _mediaListEndReached;

        public MediaListEventManager(IntPtr ptr) : base(ptr)
        {
        }

        protected internal override void AttachEvent<T>(EventType eventType, EventHandler<T> eventHandler)
        {
            switch (eventType)
            {
                case EventType.MediaListItemAdded:
                    _mediaListItemAdded += eventHandler as EventHandler<MediaListItemAddedEventArgs>;
                    Attach(eventType, OnItemAdded);
                    break;
                case EventType.MediaListWillAddItem:
                    _mediaListWillAddItem += eventHandler as EventHandler<MediaListWillAddItemEventArgs>;
                    Attach(eventType, OnWillAddItem);
                    break;
                case EventType.MediaListItemDeleted:
                    _mediaListItemDeleted += eventHandler as EventHandler<MediaListItemDeletedEventArgs>;
                    Attach(eventType, OnItemDeleted);
                    break;
                case EventType.MediaListViewWillDeleteItem:
                    _mediaListWillDeleteItem += eventHandler as EventHandler<MediaListWillDeleteItemEventArgs>;
                    Attach(eventType, OnWillDeleteItem);
                    break;
                case EventType.MediaListEndReached:
                    _mediaListEndReached += eventHandler as EventHandler<EventArgs>;
                    Attach(eventType, OnEndReached);
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
                case EventType.MediaListItemAdded:
                    _mediaListItemAdded -= eventHandler as EventHandler<MediaListItemAddedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaListWillAddItem:
                    _mediaListWillAddItem -= eventHandler as EventHandler<MediaListWillAddItemEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaListItemDeleted:
                    _mediaListItemDeleted -= eventHandler as EventHandler<MediaListItemDeletedEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaListViewWillDeleteItem:
                    _mediaListWillDeleteItem -= eventHandler as EventHandler<MediaListWillDeleteItemEventArgs>;
                    Detach(eventType);
                    break;
                case EventType.MediaListEndReached:
                    _mediaListEndReached -= eventHandler as EventHandler<EventArgs>;
                    Detach(eventType);
                    break;
                default:
                    OnEventUnhandled(this, eventType);
                    break;
            }
        }

        void OnItemAdded(IntPtr ptr)
        {
            var itemAdded = RetrieveEvent(ptr).Union.MediaListItemAdded;

            _mediaListItemAdded?.Invoke(this,
                new MediaListItemAddedEventArgs(new Media(itemAdded.MediaInstance), itemAdded.Index));
        }

        void OnWillAddItem(IntPtr ptr)
        {
            var willAddItem = RetrieveEvent(ptr).Union.MediaListWillAddItem;

            _mediaListWillAddItem?.Invoke(this,
                new MediaListWillAddItemEventArgs(new Media(willAddItem.MediaInstance), willAddItem.Index));
        }

        void OnItemDeleted(IntPtr ptr)
        {
            var itemDeleted = RetrieveEvent(ptr).Union.MediaListItemDeleted;

            _mediaListItemDeleted?.Invoke(this,
                new MediaListItemDeletedEventArgs(new Media(itemDeleted.MediaInstance), itemDeleted.Index));
        }

        void OnWillDeleteItem(IntPtr ptr)
        {
            var willDeleteItem = RetrieveEvent(ptr).Union.MediaListWillDeleteItem;

            _mediaListWillDeleteItem?.Invoke(this,
                new MediaListWillDeleteItemEventArgs(new Media(willDeleteItem.MediaInstance), willDeleteItem.Index));
        }

        void OnEndReached(IntPtr ptr)
        {
            _mediaListEndReached?.Invoke(this, EventArgs.Empty);
        }
    }
}
