//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;

namespace Telegram.Benchmarks.Json
{
    /// <summary>
    /// A payload placed so its last byte sits against a PAGE_NOACCESS page. Reading one byte past
    /// the end access-violates instead of quietly returning whatever was next in the heap, which is
    /// the only way to prove a pointer-based reader stays inside its buffer.
    ///
    /// Desktop only: VirtualAlloc isn't in the app container API set, and one host proving the
    /// bounds is enough - the code under test is the same everywhere.
    /// </summary>
    internal sealed unsafe class GuardedBuffer : IDisposable
    {
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_NOACCESS = 0x01;

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);

        private IntPtr _region;
        private readonly int _dataSize;

        /// <summary>
        /// One region per payload, reused for every prefix - reserving a fresh one per prefix
        /// exhausts the address space long before the sweep finishes.
        /// </summary>
        public GuardedBuffer(int capacity)
        {
            const int PageSize = 4096;

            // +1 for the terminator, which has to be inside the committed region.
            var pages = (capacity + 1 + PageSize - 1) / PageSize;
            _dataSize = Math.Max(pages, 1) * PageSize;

            // Reserve the data pages plus one more, and commit only the data pages. The extra page
            // stays reserved-but-not-committed, which faults on any access.
            _region = VirtualAlloc(IntPtr.Zero, (UIntPtr)(_dataSize + PageSize), MEM_RESERVE, PAGE_NOACCESS);
            if (_region == IntPtr.Zero)
            {
                throw new OutOfMemoryException($"VirtualAlloc reserve failed: {Marshal.GetLastWin32Error()}");
            }

            if (VirtualAlloc(_region, (UIntPtr)_dataSize, MEM_COMMIT, PAGE_READWRITE) == IntPtr.Zero)
            {
                throw new OutOfMemoryException($"VirtualAlloc commit failed: {Marshal.GetLastWin32Error()}");
            }
        }

        /// <summary>
        /// Copies the first <paramref name="length"/> bytes followed by the terminator TdJsonReader
        /// requires, positioned so that the terminator is the last committed byte. Reading even one
        /// byte beyond it hits the guard page.
        /// </summary>
        public byte* Place(byte[] payload, int length)
        {
            var pointer = (byte*)_region + (_dataSize - length - 1);
            Marshal.Copy(payload, 0, (IntPtr)pointer, length);
            pointer[length] = 0;
            return pointer;
        }

        public void Dispose()
        {
            if (_region != IntPtr.Zero)
            {
                VirtualFree(_region, UIntPtr.Zero, MEM_RELEASE);
                _region = IntPtr.Zero;
            }
        }
    }
}
