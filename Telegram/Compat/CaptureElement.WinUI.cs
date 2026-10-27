//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Capture;

namespace Telegram.Compat
{
    /// <summary>
    /// A CaptureElement that captures nothing. WinUI 3 has no equivalent - the camera preview is
    /// built on MediaPlayerElement or a SwapChainPanel there instead - and it is not ported yet,
    /// so the video message recorder gets a hole where its preview is:
    /// the ring, the timer, the shadow and the layout are all real, and the picture is missing.
    ///
    /// A Grid rather than a bare Control so that it lays out and renders like the real one, which
    /// matters at two of the call sites: it is added to a Grid's children, and it is handed to
    /// RenderTargetBitmap for the last frame.
    ///
    /// GlobalUsings.WinUI.cs aliases the name onto this type. Delete both when the preview is really
    /// ported.
    /// </summary>
    public partial class CaptureElement : Grid
    {
        public MediaCapture Source { get; set; }

        public Stretch Stretch { get; set; }
    }
}
