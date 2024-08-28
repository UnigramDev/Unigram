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

    // TODO: Use above class instead
    public partial class DictionaryStringDouble : Dictionary<string, double>
    {

    }

    internal sealed class MvxObservableCollectionWinRTTypeDetails : global::WinRT.IWinRTExposedTypeDetails
    {
        public global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry[] GetExposedInterfaces()
        {
            _ = global::WinRT.TelegramGenericHelpers.IList_string.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IReadOnlyList_string.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_char.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IReadOnlyList_System_Collections_Generic_IEnumerable_char_.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_object.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IReadOnlyList_System_Collections_Generic_IEnumerable_object_.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IReadOnlyList_System_Collections_IEnumerable.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IReadOnlyList_object.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_string.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_System_Collections_Generic_IEnumerable_char_.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_System_Collections_Generic_IEnumerable_object_.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_System_Collections_IEnumerable.Initialized;

            return new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry[]
            {
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IListMethods<string>.IID,
                Vtable = global::ABI.System.Collections.Generic.IListMethods<string>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IReadOnlyListMethods<string>.IID,
                Vtable = global::ABI.System.Collections.Generic.IReadOnlyListMethods<string>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.Generic.IEnumerable<char>>.IID,
                Vtable = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.Generic.IEnumerable<char>>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.Generic.IEnumerable<object>>.IID,
                Vtable = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.Generic.IEnumerable<object>>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.IEnumerable>.IID,
                Vtable = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.IEnumerable>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IReadOnlyListMethods<object>.IID,
                Vtable = global::ABI.System.Collections.Generic.IReadOnlyListMethods<object>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IEnumerableMethods<string>.IID,
                Vtable = global::ABI.System.Collections.Generic.IEnumerableMethods<string>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.Generic.IEnumerable<char>>.IID,
                Vtable = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.Generic.IEnumerable<char>>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.Generic.IEnumerable<object>>.IID,
                Vtable = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.Generic.IEnumerable<object>>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.IEnumerable>.IID,
                Vtable = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.IEnumerable>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IEnumerableMethods<object>.IID,
                Vtable = global::ABI.System.Collections.Generic.IEnumerableMethods<object>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.IListMethods.IID,
                Vtable = global::ABI.System.Collections.IListMethods.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.IEnumerableMethods.IID,
                Vtable = global::ABI.System.Collections.IEnumerableMethods.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.ComponentModel.INotifyPropertyChangedMethods.IID,
                Vtable = global::ABI.System.ComponentModel.INotifyPropertyChangedMethods.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Specialized.INotifyCollectionChangedMethods.IID,
                Vtable = global::ABI.System.Collections.Specialized.INotifyCollectionChangedMethods.AbiToProjectionVftablePtr
            },
    };
        }
    }

    internal sealed class IncrementalCollectionWinRTTypeDetails : global::WinRT.IWinRTExposedTypeDetails
    {
        public global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry[] GetExposedInterfaces()
        {
            _ = global::WinRT.TelegramGenericHelpers.IList_string.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IReadOnlyList_string.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_char.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IReadOnlyList_System_Collections_Generic_IEnumerable_char_.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_object.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IReadOnlyList_System_Collections_Generic_IEnumerable_object_.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IReadOnlyList_System_Collections_IEnumerable.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IReadOnlyList_object.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_string.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_System_Collections_Generic_IEnumerable_char_.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_System_Collections_Generic_IEnumerable_object_.Initialized;
            _ = global::WinRT.TelegramGenericHelpers.IEnumerable_System_Collections_IEnumerable.Initialized;

            return new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry[]
            {
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IListMethods<string>.IID,
                Vtable = global::ABI.System.Collections.Generic.IListMethods<string>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IReadOnlyListMethods<string>.IID,
                Vtable = global::ABI.System.Collections.Generic.IReadOnlyListMethods<string>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.Generic.IEnumerable<char>>.IID,
                Vtable = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.Generic.IEnumerable<char>>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.Generic.IEnumerable<object>>.IID,
                Vtable = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.Generic.IEnumerable<object>>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.IEnumerable>.IID,
                Vtable = global::ABI.System.Collections.Generic.IReadOnlyListMethods<global::System.Collections.IEnumerable>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IReadOnlyListMethods<object>.IID,
                Vtable = global::ABI.System.Collections.Generic.IReadOnlyListMethods<object>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IEnumerableMethods<string>.IID,
                Vtable = global::ABI.System.Collections.Generic.IEnumerableMethods<string>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.Generic.IEnumerable<char>>.IID,
                Vtable = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.Generic.IEnumerable<char>>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.Generic.IEnumerable<object>>.IID,
                Vtable = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.Generic.IEnumerable<object>>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.IEnumerable>.IID,
                Vtable = global::ABI.System.Collections.Generic.IEnumerableMethods<global::System.Collections.IEnumerable>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Generic.IEnumerableMethods<object>.IID,
                Vtable = global::ABI.System.Collections.Generic.IEnumerableMethods<object>.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.IListMethods.IID,
                Vtable = global::ABI.System.Collections.IListMethods.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.IEnumerableMethods.IID,
                Vtable = global::ABI.System.Collections.IEnumerableMethods.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.Collections.Specialized.INotifyCollectionChangedMethods.IID,
                Vtable = global::ABI.System.Collections.Specialized.INotifyCollectionChangedMethods.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.System.ComponentModel.INotifyPropertyChangedMethods.IID,
                Vtable = global::ABI.System.ComponentModel.INotifyPropertyChangedMethods.AbiToProjectionVftablePtr
            },
            new global::System.Runtime.InteropServices.ComWrappers.ComInterfaceEntry
            {
                IID = global::ABI.Microsoft.UI.Xaml.Data.ISupportIncrementalLoadingMethods.IID,
                Vtable = global::ABI.Microsoft.UI.Xaml.Data.ISupportIncrementalLoadingMethods.AbiToProjectionVftablePtr
            },
    };
        }
    }
}
