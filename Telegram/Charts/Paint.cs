//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;

namespace Telegram.Charts
{
    public partial class Paint
    {
        private Color _color;
        public Color Color
        {
            get => _color;
            set => _color = value;
        }

        public byte A
        {
            get => _color.A;
            set => _color.A = value;
        }

        public byte R
        {
            get => _color.R;
            set => _color.R = value;
        }

        public byte G
        {
            get => _color.G;
            set => _color.G = value;
        }

        public byte B
        {
            get => _color.B;
            set => _color.B = value;
        }



        public float StrokeWidth { get; set; } = 1;
        public CanvasCapStyle? StrokeCap { get; set; }



        public float? TextSize { get; set; }
        public CanvasHorizontalAlignment? TextAlignment { get; set; }


        // A paint is drawn with on every frame, so the D2D and DWrite objects it needs are kept
        // here rather than built per call. Both are configured by the draw extensions, which only
        // assign a property when it actually changes - Win2D drops the realized native object on
        // every set, so writing the same value back would rebuild it just as often.

        private CanvasTextFormat _textFormat;
        internal CanvasTextFormat TextFormat => _textFormat ??= new CanvasTextFormat();

        private CanvasStrokeStyle _strokeStyle;
        internal CanvasStrokeStyle StrokeStyle => _strokeStyle ??= new CanvasStrokeStyle();

        /// <summary>Releases both. Either is rebuilt by its accessor on next use, so this is safe
        /// on a paint that will be drawn with again.</summary>
        internal void Release()
        {
            _textFormat?.Dispose();
            _textFormat = null;

            _strokeStyle?.Dispose();
            _strokeStyle = null;
        }


    }
}
