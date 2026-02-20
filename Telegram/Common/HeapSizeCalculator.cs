//
// Copyright Fela Ameghino & Contributors 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Telegram.Views
{
    public class HeapSizeCalculator
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetProcessHeap();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetProcessHeaps(uint NumberOfHeaps, IntPtr[] ProcessHeaps);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool HeapWalk(IntPtr hHeap, ref PROCESS_HEAP_ENTRY lpEntry);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool HeapLock(IntPtr hHeap);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool HeapUnlock(IntPtr hHeap);

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_HEAP_ENTRY
        {
            public IntPtr lpData;
            public uint cbData;
            public byte cbOverhead;
            public byte iRegionIndex;
            public ushort wFlags;
            public uint dwCommittedSize;
            public uint dwUnCommittedSize;
            public IntPtr lpFirstBlock;
            public IntPtr lpLastBlock;
        }

        public enum HEAP_INFORMATION_CLASS
        {
            HeapCompatibilityInformation = 0,
            HeapEnableTerminationOnCorruption = 1,
            HeapOptimizeResources = 3
        }

        // PROCESS_HEAP_ENTRY flags
        public const ushort PROCESS_HEAP_REGION = 0x0001;
        public const ushort PROCESS_HEAP_UNCOMMITTED_RANGE = 0x0002;
        public const ushort PROCESS_HEAP_ENTRY_BUSY = 0x0004;
        public const ushort PROCESS_HEAP_ENTRY_MOVEABLE = 0x0010;
        public const ushort PROCESS_HEAP_ENTRY_DDESHARE = 0x0020;

        // Error codes
        public const uint ERROR_NO_MORE_ITEMS = 259;

        public class HeapInfo
        {
            public IntPtr HeapHandle { get; set; }
            public ulong TotalSize { get; set; }
            public ulong CommittedSize { get; set; }
            public ulong UncommittedSize { get; set; }
            public ulong AllocatedSize { get; set; }
            public ulong FreeSize { get; set; }
            public uint BlockCount { get; set; }
            public uint FreeBlockCount { get; set; }
        }

        public class ProcessHeapSummary
        {
            public List<HeapInfo> Heaps { get; set; } = new List<HeapInfo>();
            public ulong TotalHeapSize { get; set; }
            public ulong TotalAllocatedSize { get; set; }
            public ulong TotalCommittedSize { get; set; }
            public ulong ManagedHeapSize { get; set; }
            public int HeapCount { get; set; }
        }

        /// <summary>
        /// Gets comprehensive heap information for the current process
        /// </summary>
        public static ProcessHeapSummary GetProcessHeapInfo(bool defaultOnly)
        {
            var summary = new ProcessHeapSummary();

            try
            {
                // Get managed heap size first
                summary.ManagedHeapSize = (ulong)GC.GetTotalMemory(false);

                // Get all process heaps
                IntPtr[] heaps = GetAllProcessHeaps(defaultOnly);

                summary.HeapCount = heaps.Length;

                foreach (var heapHandle in heaps)
                {
                    var heapInfo = AnalyzeHeap(heapHandle);
                    if (heapInfo != null)
                    {
                        summary.Heaps.Add(heapInfo);
                        summary.TotalHeapSize += heapInfo.TotalSize;
                        summary.TotalAllocatedSize += heapInfo.AllocatedSize;
                        summary.TotalCommittedSize += heapInfo.CommittedSize;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing heaps: {ex.Message}");
            }

            return summary;
        }

        /// <summary>
        /// Gets all heap handles for the current process
        /// </summary>
        private static IntPtr[] GetAllProcessHeaps(bool defaultOnly)
        {
            if (defaultOnly)
            {
                return new[] { GetProcessHeap() };
            }

            // First call to get the number of heaps
            uint heapCount = GetProcessHeaps(0, null);
            if (heapCount == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            // Second call to get the actual heap handles
            IntPtr[] heaps = new IntPtr[heapCount];
            uint actualCount = GetProcessHeaps(heapCount, heaps);

            if (actualCount == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            // Resize array if needed
            if (actualCount != heapCount)
            {
                Array.Resize(ref heaps, (int)actualCount);
            }

            return heaps;
        }

        /// <summary>
        /// Analyzes a single heap and returns detailed information
        /// </summary>
        private static HeapInfo AnalyzeHeap(IntPtr heapHandle)
        {
            var heapInfo = new HeapInfo
            {
                HeapHandle = heapHandle,
            };

            try
            {
                // Lock the heap for walking
                if (!HeapLock(heapHandle))
                {
                    Console.WriteLine($"Failed to lock heap {heapHandle:X}: {Marshal.GetLastWin32Error()}");
                    return heapInfo;
                }

                try
                {
                    PROCESS_HEAP_ENTRY entry = new PROCESS_HEAP_ENTRY();
                    entry.lpData = IntPtr.Zero;

                    // Walk through all heap entries
                    while (HeapWalk(heapHandle, ref entry))
                    {
                        heapInfo.BlockCount++;

                        if ((entry.wFlags & PROCESS_HEAP_REGION) != 0)
                        {
                            // This is a region entry
                            heapInfo.CommittedSize += entry.dwCommittedSize;
                            heapInfo.UncommittedSize += entry.dwUnCommittedSize;
                            heapInfo.TotalSize += entry.dwCommittedSize + entry.dwUnCommittedSize;
                        }
                        else if ((entry.wFlags & PROCESS_HEAP_UNCOMMITTED_RANGE) == 0)
                        {
                            // This is a committed block
                            uint blockSize = entry.cbData + entry.cbOverhead;

                            if ((entry.wFlags & PROCESS_HEAP_ENTRY_BUSY) != 0)
                            {
                                // Allocated block
                                heapInfo.AllocatedSize += blockSize;
                            }
                            else
                            {
                                // Free block
                                heapInfo.FreeSize += blockSize;
                                heapInfo.FreeBlockCount++;
                            }
                        }
                    }

                    // Check if the walk ended due to reaching the end or an error
                    uint lastError = (uint)Marshal.GetLastWin32Error();
                    if (lastError != ERROR_NO_MORE_ITEMS)
                    {
                        Console.WriteLine($"HeapWalk error for heap {heapHandle:X}: {lastError}");
                    }
                }
                finally
                {
                    HeapUnlock(heapHandle);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing heap {heapHandle:X}: {ex.Message}");
            }

            return heapInfo;
        }

        /// <summary>
        /// Gets just the total native heap size (simplified version)
        /// </summary>
        public static ulong GetTotalNativeHeapSize(bool defaultOnly)
        {
            try
            {
                var summary = GetProcessHeapInfo(defaultOnly);
                return summary.TotalAllocatedSize;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets both managed and native heap sizes
        /// </summary>
        public static (ulong ManagedHeap, ulong NativeHeap) GetHeapSizes(bool defaultOnly)
        {
            var summary = GetProcessHeapInfo(defaultOnly);
            return (summary.ManagedHeapSize, summary.TotalAllocatedSize);
        }
    }
}
