//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Telegram.Common
{
    public struct ModuleInfo
    {
        public string Name;
        public IntPtr BaseAddress;
        public uint Size;
    }

    public struct ThreadStackSnapshot
    {
        public uint ThreadId;
        public List<IntPtr> Frames;
    }

    public struct DeadlockSnapshot
    {
        public List<ThreadStackSnapshot> Threads;
        public HashSet<ModuleInfo> Modules;
    }

    public static class StackCapture
    {
        #region P/Invoke Declarations

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        private delegate bool Module32FirstDelegate(IntPtr hSnapshot, ref MODULEENTRY32 lpme);
        private static readonly Lazy<Module32FirstDelegate> Module32First = new(() => NativeMethodInvoker.GetNativeMethod<Module32FirstDelegate>("kernel32.dll", "Module32First"));

        private delegate bool Module32NextDelegate(IntPtr hSnapshot, ref MODULEENTRY32 lpme);
        private static readonly Lazy<Module32NextDelegate> Module32Next = new(() => NativeMethodInvoker.GetNativeMethod<Module32NextDelegate>("kernel32.dll", "Module32Next"));

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT lpContext);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetCurrentProcess();

        private delegate bool SymInitializeDelegate(IntPtr hProcess, string UserSearchPath, bool fInvadeProcess);
        private static readonly Lazy<SymInitializeDelegate> SymInitialize = new(() => NativeMethodInvoker.GetNativeMethod<SymInitializeDelegate>("dbghelp.dll", "SymInitialize"));

        private delegate bool SymCleanupDelegate(IntPtr hProcess);
        private static readonly Lazy<SymCleanupDelegate> SymCleanup = new(() => NativeMethodInvoker.GetNativeMethod<SymCleanupDelegate>("dbghelp.dll", "SymCleanup"));

        private delegate bool StackWalk64Delegate(
            uint MachineType,
            IntPtr hProcess,
            IntPtr hThread,
            ref STACKFRAME64 StackFrame,
            ref CONTEXT64 ContextRecord,
            IntPtr ReadMemoryRoutine,
            IntPtr FunctionTableAccessRoutine,
            IntPtr GetModuleBaseRoutine,
            IntPtr TranslateAddress);
        private static readonly Lazy<StackWalk64Delegate> StackWalk64 = new(() => NativeMethodInvoker.GetNativeMethod<StackWalk64Delegate>("dbghelp.dll", "StackWalk64"));

        private delegate IntPtr SymFunctionTableAccess64Delegate(IntPtr hProcess, ulong AddrBase);
        private static readonly Lazy<SymFunctionTableAccess64Delegate> SymFunctionTableAccess64 = new(() => NativeMethodInvoker.GetNativeMethod<SymFunctionTableAccess64Delegate>("dbghelp.dll", "SymFunctionTableAccess64"));

        private delegate ulong SymGetModuleBase64Delegate(IntPtr hProcess, ulong qwAddr);
        private static readonly Lazy<SymGetModuleBase64Delegate> SymGetModuleBase64 = new(() => NativeMethodInvoker.GetNativeMethod<SymGetModuleBase64Delegate>("dbghelp.dll", "SymGetModuleBase64"));

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        private const uint STILL_ACTIVE = 259;

        private const uint TH32CS_SNAPTHREAD = 0x00000004;
        private const uint TH32CS_SNAPMODULE = 0x00000008;
        private const uint TH32CS_SNAPMODULE32 = 0x00000010;
        private const uint THREAD_ALL_ACCESS = 0x001FFFFF;
        private const uint CONTEXT_FULL = 0x00010007;
        private const uint IMAGE_FILE_MACHINE_AMD64 = 0x8664;
        private const uint IMAGE_FILE_MACHINE_I386 = 0x014c;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct THREADENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ThreadID;
            public uint th32OwnerProcessID;
            public int tpBasePri;
            public int tpDeltaPri;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MODULEENTRY32
        {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            public IntPtr modBaseAddr;
            public uint modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CONTEXT
        {
            public uint ContextFlags;
            public uint Dr0;
            public uint Dr1;
            public uint Dr2;
            public uint Dr3;
            public uint Dr6;
            public uint Dr7;
            public FLOATING_SAVE_AREA FloatSave;
            public uint SegGs;
            public uint SegFs;
            public uint SegEs;
            public uint SegDs;
            public uint Edi;
            public uint Esi;
            public uint Ebx;
            public uint Edx;
            public uint Ecx;
            public uint Eax;
            public uint Ebp;
            public uint Eip;
            public uint SegCs;
            public uint EFlags;
            public uint Esp;
            public uint SegSs;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] ExtendedRegisters;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CONTEXT64
        {
            public ulong P1Home;
            public ulong P2Home;
            public ulong P3Home;
            public ulong P4Home;
            public ulong P5Home;
            public ulong P6Home;
            public uint ContextFlags;
            public uint MxCsr;
            public ushort SegCs;
            public ushort SegDs;
            public ushort SegEs;
            public ushort SegFs;
            public ushort SegGs;
            public ushort SegSs;
            public uint EFlags;
            public ulong Dr0;
            public ulong Dr1;
            public ulong Dr2;
            public ulong Dr3;
            public ulong Dr6;
            public ulong Dr7;
            public ulong Rax;
            public ulong Rcx;
            public ulong Rdx;
            public ulong Rbx;
            public ulong Rsp;
            public ulong Rbp;
            public ulong Rsi;
            public ulong Rdi;
            public ulong R8;
            public ulong R9;
            public ulong R10;
            public ulong R11;
            public ulong R12;
            public ulong R13;
            public ulong R14;
            public ulong R15;
            public ulong Rip;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] FltSave;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 26)]
            public M128A[] VectorRegister;
            public ulong VectorControl;
            public ulong DebugControl;
            public ulong LastBranchToRip;
            public ulong LastBranchFromRip;
            public ulong LastExceptionToRip;
            public ulong LastExceptionFromRip;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct M128A
        {
            public ulong Low;
            public long High;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FLOATING_SAVE_AREA
        {
            public uint ControlWord;
            public uint StatusWord;
            public uint TagWord;
            public uint ErrorOffset;
            public uint ErrorSelector;
            public uint DataOffset;
            public uint DataSelector;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 80)]
            public byte[] RegisterArea;
            public uint Cr0NpxState;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STACKFRAME64
        {
            public ADDRESS64 AddrPC;
            public ADDRESS64 AddrReturn;
            public ADDRESS64 AddrFrame;
            public ADDRESS64 AddrStack;
            public ADDRESS64 AddrBStore;
            public IntPtr FuncTableEntry;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public ulong[] Params;
            public bool Far;
            public bool Virtual;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public ulong[] Reserved;
            public KDHELP64 KdHelp;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ADDRESS64
        {
            public ulong Offset;
            public ushort Segment;
            public ADDRESS_MODE Mode;
        }

        private enum ADDRESS_MODE : uint
        {
            AddrMode1616,
            AddrMode1632,
            AddrModeReal,
            AddrModeFlat
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KDHELP64
        {
            public ulong Thread;
            public uint ThCallbackStack;
            public uint ThCallbackBStore;
            public uint NextCallback;
            public uint FramePointer;
            public ulong KiCallUserMode;
            public ulong KeUserCallbackDispatcher;
            public ulong SystemRangeStart;
            public ulong KiUserExceptionDispatcher;
            public ulong StackBase;
            public ulong StackLimit;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public ulong[] Reserved;
        }

        #endregion

        public static DeadlockSnapshot CaptureAllThreadStacks()
        {
            var snapshot = new DeadlockSnapshot
            {
                Threads = new List<ThreadStackSnapshot>(),
                Modules = new HashSet<ModuleInfo>()
            };

            var modules = new List<ModuleInfo>();

            var process = GetCurrentProcess();
            var currentProcessId = GetCurrentProcessId();
            var currentThreadId = GetCurrentThreadId();
            bool is64Bit = IntPtr.Size == 8;

            // Capture loaded modules
            var moduleSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, currentProcessId);
            if (moduleSnapshot != INVALID_HANDLE_VALUE)
            {
                var me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32)) };

                if (Module32First.Value(moduleSnapshot, ref me))
                {
                    do
                    {
                        modules.Add(new ModuleInfo
                        {
                            Name = me.szModule,
                            BaseAddress = me.modBaseAddr,
                            Size = me.modBaseSize
                        });
                    } while (Module32Next.Value(moduleSnapshot, ref me));
                }
                CloseHandle(moduleSnapshot);
            }

            static ModuleInfo? FindModuleForAddress(List<ModuleInfo> modules, ulong addr)
            {
                foreach (var module in modules)
                {
                    ulong baseAddr = (ulong)module.BaseAddress.ToInt64();
                    ulong endAddr = baseAddr + module.Size;

                    if (addr >= baseAddr && addr < endAddr)
                    {
                        return module;
                    }
                }

                return null;
            }

            // Initialize symbol handler
            SymInitialize.Value(process, null, false);

            // Capture thread stacks
            var threadSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
            if (threadSnapshot == INVALID_HANDLE_VALUE)
            {
                SymCleanup.Value(process);
                return snapshot;
            }

            var te = new THREADENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(THREADENTRY32)) };

            if (Thread32First(threadSnapshot, ref te))
            {
                do
                {
                    if (te.th32OwnerProcessID == currentProcessId)
                    {
                        var ts = new ThreadStackSnapshot
                        {
                            ThreadId = te.th32ThreadID,
                            Frames = new List<IntPtr>()
                        };

                        var thread = OpenThread(THREAD_ALL_ACCESS, false, te.th32ThreadID);
                        if (thread != IntPtr.Zero)
                        {
                            if (!GetExitCodeThread(thread, out uint exitCode) || exitCode != STILL_ACTIVE)
                            {
                                CloseHandle(thread);
                                continue;
                            }

                            // Don't suspend current thread
                            if (te.th32ThreadID != currentThreadId)
                            {
                                SuspendThread(thread);
                            }

                            if (is64Bit)
                            {
                                var context = new CONTEXT64 { ContextFlags = CONTEXT_FULL };
                                if (GetThreadContext(thread, ref context))
                                {
                                    var frame = new STACKFRAME64
                                    {
                                        AddrPC = new ADDRESS64 { Offset = context.Rip, Mode = ADDRESS_MODE.AddrModeFlat },
                                        AddrFrame = new ADDRESS64 { Offset = context.Rbp, Mode = ADDRESS_MODE.AddrModeFlat },
                                        AddrStack = new ADDRESS64 { Offset = context.Rsp, Mode = ADDRESS_MODE.AddrModeFlat }
                                    };

                                    while (StackWalk64.Value(IMAGE_FILE_MACHINE_AMD64, process, thread, ref frame,
                                        ref context, IntPtr.Zero,
                                        Marshal.GetFunctionPointerForDelegate<SymFunctionTableAccess64Delegate>(SymFunctionTableAccess64.Value),
                                        Marshal.GetFunctionPointerForDelegate<SymGetModuleBase64Delegate>(SymGetModuleBase64.Value),
                                        IntPtr.Zero))
                                    {
                                        if (frame.AddrPC.Offset == 0) break;

                                        var module = FindModuleForAddress(modules, frame.AddrPC.Offset);
                                        if (module != null)
                                        {
                                            snapshot.Modules.Add(module.Value);
                                        }

                                        ts.Frames.Add(new IntPtr((long)frame.AddrPC.Offset));

                                        if (ts.Frames.Count > 200) break;
                                    }
                                }
                            }
                            else
                            {
                                //var context = new CONTEXT { ContextFlags = CONTEXT_FULL };
                                //if (GetThreadContext(thread, ref context))
                                //{
                                //    var frame = new STACKFRAME64
                                //    {
                                //        AddrPC = new ADDRESS64 { Offset = context.Eip, Mode = ADDRESS_MODE.AddrModeFlat },
                                //        AddrFrame = new ADDRESS64 { Offset = context.Ebp, Mode = ADDRESS_MODE.AddrModeFlat },
                                //        AddrStack = new ADDRESS64 { Offset = context.Esp, Mode = ADDRESS_MODE.AddrModeFlat }
                                //    };

                                //    while (StackWalk64(IMAGE_FILE_MACHINE_I386, process, thread, ref frame,
                                //        ref context, IntPtr.Zero,
                                //        Marshal.GetFunctionPointerForDelegate<SymFunctionTableAccess64Delegate>(SymFunctionTableAccess64),
                                //        Marshal.GetFunctionPointerForDelegate<SymGetModuleBase64Delegate>(SymGetModuleBase64),
                                //        IntPtr.Zero))
                                //    {
                                //        if (frame.AddrPC.Offset == 0) break;
                                //        ts.Frames.Add(new IntPtr((long)frame.AddrPC.Offset));

                                //        if (ts.Frames.Count > 200) break;
                                //    }
                                //}
                            }

                            if (te.th32ThreadID != currentThreadId)
                            {
                                ResumeThread(thread);
                            }
                            CloseHandle(thread);
                        }

                        if (ts.Frames.Count > 0)
                        {
                            snapshot.Threads.Add(ts);
                        }
                    }
                } while (Thread32Next(threadSnapshot, ref te));
            }

            CloseHandle(threadSnapshot);
            SymCleanup.Value(process);

            return snapshot;
        }

        // Delegates for StackWalk64 callbacks
        //private delegate IntPtr SymFunctionTableAccess64Delegate(IntPtr hProcess, ulong AddrBase);
        //private delegate ulong SymGetModuleBase64Delegate(IntPtr hProcess, ulong qwAddr);

        // 64-bit version of GetThreadContext
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetThreadContext")]
        private static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT64 lpContext);
    }
}
