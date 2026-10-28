//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if UNPACKAGED
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using Microsoft.Win32;
using Telegram.Navigation;

namespace Telegram.Common
{
    /// <summary>
    /// What a package's manifest gives its toasts, provided by the process itself: something for
    /// the shell to call when one is clicked. The shell finds this CLSID through the
    /// AppUserModelId key's CustomActivator and calls the class object registered here - or, with
    /// the app not running, starts LocalServer32 and waits for the new process to register it.
    ///
    /// Every button comes here too, not only clicks on the toast: a desktop app gets no
    /// activationType='background', so a reply arrives exactly the way a click does.
    /// </summary>
    internal static partial class ToastActivator
    {
        public static readonly Guid Clsid = new("3542F50C-8B9A-4A2B-A47D-9E5EAF762103");

        // LocalServer32's. Nothing reads it: a cold start is an ordinary launch, and the click
        // arrives through the class object once Program has registered it.
        public const string CommandLineArgument = "-ToastActivated";

        private const uint CLSCTX_LOCAL_SERVER = 0x4;
        private const uint REGCLS_MULTIPLEUSE = 0x1;
        private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);

        private static readonly StrategyBasedComWrappers _wrappers = new();

        private static readonly object _lock = new();
        private static List<Activation> _pending = new();
        private static SynchronizationContext _context;

        private readonly record struct Activation(string Arguments, Dictionary<string, string> Input);

        /// <summary>
        /// For the life of the process: the app exits rather than stopping being a toast target.
        /// On the thread that becomes the UI thread, before it pumps: the shell's calls then arrive
        /// on it, and one made during startup waits in the queue until the app is running.
        /// </summary>
        public static void Register()
        {
            using (var server = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\" + Clsid.ToString("B") + @"\LocalServer32"))
            {
                server.SetValue(null, $"\"{Environment.ProcessPath}\" {CommandLineArgument}");
            }

            var factory = _wrappers.GetOrCreateComInterfaceForObject(new ClassFactory(), CreateComInterfaceFlags.None);

            try
            {
                Marshal.ThrowExceptionForHR(CoRegisterClassObject(Clsid, factory, CLSCTX_LOCAL_SERVER, REGCLS_MULTIPLEUSE, out _));
            }
            finally
            {
                // COM took its own reference.
                Marshal.Release(factory);
            }
        }

        /// <summary>
        /// On the UI thread, once the first activation has made the window. A cold start from a
        /// toast is a launch first: the click can reach the class object before the app has
        /// anything to activate into, so it waits here until then.
        /// </summary>
        public static void Ready()
        {
            List<Activation> pending;

            lock (_lock)
            {
                _context = SynchronizationContext.Current;
                pending = _pending;
                _pending = null;
            }

            foreach (var activation in pending)
            {
                Route(activation);
            }
        }

        // Posted rather than run inline whatever thread this is on: it is a COM call from the
        // shell, which should not be held for the length of a navigation.
        private static void Activate(string arguments, Dictionary<string, string> input)
        {
            Logger.Info(arguments);

            var activation = new Activation(arguments, input);

            lock (_lock)
            {
                if (_pending != null)
                {
                    _pending.Add(activation);
                    return;
                }
            }

            _context.Post(Route, activation);
        }

        private static void Route(object state)
        {
            Route((Activation)state);
        }

        private static void Route(Activation activation)
        {
            var data = Toast.SplitArguments(activation.Arguments);

            foreach (var item in activation.Input)
            {
                data[item.Key] = item.Value;
            }

            if (data.ContainsKey("action"))
            {
                _ = Toast.ProcessAsync(data);
            }
            else
            {
                BootStrapper.Current.ActivateFromToast(activation.Arguments, activation.Input);
            }
        }

        [LibraryImport("ole32.dll")]
        private static partial int CoRegisterClassObject(in Guid rclsid, IntPtr pUnk, uint dwClsContext, uint flags, out uint lpdwRegister);

        [GeneratedComInterface]
        [Guid("00000001-0000-0000-C000-000000000046")]
        internal partial interface IClassFactory
        {
            [PreserveSig]
            int CreateInstance(IntPtr outer, in Guid iid, out IntPtr instance);

            [PreserveSig]
            int LockServer(int fLock);
        }

        [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
        [Guid("53E31837-6600-4A81-9395-75CFFE746F94")]
        internal partial interface INotificationActivationCallback
        {
            void Activate(string appUserModelId, string invokedArgs, IntPtr data, uint count);
        }

        [GeneratedComClass]
        internal sealed partial class ClassFactory : IClassFactory
        {
            public int CreateInstance(IntPtr outer, in Guid iid, out IntPtr instance)
            {
                instance = IntPtr.Zero;

                if (outer != IntPtr.Zero)
                {
                    return CLASS_E_NOAGGREGATION;
                }

                var unknown = _wrappers.GetOrCreateComInterfaceForObject(new Callback(), CreateComInterfaceFlags.None);
                var hr = Marshal.QueryInterface(unknown, iid, out instance);

                Marshal.Release(unknown);
                return hr;
            }

            public int LockServer(int fLock)
            {
                return 0;
            }
        }

        [GeneratedComClass]
        internal sealed partial class Callback : INotificationActivationCallback
        {
            // data is an array of NOTIFICATION_USER_INPUT_DATA, two LPCWSTRs each: the input's id
            // and what was typed or picked in it.
            public void Activate(string appUserModelId, string invokedArgs, IntPtr data, uint count)
            {
                var input = new Dictionary<string, string>((int)count);

                for (int i = 0; i < count; i++)
                {
                    var entry = data + i * 2 * IntPtr.Size;
                    var key = Marshal.PtrToStringUni(Marshal.ReadIntPtr(entry));

                    if (key != null)
                    {
                        input[key] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(entry, IntPtr.Size)) ?? string.Empty;
                    }
                }

                ToastActivator.Activate(invokedArgs, input);
            }
        }
    }
}
#endif
