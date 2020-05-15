using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls
{
    public class StorageChart : Control
    {
        private const float MIN_TRIM = 0.0001f;
        private const float PAD_TRIM = 0.03f;

        private const float THICKNESS = 6;

        private ShapeVisual _visual;

        private float[] _values;
        private bool[] _visible;

        private CompositionEllipseGeometry[] _geometries;
        private CompositionShape[] _shapes;

        public StorageChart()
        {
            DefaultStyleKey = typeof(StorageChart);

            _visual = Window.Current.Compositor.CreateShapeVisual();
            ElementCompositionPreview.SetElementChildVisual(this, _visual);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _visual.Size = finalSize.ToVector2();

            var width = Math.Max(THICKNESS, Math.Min((float)finalSize.Width, (float)finalSize.Height));

            foreach (var ellipse in _geometries)
            {
                ellipse.Radius = new Vector2((width / 2) - (THICKNESS / 2));
                ellipse.Center = new Vector2(width / 2);
            }

            return base.ArrangeOverride(finalSize);
        }

        private IList<StorageChartItem> _items;
        public IList<StorageChartItem> Items
        {
            get => _items;
            set => SetItems(value);
        }

        private void SetItems(IList<StorageChartItem> items)
        {
            _items = items;
            _visual.Shapes.Clear();

            _values = items.Select(x => (float)x.Size).ToArray();
            _visible = new bool[_values.Length];

            _geometries = new CompositionEllipseGeometry[_values.Length];
            _shapes = new CompositionShape[_values.Length];

            Array.Fill(_visible, true);

            var width = Math.Max(THICKNESS, Math.Min((float)ActualWidth, (float)ActualHeight));

            for (int i = 0; i < _values.Length; i++)
            {
                var ellipse = Window.Current.Compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2((width / 2) - (THICKNESS / 2));
                ellipse.Center = new Vector2(width / 2);

                var shape = Window.Current.Compositor.CreateSpriteShape(ellipse);
                shape.StrokeBrush = Window.Current.Compositor.CreateColorBrush(items[i].Stroke);
                shape.StrokeThickness = THICKNESS;
                shape.StrokeStartCap = CompositionStrokeCap.Round;
                shape.StrokeEndCap = CompositionStrokeCap.Round;

                _geometries[i] = ellipse;
                _shapes[i] = shape;

                _visual.Shapes.Add(shape);
            }

            Update(0, true);
        }

        public void Update(int index, bool v)
        {
            var (previous, previousOne) = Snapshot();

            _visible[index] = v;

            _visual.Shapes.Remove(_shapes[index]);
            _visual.Shapes.Insert(0, _shapes[index]);

            var (current, currentOne) = Snapshot();

            previous = Shrink(previous);
            current = Shrink(current);

            var previousStart = 0f;
            var currentStart = 0f;

            for (int i = 0; i < _values.Length; i++)
            {
                var previousValue = previous[i] / previous.Sum();
                var currentValue = current[i] / current.Sum();

                var previousOffset = previousStart;
                var previousEnd = previousValue - PAD_TRIM;

                var currentOffset = currentStart;
                var currentEnd = currentValue - PAD_TRIM;

                if (currentOne == i)
                {
                    currentOffset = 0;
                    currentEnd = 1;
                }
                else if (previousOne == i)
                {
                    previousOffset = 0;
                    previousEnd = 1;
                }
                else if (current[i] == 0)
                {
                    currentOffset = currentStart - PAD_TRIM;
                    currentEnd = 0;
                }
                else if (previous[i] == 0)
                {
                    previousOffset = previousStart - PAD_TRIM;
                    previousEnd = 0;
                }

                var trimStart = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                trimStart.InsertKeyFrame(0, previousOffset);
                trimStart.InsertKeyFrame(1, currentOffset);

                var trimEnd = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                trimEnd.InsertKeyFrame(0, previousEnd);
                trimEnd.InsertKeyFrame(1, currentEnd);

                previousStart += previousValue;
                currentStart += currentValue;

                _geometries[i].StartAnimation("TrimOffset", trimStart);
                _geometries[i].StartAnimation("TrimEnd", trimEnd);
            }
        }

        private float[] Shrink(float[] p)
        {
            var sum = p.Sum();
            var min = MIN_TRIM + PAD_TRIM;

            for (int i = 0; i < p.Length; i++)
            {
                p[i] = p[i] / sum;
            }

            var shrink = 0f;

            for (int i = 0; i < p.Length; i++)
            {
                if (p[i] < min && p[i] > 0)
                {
                    shrink += min - p[i];
                }
            }

            for (int i = 0; i < p.Length; i++)
            {
                if (p[i] > min)
                {
                    p[i] = min + (p[i] - min) * (1 - p.Length * min) / (1 - p.Length * min + shrink);
                }
                else if (p[i] > 0)
                {
                    p[i] = min;
                }
            }

            return p;
        }

        private (float[], int) Snapshot()
        {
            var previous = new float[_values.Length];
            var previousOne = SingleIndex(_visible);

            for (int i = 0; i < _values.Length; i++)
            {
                previous[i] = _visible[i] ? _values[i] : 0;
            }

            return (previous, previousOne);
        }

        private int SingleIndex(bool[] p)
        {
            var j = Array.IndexOf(p, true);
            var k = Array.LastIndexOf(p, true);

            if (j == k)
            {
                return j;
            }

            return -1;
        }
    }

    public class StorageChartItem
    {
        public string Name { get; set; }

        public long Size { get; set; }

        public Color Stroke { get; set; }

        public bool IsVisible { get; set; } = true;

        protected List<FileType> _types;
        public IList<FileType> Types => _types;

        public StorageChartItem(StorageStatisticsByFileType statistics)
        {
            _types = new List<FileType>(1);
            _types.Add(statistics.FileType);

            Size = statistics.Size;

            switch (statistics.FileType)
            {
                case FileTypePhoto fileTypePhoto:
                    Name = Strings.Resources.LocalPhotoCache;
                    Stroke = Color.FromArgb(0xFF, 0x32, 0x7F, 0xE5);
                    break;
                case FileTypeVideo fileTypeVideo:
                    Name = Strings.Resources.LocalVideoCache;
                    Stroke = Color.FromArgb(0xFF, 0xDE, 0xBA, 0x08);
                    break;
                case FileTypeDocument fileTypeDocument:
                    Name = Strings.Resources.LocalDocumentCache;
                    Stroke = Color.FromArgb(0xFF, 0x61, 0xC7, 0x52);
                    break;
                case FileTypeAudio fileTypeAudio:
                    Name = Strings.Resources.LocalMusicCache;
                    Stroke = Color.FromArgb(0xFF, 0x7F, 0x79, 0xF3);
                    break;
                case FileTypeVideoNote videoNote:
                case FileTypeVoiceNote voiceNote:
                    Name = Strings.Resources.LocalAudioCache;
                    Stroke = Color.FromArgb(0xFF, 0xE0, 0x53, 0x56);
                    break;
                case FileTypeSticker fileTypeSticker:
                    Name = Strings.Resources.AccDescrStickers;
                    Stroke = Color.FromArgb(0xFF, 0x8F, 0xCF, 0x39);
                    break;
                default:
                    Name = Strings.Resources.LocalCache;
                    Stroke = Color.FromArgb(0xFF, 0x58, 0xA8, 0xED);
                    break;
            }
        }

        public StorageChartItem Add(StorageStatisticsByFileType statistics)
        {
            _types.Add(statistics.FileType);
            Size += statistics.Size;
            return this;
        }
    }
}
