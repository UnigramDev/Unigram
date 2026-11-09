//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using WinRT;

namespace Telegram.Common
{
    /// <summary>
    /// The composition atlas a bitmap's surface is packed into. An atlas page is freed only once
    /// every surface on it is gone, so bitmaps that come and go together share a group, and a
    /// long-lived one never pins a page full of dead ones from another burst.
    /// </summary>
    public enum AtlasGroup : uint
    {
        /// <summary>XAML's shared atlas, where everything not grouped lands.</summary>
        Default,
        AnimatedImage,
        Stickers,
        ProfilePicture,
        EmojiStatus,
    }

    public static unsafe class AtlasGroupExtensions
    {
        private static readonly Guid IID_IBitmapSourceAtlasGroup = new("c81653bf-fd64-43aa-bfb4-5006d578301f");

        /// <summary>
        /// Takes effect for the surfaces allocated after it, so it must come before the source is
        /// set or the first pixels are written.
        /// </summary>
        /// <remarks>
        /// IBitmapSourceAtlasGroup only exists in our build of Microsoft.UI.Xaml.dll, IUnknown-based
        /// with SetAtlasGroup at slot 3. On any other build the QI fails and this does nothing.
        /// </remarks>
        public static void SetAtlasGroup(this BitmapSource source, AtlasGroup group)
        {
            if (group == AtlasGroup.Default || Marshal.QueryInterface(((IWinRTObject)source).NativeObject.ThisPtr, in IID_IBitmapSourceAtlasGroup, out var groupSource) < 0)
            {
                return;
            }

            // Only fails off the source's thread, and a bitmap that keeps the default atlas still works.
            ((delegate* unmanaged[Stdcall]<IntPtr, uint, int>)(*(IntPtr**)groupSource)[3])(groupSource, (uint)group);
            Marshal.Release(groupSource);
        }

        private static readonly Guid IID_IWindowPrivate = new("f5def8f6-95c2-55cb-b1a4-551b9da34220");

        /// <summary>
        /// Holds atlas pages at about 4/3 of <paramref name="size"/> a side for every surface
        /// factory on XAML's device. Left alone, XAML raises the hint to the primary monitor's
        /// size the first time an image doesn't fit the default atlas, and never lowers it, and
        /// from then on every atlas group costs at least one page that size. Larger surfaces get
        /// a texture of their own instead.
        /// </summary>
        /// <remarks>
        /// IWindowPrivate.SetAtlasSizeHint, slot 11. The hint belongs to the thread's XAML core,
        /// so one window is enough. Stock Microsoft.UI.Xaml.dll returns E_NOTIMPL for desktop
        /// windows, and then nothing changes.
        /// </remarks>
        public static void SetAtlasSizeHint(this Window window, uint size)
        {
            if (Marshal.QueryInterface(((IWinRTObject)window).NativeObject.ThisPtr, in IID_IWindowPrivate, out var windowPrivate) < 0)
            {
                return;
            }

            ((delegate* unmanaged[Stdcall]<IntPtr, uint, uint, int>)(*(IntPtr**)windowPrivate)[11])(windowPrivate, size, size);
            Marshal.Release(windowPrivate);
        }
    }
}
