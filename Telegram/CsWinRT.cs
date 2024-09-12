using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Telegram
{
    internal static class VtableInitialization
    {
        // This registers with CsWinRT a way to provide vtables for a couple types that CsWinRT
        // doesn't detect today but we use so that they can be made AOT and trimming safe.
        [ModuleInitializer]
        internal static void Initialize()
        {
            WinRT.ComWrappersSupport.RegisterTypeComInterfaceEntriesLookup(LookupVtableEntries);
            WinRT.ComWrappersSupport.RegisterTypeRuntimeClassNameLookup(new Func<Type, string>(LookupRuntimeClassName));
        }

        private static ComWrappers.ComInterfaceEntry[] LookupVtableEntries(Type type)
        {
            var typeName = type.ToString();
            // Workaround an issue in CsWinRT where it doesn't handle nested types correctly
            // in its Vtable lookup by defining our own entry for it.  Will be addressed in
            // upcoming CsWinRT versions.
            if (typeName == "System.Collections.Specialized.ReadOnlyList")
            {
                // CsWinRT already generates these for other scenarios we use.
                _ = WinRT.TelegramGenericHelpers.IReadOnlyList_object.Initialized;
                _ = WinRT.TelegramGenericHelpers.IEnumerable_object.Initialized;

                return new ComWrappers.ComInterfaceEntry[]
                {
                        new ComWrappers.ComInterfaceEntry
                        {
                            IID = ABI.System.Collections.Generic.IReadOnlyListMethods<object>.IID,
                            Vtable = ABI.System.Collections.Generic.IReadOnlyListMethods<object>.AbiToProjectionVftablePtr
                        },
                        new ComWrappers.ComInterfaceEntry
                        {
                            IID = ABI.System.Collections.Generic.IEnumerableMethods<object>.IID,
                            Vtable = ABI.System.Collections.Generic.IEnumerableMethods<object>.AbiToProjectionVftablePtr
                        },
                        new ComWrappers.ComInterfaceEntry
                        {
                            IID = ABI.System.Collections.IListMethods.IID,
                            Vtable = ABI.System.Collections.IListMethods.AbiToProjectionVftablePtr
                        },
                        new ComWrappers.ComInterfaceEntry
                        {
                            IID = ABI.System.Collections.IEnumerableMethods.IID,
                            Vtable = ABI.System.Collections.IEnumerableMethods.AbiToProjectionVftablePtr
                        },
                };
            }
            // This is an internal type in linq which CsWinRT can not detect today.
            // Given we know we use it and pass it across the ABI, providing a vtable
            // for it here.
            //else if (typeName == "System.Linq.Enumerable+RangeIterator")
            //{
            //    // CsWinRT already generates these for other scenarios we use.
            //    _ = WinRT.App1GenericHelpers.IEnumerable_int.Initialized;

            //    return new ComWrappers.ComInterfaceEntry[]
            //    {
            //            new ComWrappers.ComInterfaceEntry
            //            {
            //                IID = ABI.System.Collections.Generic.IEnumerableMethods<int>.IID,
            //                Vtable = ABI.System.Collections.Generic.IEnumerableMethods<int>.AbiToProjectionVftablePtr
            //            },
            //            new ComWrappers.ComInterfaceEntry
            //            {
            //                IID = ABI.System.Collections.IEnumerableMethods.IID,
            //                Vtable = ABI.System.Collections.IEnumerableMethods.AbiToProjectionVftablePtr
            //            },
            //    };
            //}

            return default;
        }

        private static string LookupRuntimeClassName(Type type)
        {
            var typeName = type.ToString();
            if (typeName == "System.Collections.Specialized.ReadOnlyList")
            {
                return "Windows.Foundation.Collections.IVectorView`1<Object>";
            }
            //else if (typeName == "System.Linq.Enumerable+RangeIterator")
            //{
            //    return "Windows.Foundation.Collections.IIterable`1<Int32>";
            //}

            return default;
        }
    }
}
