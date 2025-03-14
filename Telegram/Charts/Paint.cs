//
// Copyright Fela Ameghino 2015-2025
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


    }
}
