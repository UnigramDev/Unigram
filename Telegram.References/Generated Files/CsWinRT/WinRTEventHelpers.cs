//------------------------------------------------------------------------------
// <auto-generated>
//     This file was generated by cswinrt.exe version 2.1.1.240807.2
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
using System;
namespace WinRT
{

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__global__Telegram_Native_Calls_RemoteMediaStateUpdatedEventArgs_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.RemoteMediaStateUpdatedEventArgs>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipManager__Telegram_Native_Calls_RemoteMediaStateUpdatedEventArgs.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__global__Telegram_Native_Calls_RemoteMediaStateUpdatedEventArgs_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.RemoteMediaStateUpdatedEventArgs> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.RemoteMediaStateUpdatedEventArgs>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.RemoteMediaStateUpdatedEventArgs>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.RemoteMediaStateUpdatedEventArgs>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.RemoteMediaStateUpdatedEventArgs> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipManager sender, global::Telegram.Native.Calls.RemoteMediaStateUpdatedEventArgs args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__int_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, int>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipManager__Int32.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__int_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, int> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, int>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, int>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, int>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, int> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipManager sender, int args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__global__Telegram_Native_Calls_SignalingDataEmittedEventArgs_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.SignalingDataEmittedEventArgs>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipManager__Telegram_Native_Calls_SignalingDataEmittedEventArgs.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__global__Telegram_Native_Calls_SignalingDataEmittedEventArgs_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.SignalingDataEmittedEventArgs> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.SignalingDataEmittedEventArgs>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.SignalingDataEmittedEventArgs>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.SignalingDataEmittedEventArgs>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.SignalingDataEmittedEventArgs> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipManager sender, global::Telegram.Native.Calls.SignalingDataEmittedEventArgs args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__global__Telegram_Native_Calls_VoipState_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.VoipState>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipManager__Telegram_Native_Calls_VoipState.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__global__Telegram_Native_Calls_VoipState_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.VoipState> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.VoipState>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.VoipState>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.VoipState>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, global::Telegram.Native.Calls.VoipState> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipManager sender, global::Telegram.Native.Calls.VoipState args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipScreenCapture__bool_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, bool>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipScreenCapture__Boolean.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipScreenCapture__bool_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, bool> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, bool>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, bool>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, bool>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, bool> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipScreenCapture sender, bool args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipGroupManager__global__Telegram_Native_Calls_GroupNetworkStateChangedEventArgs_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.GroupNetworkStateChangedEventArgs>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipGroupManager__Telegram_Native_Calls_GroupNetworkStateChangedEventArgs.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipGroupManager__global__Telegram_Native_Calls_GroupNetworkStateChangedEventArgs_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.GroupNetworkStateChangedEventArgs> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.GroupNetworkStateChangedEventArgs>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.GroupNetworkStateChangedEventArgs>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.GroupNetworkStateChangedEventArgs>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.GroupNetworkStateChangedEventArgs> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipGroupManager sender, global::Telegram.Native.Calls.GroupNetworkStateChangedEventArgs args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_HttpProxyWatcher__bool_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.HttpProxyWatcher, bool>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_HttpProxyWatcher__Boolean.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_HttpProxyWatcher__bool_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.HttpProxyWatcher, bool> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.HttpProxyWatcher, bool>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.HttpProxyWatcher, bool>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.HttpProxyWatcher, bool>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.HttpProxyWatcher, bool> GetEventInvoke()
            {
                return (global::Telegram.Native.HttpProxyWatcher sender, bool args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__bool_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, bool>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipManager__Boolean.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__bool_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, bool> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, bool>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, bool>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, bool>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, bool> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipManager sender, bool args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__float_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, float>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipManager__Float.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipManager__float_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, float> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, float>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, float>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, float>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipManager, float> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipManager sender, float args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipGroupManager__global__Telegram_Native_Calls_BroadcastPartRequestedEventArgs_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastPartRequestedEventArgs>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipGroupManager__Telegram_Native_Calls_BroadcastPartRequestedEventArgs.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipGroupManager__global__Telegram_Native_Calls_BroadcastPartRequestedEventArgs_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastPartRequestedEventArgs> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastPartRequestedEventArgs>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastPartRequestedEventArgs>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastPartRequestedEventArgs>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastPartRequestedEventArgs> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipGroupManager sender, global::Telegram.Native.Calls.BroadcastPartRequestedEventArgs args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipGroupManager__global__System_Collections_Generic_IList_global__Telegram_Native_Calls_VoipGroupParticipant__ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::System.Collections.Generic.IList<global::Telegram.Native.Calls.VoipGroupParticipant>>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipGroupManager__Windows_Foundation_Collections_IVector_1_Telegram_Native_Calls_VoipGroupParticipant_.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipGroupManager__global__System_Collections_Generic_IList_global__Telegram_Native_Calls_VoipGroupParticipant__(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::System.Collections.Generic.IList<global::Telegram.Native.Calls.VoipGroupParticipant>> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::System.Collections.Generic.IList<global::Telegram.Native.Calls.VoipGroupParticipant>>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::System.Collections.Generic.IList<global::Telegram.Native.Calls.VoipGroupParticipant>>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::System.Collections.Generic.IList<global::Telegram.Native.Calls.VoipGroupParticipant>>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::System.Collections.Generic.IList<global::Telegram.Native.Calls.VoipGroupParticipant>> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipGroupManager sender, global::System.Collections.Generic.IList<global::Telegram.Native.Calls.VoipGroupParticipant> args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipGroupManager__global__Telegram_Native_Calls_BroadcastTimeRequestedEventArgs_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastTimeRequestedEventArgs>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipGroupManager__Telegram_Native_Calls_BroadcastTimeRequestedEventArgs.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipGroupManager__global__Telegram_Native_Calls_BroadcastTimeRequestedEventArgs_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastTimeRequestedEventArgs> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastTimeRequestedEventArgs>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastTimeRequestedEventArgs>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastTimeRequestedEventArgs>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipGroupManager, global::Telegram.Native.Calls.BroadcastTimeRequestedEventArgs> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipGroupManager sender, global::Telegram.Native.Calls.BroadcastTimeRequestedEventArgs args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

    internal sealed unsafe class _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipScreenCapture__object_ : global::ABI.WinRT.Interop.EventSource<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, object>>
    {
        private static readonly bool initialized = global::WinRT.GenericTypeInstantiations.Windows_Foundation_TypedEventHandler_2_Telegram_Native_Calls_VoipScreenCapture__object.EnsureInitialized();

        internal _EventSource_global__Windows_Foundation_TypedEventHandler_global__Telegram_Native_Calls_VoipScreenCapture__object_(IObjectReference obj,
        delegate* unmanaged[Stdcall]<System.IntPtr, System.IntPtr, WinRT.EventRegistrationToken*, int> addHandler,
        delegate* unmanaged[Stdcall]<System.IntPtr, WinRT.EventRegistrationToken, int> removeHandler, int index) : base(obj, addHandler, removeHandler, index)
        {
            _ = initialized;
        }

        protected override ObjectReferenceValue CreateMarshaler(global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, object> handler) =>
        global::ABI.Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, object>.CreateMarshaler2(handler);

        protected override global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, object>> CreateEventSourceState() =>
        new EventState(ObjectReference.ThisPtr, Index);

        private sealed class EventState : global::ABI.WinRT.Interop.EventSourceState<global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, object>>
        {
            public EventState(System.IntPtr obj, int index)
            : base(obj, index)
            {
            }

            protected override global::Windows.Foundation.TypedEventHandler<global::Telegram.Native.Calls.VoipScreenCapture, object> GetEventInvoke()
            {
                return (global::Telegram.Native.Calls.VoipScreenCapture sender, object args) =>
                {
                    var targetDelegate = TargetDelegate;
                    if (targetDelegate is null)
                    {
                        return ;
                    }
                    targetDelegate.Invoke(sender, args);
                };
            }
        }
    }

}