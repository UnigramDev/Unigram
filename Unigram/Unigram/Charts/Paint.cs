using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;

namespace Unigram.Charts
{
    public class Paint
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
