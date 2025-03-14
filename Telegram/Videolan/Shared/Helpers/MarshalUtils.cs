using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared.Helpers
{
    internal static class MarshalUtils
    {
        internal readonly struct Native
        {
#pragma warning disable IDE1006 // Naming Styles
#if DESKTOP
            #region Windows

            [DllImport(Constants.Msvcrt, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int _wfopen_s(out IntPtr pFile, string filename, string mode = Write);

            [DllImport(Constants.Msvcrt, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fclose", SetLastError = true)]
            public static extern int fcloseWindows(IntPtr stream);

            #endregion

            #region Linux

            [DllImport(Constants.Libc, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fopen", CharSet = CharSet.Ansi, SetLastError = true)]
            public static extern IntPtr fopenLinux(string filename, string mode = Write);

            [DllImport(Constants.Libc, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fclose", CharSet = CharSet.Ansi, SetLastError = true)]
            public static extern int fcloseLinux(IntPtr file);

            #endregion

            #region Mac

            [DllImport(Constants.LibSystem, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fopen", SetLastError = true)]
            public static extern IntPtr fopenMac(string path, string mode = Write);

            [DllImport(Constants.LibSystem, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fclose", SetLastError = true)]
            public static extern int fcloseMac(IntPtr file);

            #endregion
#endif
            [DllImport(Constants.LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_free", SetLastError = true)]
            public static extern void LibVLCFree(IntPtr ptr);
#pragma warning restore IDE1006 // Naming Styles
        }

        #region logging

        static string UseStructurePointer<T>(T structure, Func<IntPtr, string> action) where T : notnull
        {
            var structurePointer = IntPtr.Zero;
            try
            {
                structurePointer = Marshal.AllocHGlobal(Marshal.SizeOf(structure));
                Marshal.StructureToPtr(structure, structurePointer, false);
                return action(structurePointer);
            }
            finally
            {
                Marshal.FreeHGlobal(structurePointer);
            }
        }

        static void UseStructurePointer<T>(T structure, Action<IntPtr> action) where T : notnull
        {
            var structurePointer = IntPtr.Zero;
            try
            {
                structurePointer = Marshal.AllocHGlobal(Marshal.SizeOf(structure));
                Marshal.StructureToPtr(structure, structurePointer, false);
                action(structurePointer);
            }
            finally
            {
                Marshal.FreeHGlobal(structurePointer);
            }
        }

        #endregion
        /// <summary>
        /// Helper for libvlc_new
        /// </summary>
        /// <param name="options">libvlc options, an UTF16 string array turned to UTF8 string pointer array</param>
        /// <param name="create">the create function call</param>
        /// <returns>the result of the create function</returns>
        /// <exception cref="VLCException">Thrown when libvlc could not be created</exception>
        internal static IntPtr CreateWithOptions(string[] options, Func<int, IntPtr[], IntPtr> create)
        {
            var utf8Args = default(IntPtr[]);
            try
            {
                utf8Args = options.ToUtf8();
                Core.EnsureLoaded();
                return create(utf8Args.Length, utf8Args);
            }
            catch (DllNotFoundException ex)
            {
                throw new VLCException("LibVLC could not be created. Make sure that you have done the following:" +
                    $"{Environment.NewLine}- Installed latest LibVLC from nuget for your target platform." +
                    $"{Environment.NewLine}{ex.Message} {ex.StackTrace}");
            }
            finally
            {
                if (!(utf8Args is null))
                {
                    foreach (var arg in utf8Args)
                    {
                        if (arg != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(arg);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generic marshalling function to retrieve structs from a libvlc linked list
        /// </summary>
        /// <typeparam name="T">Internal struct type</typeparam>
        /// <typeparam name="TU">publicly facing struct type</typeparam>
        /// <param name="getRef">Native libvlc call: retrieve collection start pointer from parent reference</param>
        /// <param name="retrieve">Retrieve the internal struct by marshalling the native pointer</param>
        /// <param name="create">Create a publicly facing struct from the internal struct values</param>
        /// <param name="next">Access next element in the list</param>
        /// <param name="releaseRef">Native libvlc call: release resources allocated with the getRef call</param>
        /// <returns>An array of publicly facing struct types</returns>
        internal static TU[] Retrieve<T, TU>(Func<IntPtr> getRef, Func<IntPtr, T> retrieve,
            Func<T, TU> create, Func<T, IntPtr> next, Action<IntPtr> releaseRef)
            where T : struct
            where TU : struct
        {
            var nativeRef = IntPtr.Zero;

            try
            {
                nativeRef = getRef();
                if (nativeRef == IntPtr.Zero)
                {
#if NETSTANDARD1_1 || NET40
                    return new TU[0];
#else
                    return Array.Empty<TU>();
#endif
                }

                var resultList = new List<TU>();
                var nextRef = nativeRef;
                T structure;
                TU obj;

                while (nextRef != IntPtr.Zero)
                {
                    structure = retrieve(nextRef);
                    obj = create(structure);
                    resultList.Add(obj);
                    nextRef = next(structure);
                }
                return resultList.ToArray();
            }
            finally
            {
                if (nativeRef != IntPtr.Zero)
                {
                    releaseRef(nativeRef);
                    nativeRef = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Generic marshalling function to retrieve structs from libvlc by reading from unmanaged memory with offsets
        /// This supports uint libvlc signatures.
        /// </summary>
        /// <typeparam name="T">Internal struct type</typeparam>
        /// <typeparam name="TU">publicly facing struct type</typeparam>
        /// <param name="nativeRef">native reference of the parent</param>
        /// <param name="getRef">Native libvlc call: retrieve collection start pointer from parent reference</param>
        /// <param name="retrieve">Retrieve the internal struct by marshalling the native pointer</param>
        /// <param name="create">Create a publicly facing struct from the internal struct values</param>
        /// <param name="releaseRef">Native libvlc call: release the array allocated with the getRef call with the given element count</param>
        /// <returns>An array of publicly facing struct types</returns>
        internal static TU[] Retrieve<T, TU>(IntPtr nativeRef, ArrayOut getRef, Func<IntPtr, T> retrieve,
            Func<T, TU> create, Action<IntPtr, uint> releaseRef)
            where T : struct
            where TU : struct
        {
            var arrayPtr = IntPtr.Zero;
            uint count = 0;

            try
            {
                count = getRef(nativeRef, out arrayPtr);
                if (count == 0)
                {
#if NETSTANDARD1_1 || NET40
                    return new TU[0];
#else
                    return Array.Empty<TU>();
#endif
                }

                var resultList = new List<TU>();
                T structure;

                for (var i = 0; i < count; i++)
                {
                    var ptr = Marshal.ReadIntPtr(arrayPtr, i * IntPtr.Size);
                    structure = retrieve(ptr);
                    var managedStruct = create(structure);
                    resultList.Add(managedStruct);
                }

                return resultList.ToArray();
            }
            finally
            {
                if (arrayPtr != IntPtr.Zero)
                {
                    releaseRef(arrayPtr, count);
                    arrayPtr = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Generic marshalling function to retrieve structs from libvlc by reading from unmanaged memory with offsets
        /// </summary>
        /// <typeparam name="T">Internal struct type</typeparam>
        /// <typeparam name="TU">publicly facing struct type</typeparam>
        /// <param name="nativeRef">native reference of the parent</param>
        /// <param name="getRef">Native libvlc call: retrieve collection start pointer from parent reference</param>
        /// <param name="retrieve">Retrieve the internal struct by marshalling the native pointer</param>
        /// <param name="create">Create a publicly facing struct from the internal struct values</param>
        /// <param name="releaseRef">Native libvlc call: release the array allocated with the getRef call with the given element count</param>
        /// <returns>An array of publicly facing struct types</returns>
        internal static TU[] Retrieve<T, TU>(IntPtr nativeRef, ArrayLongOut getRef, Func<IntPtr, T> retrieve,
            Func<T, TU> create, Action<IntPtr, UIntPtr> releaseRef)
            where T : struct
            where TU : struct
        {
            var arrayPtr = IntPtr.Zero;
            var countSizeT = UIntPtr.Zero;
            var count = 0;

            try
            {
                countSizeT = getRef(nativeRef, out arrayPtr);
                if (IntPtr.Size == 4)
                {
                    count = Convert.ToInt32((uint)countSizeT);
                }
                else if (IntPtr.Size == 8)
                {
                    count = Convert.ToInt32((ulong)countSizeT);
                }

                if (count == 0)
                {
#if NETSTANDARD1_1 || NET40
                    return new TU[0];
#else
                    return Array.Empty<TU>();
#endif
                }

                var resultList = new List<TU>();
                T structure;

                for (var i = 0; i < count; i++)
                {
                    var ptr = Marshal.ReadIntPtr(arrayPtr, i * IntPtr.Size);
                    structure = retrieve(ptr);
                    var managedStruct = create(structure);
                    resultList.Add(managedStruct);
                }

                return resultList.ToArray();
            }
            finally
            {
                if (arrayPtr != IntPtr.Zero)
                {
                    releaseRef(arrayPtr, countSizeT);
                    arrayPtr = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Generic marshalling function to retrieve structs from libvlc by reading from unmanaged memory with offsets
        /// This supports an additional enum configuration parameter.
        /// </summary>
        /// <typeparam name="T">Internal struct type</typeparam>
        /// <typeparam name="TU">publicly facing struct type</typeparam>
        /// <typeparam name="TE">Additional enum confugation type</typeparam>
        /// <param name="nativeRef">native reference of the parent</param>
        /// <param name="extraParam">Additional enum confugation type</param>
        /// <param name="getRef">Native libvlc call: retrieve collection start pointer from parent reference</param>
        /// <param name="retrieve">Retrieve the internal struct by marshalling the native pointer</param>
        /// <param name="create">Create a publicly facing struct from the internal struct values</param>
        /// <param name="releaseRef">Native libvlc call: release the array allocated with the getRef call with the given element count</param>
        /// <returns>An array of publicly facing struct types</returns>
        internal static TU[] Retrieve<T, TU, TE>(IntPtr nativeRef, TE extraParam, CategoryArrayOut<TE> getRef, Func<IntPtr, T> retrieve,
            Func<T, TU> create, Action<IntPtr, UIntPtr> releaseRef)
            where T : struct
            where TU : struct
            where TE : Enum
        {
            var arrayPtr = IntPtr.Zero;
            var countSizeT = UIntPtr.Zero;
            var count = 0;

            try
            {
                countSizeT = getRef(nativeRef, extraParam, out arrayPtr);
                if (IntPtr.Size == 4)
                {
                    count = Convert.ToInt32((uint)countSizeT);
                }
                else if (IntPtr.Size == 8)
                {
                    count = Convert.ToInt32((ulong)countSizeT);
                }

                if (count == 0)
                {
#if NETSTANDARD1_1 || NET40
                    return new TU[0];
#else
                    return Array.Empty<TU>();
#endif
                }

                var resultList = new List<TU>();
                T structure;

                for (var i = 0; i < count; i++)
                {
                    var ptr = Marshal.ReadIntPtr(arrayPtr, i * IntPtr.Size);
                    structure = retrieve(ptr);
                    var managedStruct = create(structure);
                    resultList.Add(managedStruct);
                }

                return resultList.ToArray();
            }
            finally
            {
                if (arrayPtr != IntPtr.Zero)
                {
                    releaseRef(arrayPtr, countSizeT);
                    arrayPtr = IntPtr.Zero;
                }
            }
        }

        // These delegates allow the definition of generic functions with [OUT] parameters
        internal delegate UIntPtr CategoryArrayOut<T>(IntPtr nativeRef, T enumType, out IntPtr array) where T : Enum;
        internal delegate uint ArrayOut(IntPtr nativeRef, out IntPtr array);
        internal delegate UIntPtr ArrayLongOut(IntPtr nativeRef, out IntPtr array);

        /// <summary>
        /// Turns an array of UTF16 C# strings to an array of pointer to UTF8 strings
        /// </summary>
        /// <param name="args"></param>
        /// <returns>Array of pointer you need to release when you're done with Marshal.FreeHGlobal</returns>
        internal static IntPtr[] ToUtf8(this string[]? args)
        {
            var utf8Args = new IntPtr[args?.Length ?? 0];

            for (var i = 0; i < utf8Args.Length; i++)
            {
                utf8Args[i] = args![i].ToUtf8();
            }

            return utf8Args;
        }

        /// <summary>
        /// Marshal a pointer to a struct
        /// Helper with netstandard1.1 and net40 support
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ptr"></param>
        /// <returns></returns>
        internal static T PtrToStructure<T>(IntPtr ptr)
        {
#if NETSTANDARD1_1 || NET40
            return (T)Marshal.PtrToStructure(ptr, typeof(T));
#else
            return Marshal.PtrToStructure<T>(ptr)!;
#endif
        }

#if DESKTOP
        /// <summary>
        /// Crossplatform dlopen
        /// </summary>
        /// <returns>true if successful</returns>
        internal static bool Open(string filename, out IntPtr fileHandle)
        {
            fileHandle = IntPtr.Zero;
#if NET40
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    fileHandle = Native.fopenMac(filename);
                    return fileHandle != IntPtr.Zero;
                case PlatformID.Unix:
                    fileHandle = Native.fopenLinux(filename);
                    return fileHandle != IntPtr.Zero;
                default:
                    return Native._wfopen_s(out fileHandle, filename) == 0;
            }
#else
            if (PlatformHelper.IsWindows)
            {
                if(Native._wfopen_s(out fileHandle, filename) != 0) return false;
            }
            else if (PlatformHelper.IsLinux)
            {
                fileHandle = Native.fopenLinux(filename);
            }
            else if (PlatformHelper.IsMac)
            {
                fileHandle = Native.fopenMac(filename);
            }
            return fileHandle != IntPtr.Zero;
#endif
        }

        /// <summary>
        /// Crossplatform fclose
        /// </summary>
        /// <param name="fileHandle"></param>
        /// <returns>true if successful</returns>
        internal static bool Close(IntPtr fileHandle)
        {
#if NET40
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.MacOSX:
                    return Native.fcloseMac(fileHandle) == 0;
                case PlatformID.Unix:
                    return Native.fcloseLinux(fileHandle) == 0;
                default:
                    return Native.fcloseWindows(fileHandle) == 0;
            }
#else
            if (PlatformHelper.IsMac)
            {
                return Native.fcloseMac(fileHandle) == 0;
            }
            else if (PlatformHelper.IsLinux)
            {
                return Native.fcloseLinux(fileHandle) == 0;
            }
            else
            {
                return Native.fcloseWindows(fileHandle) == 0;
            }
#endif
        }

#endif
        /// <summary>
        /// Frees an heap allocation returned by a LibVLC function.
        /// If you know you're using the same underlying C run-time as the LibVLC
        /// implementation, then you can call ANSI C free() directly instead.
        /// </summary>
        /// <param name="ptr">the pointer</param>
        internal static void LibVLCFree(ref IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return;

            Native.LibVLCFree(ptr);
            ptr = IntPtr.Zero;
        }

        /// <summary>
        /// Performs the native call, frees the ptrs and returns the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="interopCall"></param>
        /// <param name="ptrs"></param>
        /// <returns></returns>
        internal static T PerformInteropAndFree<T>(Func<T> interopCall, params IntPtr[] ptrs)
        {
            try
            {
                return interopCall();
            }
            finally
            {
                Free(ptrs);
            }
        }

        /// <summary>
        /// Performs the native call and frees the ptrs
        /// </summary>
        /// <param name="interopCall"></param>
        /// <param name="ptrs"></param>
        internal static void PerformInteropAndFree(Action interopCall, params IntPtr[] ptrs)
        {
            try
            {
                interopCall();
            }
            finally
            {
                Free(ptrs);
            }
        }

        internal static int SizeOf<T>(T structure)
        {
#if NETSTANDARD1_1 || NET40
            return Marshal.SizeOf(typeof(T));
#else
            return Marshal.SizeOf<T>(structure);
#endif
        }

        /// <summary>
        /// Gets the instance represented by the given handle.
        /// This must be a GCHandle.
        /// </summary>
        /// <typeparam name="T">The type of instance to retrieve</typeparam>
        /// <param name="handle">The handle given back by libvlc</param>
        /// <returns>null if it is not a valid handle, the instance otherwise</returns>
        internal static T GetInstance<T>(IntPtr handle) where T : class
        {
            if (handle == IntPtr.Zero)
            {
                return default!;
            }

            var gch = GCHandle.FromIntPtr(handle);
            if (!gch.IsAllocated || !(gch.Target is T instance))
                return default!;

            return instance;
        }

        private static void Free(params IntPtr[] ptrs)
        {
            foreach (var ptr in ptrs)
                Marshal.FreeHGlobal(ptr);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct VaListLinuxX64
        {
            uint gp_offset;
            uint fp_offset;
            IntPtr overflow_arg_area;
            IntPtr reg_save_area;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type type)
        {
            Type = type;
        }
        public Type Type { get; private set; }
    }
}
