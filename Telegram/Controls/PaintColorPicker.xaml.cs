//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Globalization;
using System.Numerics;
using Telegram.Common;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    /// <summary>
    /// The media editor's full colour picker: a grid of shades, a spectrum and RGB sliders, all
    /// over one opacity strip.
    /// </summary>
    public sealed partial class PaintColorPicker : Grid
    {
        private const int Columns = 12;
        private const int Rows = 10;

        // The grid: a grey ramp, then each hue darkened towards black above its middle row and
        // lightened towards white below it.
        private static readonly Color[] _gridColors = CreateGrid();

        private Color _color = Colors.Red;

        // Where the spectrum's knob sits, as fractions; kept rather than derived from the colour,
        // which cannot tell which side of the spectrum a grey came from.
        private Point _spectrum = new(0.5, 0);

        // Set while the picker writes its own text boxes, so their change handlers stay out.
        private bool _updating;

        public PaintColorPicker()
        {
            InitializeComponent();
            UpdateControls(null);
        }

        public event EventHandler ColorChanged;
        public event EventHandler EyedropperRequested;
        public event EventHandler Completed;

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                _spectrum = FromSpectrumColor(value, _spectrum);
                UpdateControls(null);
            }
        }

        private void SetColor(Color color, object source)
        {
            if (color == _color)
            {
                return;
            }

            _color = color;

            if (source != SpectrumPanel)
            {
                _spectrum = FromSpectrumColor(color, _spectrum);
            }

            UpdateControls(source);
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateControls(object source)
        {
            var opaque = Color.FromArgb(255, _color.R, _color.G, _color.B);

            GridCanvas.Invalidate();
            AlphaCanvas.Invalidate();

            SpectrumKnobColor.Fill = new SolidColorBrush(opaque);
            PlaceSpectrumKnob();

            UpdateTrack(RedTrack, RedKnob, Color.FromArgb(255, 0, _color.G, _color.B), Color.FromArgb(255, 255, _color.G, _color.B), _color.R);
            UpdateTrack(GreenTrack, GreenKnob, Color.FromArgb(255, _color.R, 0, _color.B), Color.FromArgb(255, _color.R, 255, _color.B), _color.G);
            UpdateTrack(BlueTrack, BlueKnob, Color.FromArgb(255, _color.R, _color.G, 0), Color.FromArgb(255, _color.R, _color.G, 255), _color.B);

            UpdateText(source);
        }

        private void UpdateText(object source)
        {
            _updating = true;

            if (source != RedValue)
            {
                RedValue.Text = _color.R.ToString(CultureInfo.InvariantCulture);
            }

            if (source != GreenValue)
            {
                GreenValue.Text = _color.G.ToString(CultureInfo.InvariantCulture);
            }

            if (source != BlueValue)
            {
                BlueValue.Text = _color.B.ToString(CultureInfo.InvariantCulture);
            }

            if (source != HexValue)
            {
                HexValue.Text = _color.A == 255
                    ? string.Format("{0:X2}{1:X2}{2:X2}", _color.R, _color.G, _color.B)
                    : string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", _color.R, _color.G, _color.B, _color.A);
            }

            _updating = false;
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var index = Navigation.SelectedIndex;

            GridCanvas.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            SpectrumPanel.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            SlidersPanel.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Eyedropper_Click(object sender, RoutedEventArgs e)
        {
            EyedropperRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        private void Surface_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        }

        // The knobs are placed by size, which is unknown until a tab is first shown.
        private void Panel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateControls(null);
        }

        #region Grid

        private static Color[] CreateGrid()
        {
            var hues = new uint[]
            {
                0xFF00A1D8, 0xFF0060FD, 0xFF4C1FB7, 0xFF982BBC, 0xFFB82C5D, 0xFFFD3E12,
                0xFFFF6900, 0xFFFDAB00, 0xFFFCC700, 0xFFFCFA43, 0xFFD9EB37, 0xFF76BA3F
            };

            var colors = new Color[Columns * Rows];
            var middle = Rows / 2 + 1;

            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    Color color;

                    if (y == 0)
                    {
                        color = Blend(Colors.White, Colors.Black, (float)x / (Columns - 1));
                    }
                    else if (y < middle)
                    {
                        color = Blend(ColorEx.FromHex(hues[x]), Colors.Black, (middle - y - 1) / (Rows / 2f - 1) * 0.5f);
                    }
                    else
                    {
                        color = Blend(ColorEx.FromHex(hues[x]), Colors.White, 0.5f - (Rows - y - 1) / (Rows / 2f) * 0.5f);
                    }

                    colors[y * Columns + x] = color;
                }
            }

            return colors;
        }

        private static Color Blend(Color from, Color to, float ratio)
        {
            return Color.FromArgb(255,
                (byte)Math.Round(from.R + (to.R - from.R) * ratio),
                (byte)Math.Round(from.G + (to.G - from.G) * ratio),
                (byte)Math.Round(from.B + (to.B - from.B) * ratio));
        }

        private void GridCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var size = sender.Size.ToVector2();
            var cell = new Vector2(size.X / Columns, size.Y / Rows);
            var selected = -1;

            using (var clip = CanvasGeometry.CreateRoundedRectangle(sender, 0, 0, size.X, size.Y, 10, 10))
            using (args.DrawingSession.CreateLayer(1, clip))
            {
                for (int i = 0; i < _gridColors.Length; i++)
                {
                    var color = _gridColors[i];
                    args.DrawingSession.FillRectangle(i % Columns * cell.X, i / Columns * cell.Y, cell.X + 0.5f, cell.Y + 0.5f, color);

                    if (color.R == _color.R && color.G == _color.G && color.B == _color.B)
                    {
                        selected = i;
                    }
                }
            }

            if (selected != -1)
            {
                var color = _gridColors[selected];
                var brightness = (color.R * 0.2126 + color.G * 0.7152 + color.B * 0.0722) / 255;

                args.DrawingSession.DrawRectangle(selected % Columns * cell.X + 1.5f, selected / Columns * cell.Y + 1.5f, cell.X - 3, cell.Y - 3,
                    brightness > 0.721 ? Color.FromArgb(255, 0x11, 0x11, 0x11) : Colors.White, 3);
            }
        }

        private void GridCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            GridCanvas.CapturePointer(e.Pointer);
            PickGrid(e);
        }

        private void GridCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact)
            {
                PickGrid(e);
            }
        }

        private void PickGrid(PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(GridCanvas).Position;
            var x = Math.Clamp((int)(point.X / GridCanvas.ActualWidth * Columns), 0, Columns - 1);
            var y = Math.Clamp((int)(point.Y / GridCanvas.ActualHeight * Rows), 0, Rows - 1);

            var color = _gridColors[y * Columns + x];
            SetColor(Color.FromArgb(_color.A, color.R, color.G, color.B), GridCanvas);
        }

        #endregion

        #region Spectrum

        // Hue runs down the spectrum; the outer fifths fade to white on the left and black on
        // the right, and the middle is the pure hue.
        private static Color ToSpectrumColor(Point position, byte alpha)
        {
            double saturation = 1;
            double value = 1;

            if (position.X <= 0.22)
            {
                saturation = position.X / 0.22;
            }
            else if (position.X >= 0.78)
            {
                saturation = value = 1 - (position.X - 0.78) / 0.22;
            }

            return new HSV(Math.Clamp(position.Y, 0, 1) * 360, Math.Clamp(saturation, 0, 1), Math.Clamp(value, 0, 1)).ToRGB(alpha);
        }

        private static Point FromSpectrumColor(Color color, Point previous)
        {
            var hsv = color.ToHSV();
            var y = hsv.S < 0.01 ? previous.Y : hsv.H / 360;

            if (hsv.V < 0.99)
            {
                return new Point(0.78 + (1 - hsv.V) * 0.22, y);
            }
            else if (hsv.S < 0.99)
            {
                return new Point(hsv.S * 0.22, y);
            }

            return new Point(0.5, y);
        }

        private void PlaceSpectrumKnob()
        {
            var width = SpectrumPanel.ActualWidth;
            var height = SpectrumPanel.ActualHeight;

            if (width == 0)
            {
                width = 336;
                height = 280;
            }

            var x = Math.Clamp(_spectrum.X * width, 16, width - 16);
            var y = Math.Clamp(_spectrum.Y * height, 16, height - 16);

            Canvas.SetLeft(SpectrumKnob, x - 13);
            Canvas.SetTop(SpectrumKnob, y - 13);
        }

        private void Spectrum_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            SpectrumPanel.CapturePointer(e.Pointer);
            PickSpectrum(e);
        }

        private void Spectrum_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact)
            {
                PickSpectrum(e);
            }
        }

        private void PickSpectrum(PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(SpectrumPanel).Position;

            _spectrum = new Point(
                Math.Clamp(point.X / SpectrumPanel.ActualWidth, 0, 1),
                Math.Clamp(point.Y / SpectrumPanel.ActualHeight, 0, 1));

            SetColor(ToSpectrumColor(_spectrum, _color.A), SpectrumPanel);
            PlaceSpectrumKnob();
        }

        #endregion

        #region Sliders

        private static void UpdateTrack(Grid track, Ellipse knob, Color from, Color to, byte value)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };

            brush.GradientStops.Add(new GradientStop { Color = from, Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = to, Offset = 1 });

            track.Background = brush;
            knob.Fill = new SolidColorBrush(Color.FromArgb(255,
                (byte)(from.R + (to.R - from.R) * value / 255),
                (byte)(from.G + (to.G - from.G) * value / 255),
                (byte)(from.B + (to.B - from.B) * value / 255)));

            var width = track.ActualWidth > 0 ? track.ActualWidth : 264;
            Canvas.SetLeft(knob, 3 + (width - 32) * value / 255);
        }

        private void Track_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);
            PickTrack((Grid)sender, e);
        }

        private void Track_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact)
            {
                PickTrack((Grid)sender, e);
            }
        }

        private void PickTrack(Grid track, PointerRoutedEventArgs e)
        {
            var x = e.GetCurrentPoint(track).Position.X;
            var value = (byte)Math.Round(Math.Clamp((x - 16) / (track.ActualWidth - 32), 0, 1) * 255);

            var color = track == RedTrack ? Color.FromArgb(_color.A, value, _color.G, _color.B)
                : track == GreenTrack ? Color.FromArgb(_color.A, _color.R, value, _color.B)
                : Color.FromArgb(_color.A, _color.R, _color.G, value);

            SetColor(color, track);
        }

        private void Value_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updating || !byte.TryParse(((TextBox)sender).Text, NumberStyles.None, CultureInfo.InvariantCulture, out byte value))
            {
                return;
            }

            var color = sender == RedValue ? Color.FromArgb(_color.A, value, _color.G, _color.B)
                : sender == GreenValue ? Color.FromArgb(_color.A, _color.R, value, _color.B)
                : Color.FromArgb(_color.A, _color.R, _color.G, value);

            SetColor(color, sender);
        }

        // Empty is let through so a value can be retyped; losing focus puts the colour's back.
        private void Value_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            var text = args.NewText;
            if (text.Length > 0 && !byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                args.Cancel = true;
            }
        }

        // A leading '#' is accepted so a pasted code goes in whole.
        private void Hex_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            var text = args.NewText;
            var start = text.Length > 0 && text[0] == '#' ? 1 : 0;

            if (text.Length - start > 8)
            {
                args.Cancel = true;
                return;
            }

            for (int i = start; i < text.Length; i++)
            {
                if (!Uri.IsHexDigit(text[i]))
                {
                    args.Cancel = true;
                    return;
                }
            }
        }

        private void Value_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateText(null);
        }

        // Six digits, or eight with the opacity last.
        private void Hex_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = HexValue.Text.TrimStart('#');

            if (_updating || (text.Length != 6 && text.Length != 8) || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            {
                return;
            }

            var color = text.Length == 6
                ? Color.FromArgb(_color.A, (byte)(value >> 16), (byte)(value >> 8), (byte)value)
                : Color.FromArgb((byte)value, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8));

            SetColor(color, HexValue);
        }

        #endregion

        #region Opacity

        private void AlphaCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var size = sender.Size.ToVector2();
            var session = args.DrawingSession;

            using (var clip = CanvasGeometry.CreateRoundedRectangle(sender, 0, 0, size.X, size.Y, size.Y / 2, size.Y / 2))
            using (session.CreateLayer(1, clip))
            {
                const float Square = 6;
                var light = Color.FromArgb(255, 0xFF, 0xFF, 0xFF);
                var dark = Color.FromArgb(255, 0xCC, 0xCC, 0xCC);

                for (int x = 0; x * Square < size.X; x++)
                {
                    for (int y = 0; y * Square < size.Y; y++)
                    {
                        session.FillRectangle(x * Square, y * Square, Square, Square, (x + y) % 2 == 0 ? light : dark);
                    }
                }

                using var gradient = new CanvasLinearGradientBrush(sender, Color.FromArgb(0, _color.R, _color.G, _color.B), Color.FromArgb(255, _color.R, _color.G, _color.B))
                {
                    StartPoint = Vector2.Zero,
                    EndPoint = new Vector2(size.X, 0)
                };

                session.FillRectangle(0, 0, size.X, size.Y, gradient);
            }

            var radius = size.Y / 2 - 3;
            var center = new Vector2(radius + 3 + (size.X - 2 * (radius + 3)) * _color.A / 255f, size.Y / 2);

            session.FillCircle(center, radius, _color);
            session.DrawCircle(center, radius, Colors.White, 3);
        }

        private void AlphaCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            AlphaCanvas.CapturePointer(e.Pointer);
            PickAlpha(e);
        }

        private void AlphaCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact)
            {
                PickAlpha(e);
            }
        }

        private void PickAlpha(PointerRoutedEventArgs e)
        {
            var width = AlphaCanvas.ActualWidth;
            var height = AlphaCanvas.ActualHeight;
            var x = e.GetCurrentPoint(AlphaCanvas).Position.X;

            var alpha = (byte)Math.Round(Math.Clamp((x - height / 2) / (width - height), 0, 1) * 255);
            SetColor(Color.FromArgb(alpha, _color.R, _color.G, _color.B), AlphaCanvas);
        }

        #endregion
    }
}
