using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared
{
    /// <summary>
    /// The MediaList holds a list of Media types
    /// </summary>
    public partial class MediaList : Internal, IEnumerable<Media>
    {
        MediaListEventManager _eventManager;
        readonly object _syncLock = new object();
        bool _nativeLock;

        readonly struct Native
        {

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_subitems")]
            internal static extern IntPtr LibVLCMediaSubitems(Media media);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_new")]
            internal static extern IntPtr LibVLCMediaListNew(LibVLC instance);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_release")]
            internal static extern void LibVLCMediaListRelease(IntPtr mediaList);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_discoverer_media_list")]
            internal static extern IntPtr LibVLCMediaDiscovererMediaList(MediaDiscoverer discovererMediaList);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_set_media")]
            internal static extern void LibVLCMediaListSetMedia(IntPtr mediaList, Media media);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_add_media")]
            internal static extern int LibVLCMediaListAddMedia(IntPtr mediaList, Media media);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_insert_media")]
            internal static extern int LibVLCMediaListInsertMedia(IntPtr mediaList, Media media, int positionIndex);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_remove_index")]
            internal static extern int LibVLCMediaListRemoveIndex(IntPtr mediaList, int positionIndex);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_count")]
            internal static extern int LibVLCMediaListCount(IntPtr mediaList);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_item_at_index")]
            internal static extern IntPtr LibVLCMediaListItemAtIndex(IntPtr mediaList, int positionIndex);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_index_of_item")]
            internal static extern int LibVLCMediaListIndexOfItem(IntPtr mediaList, Media media);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_is_readonly")]
            internal static extern int LibVLCMediaListIsReadonly(IntPtr mediaList);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_lock")]
            internal static extern void LibVLCMediaListLock(IntPtr mediaList);


            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_unlock")]
            internal static extern void LibVLCMediaListUnlock(IntPtr mediaList);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_event_manager")]
            internal static extern IntPtr LibVLCMediaListEventManager(IntPtr mediaList);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "libvlc_media_list_retain")]
            internal static extern void LibVLCMediaListRetain(IntPtr mediaList);
        }

        /// <summary>
        /// Get subitems of media descriptor object.
        /// </summary>
        /// <param name="media"></param>
        public MediaList(Media media)
            : base(Native.LibVLCMediaSubitems(media))
        {
        }

        /// <summary>
        /// Get media service discover media list.
        /// </summary>
        /// <param name="mediaDiscoverer"></param>
        public MediaList(MediaDiscoverer mediaDiscoverer)
            : base(Native.LibVLCMediaDiscovererMediaList(mediaDiscoverer))
        {
        }

        /// <summary>
        /// Create an empty media list.
        /// </summary>
        /// <param name="libVLC"></param>
        public MediaList(LibVLC libVLC)
            : base(Native.LibVLCMediaListNew(libVLC))
        {
        }

        internal MediaList(IntPtr mediaListPtr)
            : base(mediaListPtr)
        {
        }

        protected override bool ReleaseHandle()
        {
            Native.LibVLCMediaListRelease(handle);
            return true;
        }

        /// <summary>
        /// Associate media instance with this media list instance. If another
        /// media instance was present it will be released.
        /// </summary>
        /// <param name="media">media instance to add</param>
        public void SetMedia(Media media) => Native.LibVLCMediaListSetMedia(handle, media);

        /// <summary>
        /// Add media instance to media list
        /// </summary>
        /// <param name="media">a media instance</param>
        /// <returns>true on success, false if the media list is read-only</returns>
        public bool AddMedia(Media media) => NativeSync(() => Native.LibVLCMediaListAddMedia(handle, media) == 0);

        T NativeSync<T>(Func<T> operation)
        {
            try
            {
                lock (_syncLock)
                {
                    if (!_nativeLock)
                        Lock();
                    return operation();
                }
            }
            finally
            {
                lock (_syncLock)
                {
                    if (_nativeLock)
                        Unlock();
                }
            }
        }

        /// <summary>
        /// Insert media instance in media list on a position.
        /// </summary>
        /// <param name="media">a media instance</param>
        /// <param name="position">position in the array where to insert</param>
        /// <returns>true on success, false if the media list is read-only</returns>
        public bool InsertMedia(Media media, int position) =>
            NativeSync(() =>
            {
                if (media == null) throw new ArgumentNullException(nameof(media));
                return Native.LibVLCMediaListInsertMedia(handle, media, position) == 0;
            });

        /// <summary>
        /// Remove media instance from media list on a position.
        /// </summary>
        /// <param name="positionIndex">position in the array where to remove the iteam</param>
        /// <returns>true on success, false if the media list is read-only or the item was not found</returns>
        public bool RemoveIndex(int positionIndex) => NativeSync(() => Native.LibVLCMediaListRemoveIndex(handle, positionIndex) == 0);

        /// <summary>
        /// Get count on media list items.
        /// </summary>
        public int Count => NativeSync(() => Native.LibVLCMediaListCount(handle));

        /// <summary>
        /// Gets the element at the specified index
        /// </summary>
        /// <param name="position">position in array where to insert</param>
        /// <returns>media instance at position, or null if not found.
        /// In case of success, Media.Retain() is called to increase the refcount on the media. </returns>
        public Media this[int position] => NativeSync(() =>
        {
            var ptr = Native.LibVLCMediaListItemAtIndex(handle, position);
            return ptr == IntPtr.Zero ? null : new Media(ptr);
        });

        /// <summary>
        /// Find index position of List media instance in media list. Warning: the
        /// function will return the first matched position.
        /// </summary>
        /// <param name="media">media instance</param>
        /// <returns>position of media instance or -1 if media not found</returns>
        public int IndexOf(Media media) => NativeSync(() => Native.LibVLCMediaListIndexOfItem(handle, media));

        /// <summary>
        /// This indicates if this media list is read-only from a user point of view.
        /// True if readonly, false otherwise
        /// </summary>
        public bool IsReadonly => Native.LibVLCMediaListIsReadonly(handle) == 1;

        /// <summary>
        /// Get lock on media list items
        /// </summary>
        internal void Lock()
        {
            lock (_syncLock)
            {
                if (_nativeLock)
                    throw new InvalidOperationException("already locked");

                _nativeLock = true;
                Native.LibVLCMediaListLock(handle);
            }
        }

        /// <summary>
        /// Release lock on media list items The MediaList lock should be held upon entering this function.
        /// </summary>
        internal void Unlock()
        {
            lock (_syncLock)
            {
                if (!_nativeLock)
                    throw new InvalidOperationException("not locked");

                _nativeLock = false;
                Native.LibVLCMediaListUnlock(handle);
            }
        }

        /// <summary>
        /// Get libvlc_event_manager from this media list instance. The
        /// p_event_manager is immutable, so you don't have to hold the lock
        /// </summary>
        MediaListEventManager EventManager
        {
            get
            {
                if (_eventManager != null) return _eventManager;
                var ptr = Native.LibVLCMediaListEventManager(handle);
                _eventManager = new MediaListEventManager(ptr);
                return _eventManager;
            }
        }

        /// <summary>Increments the native reference counter for this medialist instance</summary>
        internal void Retain() => Native.LibVLCMediaListRetain(handle);

        #region Events

        /// <summary>
        /// An item has been added to the MediaList
        /// </summary>
        public event EventHandler<MediaListItemAddedEventArgs> ItemAdded
        {
            add => EventManager.AttachEvent(EventType.MediaListItemAdded, value);
            remove => EventManager.DetachEvent(EventType.MediaListItemAdded, value);
        }

        /// <summary>
        /// An item is about to be added to the MediaList
        /// </summary>
        public event EventHandler<MediaListWillAddItemEventArgs> WillAddItem
        {
            add => EventManager.AttachEvent(EventType.MediaListWillAddItem, value);
            remove => EventManager.DetachEvent(EventType.MediaListWillAddItem, value);
        }

        /// <summary>
        /// An item has been deleted from the MediaList
        /// </summary>
        public event EventHandler<MediaListItemDeletedEventArgs> ItemDeleted
        {
            add => EventManager.AttachEvent(EventType.MediaListItemDeleted, value);
            remove => EventManager.DetachEvent(EventType.MediaListItemDeleted, value);
        }

        /// <summary>
        /// An item is about to be deleted from the MediaList
        /// </summary>
        public event EventHandler<MediaListWillDeleteItemEventArgs> WillDeleteItem
        {
            add => EventManager.AttachEvent(EventType.MediaListWillDeleteItem, value);
            remove => EventManager.DetachEvent(EventType.MediaListWillDeleteItem, value);
        }

        /// <summary>
        /// The media list reached its end
        /// </summary>
        public event EventHandler<EventArgs> EndReached
        {
            add => EventManager.AttachEvent(EventType.MediaListEndReached, value);
            remove => EventManager.DetachEvent(EventType.MediaListEndReached, value);
        }

        #endregion

        /// <summary>
        /// Returns an enumerator that iterates through a collection of media
        /// </summary>
        /// <returns>an enumerator over a media collection</returns>
        public IEnumerator<Media> GetEnumerator() => new MediaListEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal class MediaListEnumerator : IEnumerator<Media>
        {
            int position = -1;
            MediaList _mediaList;

            internal MediaListEnumerator(MediaList mediaList)
            {
                _mediaList = mediaList;
            }

            public bool MoveNext()
            {
                position++;
                return position < (_mediaList?.Count ?? 0);
            }

            void IEnumerator.Reset()
            {
                position = -1;
            }

            public void Dispose()
            {
                position = -1;
                _mediaList = default;
            }

            object IEnumerator.Current => Current;

            public Media Current
            {
                get
                {
                    if (_mediaList == null)
                    {
                        throw new ObjectDisposedException(nameof(MediaListEnumerator));
                    }
                    return _mediaList[position] ?? throw new ArgumentOutOfRangeException(nameof(position));
                }
            }
        }
    }
}
