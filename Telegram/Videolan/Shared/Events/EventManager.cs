using LibVLCSharp.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared
{
    internal abstract class EventManager
    {
        internal readonly struct Native
        {
            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_event_attach")]
            internal static extern int LibVLCEventAttach(IntPtr eventManager, EventType eventType, InternalEventCallback eventCallback,
                IntPtr userData);

            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_event_detach")]
            internal static extern void LibVLCEventDetach(IntPtr eventManager, EventType eventType, InternalEventCallback eventCallback,
                IntPtr userData);
        }

        /// <summary>
        /// The class that manages one type of event
        /// </summary>
        internal class EventTypeManager : IDisposable
        {
            internal Action<IntPtr> EventHandler { get; }
            internal GCHandle Handle { get; }

            internal EventTypeManager(Action<IntPtr> eventHandler)
            {
                EventHandler = eventHandler;
                Handle = GCHandle.Alloc(this);
            }

            internal int HandlerCount { get; set; } = 0;

            public void Dispose()
            {
                Handle.Free();
            }
        }

        internal IntPtr NativeReference;
        readonly Dictionary<EventType, EventTypeManager> _eventManagers = new Dictionary<EventType, EventTypeManager>();

        internal protected EventManager(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                throw new NullReferenceException(nameof(ptr));

            NativeReference = ptr;
        }

        private bool AttachNativeEvent(EventType eventType, EventTypeManager eventManager)
        {
            return Native.LibVLCEventAttach(NativeReference, eventType, EventCallbackHandle, GCHandle.ToIntPtr(eventManager.Handle)) == 0;
        }

        private void DetachNativeEvent(EventType eventType, EventTypeManager eventManager)
        {
            Native.LibVLCEventDetach(NativeReference, eventType, EventCallbackHandle, GCHandle.ToIntPtr(eventManager.Handle));
        }

        /// <summary>
        /// Increments the reference count to the event handler method.
        /// </summary>
        /// <param name="eventType">The event type</param>
        /// <param name="eventHandler">The event handler for this event type</param>
        protected void Attach(EventType eventType, Action<IntPtr> eventHandler)
        {
            lock (_eventManagers)
            {
                if (!_eventManagers.TryGetValue(eventType, out var mgr))
                {
                    mgr = new EventTypeManager(eventHandler);
                    if (AttachNativeEvent(eventType, mgr))
                    {
                        _eventManagers.Add(eventType, mgr);
                    }
                    else
                    {
                        mgr.Dispose();
                        throw new VLCException($"Could not attach event {eventType}");
                    }
                }
                mgr.HandlerCount++;
            }
        }

        protected void Detach(EventType eventType)
        {
            lock (_eventManagers)
            {
                if (_eventManagers.TryGetValue(eventType, out var mgr))
                {
                    mgr.HandlerCount--;
                    if (mgr.HandlerCount == 0)
                    {
                        DetachNativeEvent(eventType, mgr);
                        mgr.Dispose();
                        _eventManagers.Remove(eventType);
                    }
                }
            }
        }

        static readonly InternalEventCallback EventCallbackHandle = EventCallback;

        [MonoPInvokeCallback(typeof(InternalEventCallback))]
        private static void EventCallback(IntPtr evt, IntPtr userData)
        {
            var eventManager = MarshalUtils.GetInstance<EventTypeManager>(userData);
            eventManager?.EventHandler(evt);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void InternalEventCallback(IntPtr evt, IntPtr userData);

        internal protected LibVLCEvent RetrieveEvent(IntPtr eventPtr) => MarshalUtils.PtrToStructure<LibVLCEvent>(eventPtr);

        internal protected void OnEventUnhandled(object sender, EventType eventType)
            => throw new InvalidOperationException($"eventType {nameof(eventType)} unhandled by type {sender.GetType().Name}");

        internal protected abstract void AttachEvent<T>(EventType eventType, EventHandler<T> eventHandler) where T : EventArgs;
        internal protected abstract void DetachEvent<T>(EventType eventType, EventHandler<T> eventHandler) where T : EventArgs;
    }
}
