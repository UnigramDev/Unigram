//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Telegram.Controls;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Telegram.Views.Wallet.Popups
{
    public sealed partial class WalletSharePopup : ContentPopup
    {
        public WalletSharePopup(string address)
        {
            this.InitializeComponent();

            var geometry = QrCode.CreateGeometry("ton://transfer/" + address, 3, 4, true, 148);
            var visual = ElementComposition.GetElementVisual(Code);
            visual.Clip = visual.Compositor.CreateGeometricClip(visual.Compositor.CreatePathGeometry(geometry.Data));

            RingAddress.Text = FormatAddressBand(address);

            // Culling performs the handover: each face leaves the screen as it turns
            // edge on, with nothing timed against the rotation. Children inherit the
            // setting, so the two roots cover everything either face contains.
            ElementComposition.GetElementVisual(CardFront).BackfaceVisibility = CompositionBackfaceVisibility.Hidden;

            // The faces are stacked, so the back one rests half a turn round and is
            // therefore already culled, leaving only the front on screen.
            var back = ElementComposition.GetElementVisual(CardBack);
            back.BackfaceVisibility = CompositionBackfaceVisibility.Hidden;
            back.RotationAxis = new Vector3(0, 1, 0);
            back.RotationAngleInDegrees = -FlipDegrees;

            // Culling is a compositor concern; XAML hit testing is unaware of it.
            // CardBack is last in tree order, so without this it keeps receiving clicks
            // from behind the front face.
            CardBack.IsHitTestVisible = false;

            for (int i = 0; i < address.Length; i += 4)
            {
                var index = i / 4;
                if (index > 0)
                {
                    CardAddress.Inlines.Add(index % 3 == 0 ? new LineBreak() : new Run { Text = " " });
                }

                CardAddress.Inlines.Add(new Run
                {
                    Text = address.Substring(i, 4),
                    Foreground = new SolidColorBrush(index % 2 == 0 ? Colors.Black : Color.FromArgb(0xFF, 0x7A, 0x7A, 0x7A))
                });
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        // A TON address in its user-friendly form, and the four-character groups
        // it is broken into.
        private const int AddressLength = 48;
        private const int AddressGroup = 4;
        private const int AddressGroups = AddressLength / AddressGroup;

        // U+00B7. Named because a middle dot is hard to distinguish from a period in
        // source, and this method is otherwise all punctuation.
        private const char AddressSeparator = '·';

        // Two passes of "SEPARATOR SPACE groups", joined by a space:
        // 2 * (2 + 48 + 11) + 1.
        private const int AddressBandLength = 2 * (2 + AddressLength + AddressGroups - 1) + 1;

        /// <summary>
        /// Formats a 48 character address as the band engraved along the card:
        /// groups of four, the whole address twice, each pass introduced by a
        /// middle dot.
        ///
        /// <code>· UQAL 1DVI ... IJYI NQYK · UQAL 1DVI ... IJYI NQYK</code>
        ///
        /// Repeated because the band runs the length of the card and one pass leaves
        /// the remainder empty. The dot keeps the seam from reading as part of the
        /// address.
        /// </summary>
        private static string FormatAddressBand(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length != AddressLength)
            {
                return string.Empty;
            }

            // Sized exactly, so the builder never grows: the only allocations are this
            // and the returned string.
            var builder = new StringBuilder(AddressBandLength);

            for (int pass = 0; pass < 2; pass++)
            {
                if (pass > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(AddressSeparator);
                builder.Append(' ');

                for (int i = 0; i < AddressLength; i++)
                {
                    if (i > 0 && i % AddressGroup == 0)
                    {
                        builder.Append(' ');
                    }

                    // Cased per character rather than per group: Substring and
                    // ToUpperInvariant would allocate two dozen throwaway strings per
                    // pass.
                    builder.Append(char.ToUpperInvariant(address[i]));
                }
            }

            return builder.ToString();
        }

        // Long enough to read as an object turning over rather than a cut. Much shorter
        // and the moment the faces swap reads as the change itself, rather than the
        // rotation that carried it.
        private static readonly TimeSpan FlipDuration = TimeSpan.FromMilliseconds(450);

        // Positive rotation about +Y sends the right edge away from the viewer, the
        // same convention AttachTilt relies on, where the edge under the pointer is the
        // one that dips. A negative half turn therefore brings the right edge forward
        // and sweeps it left. Flip the sign to reverse the gesture.
        private const float FlipDegrees = -180;

        private bool _flipped;
        private bool _flipping;

        private void Flip_Click(object sender, RoutedEventArgs e)
        {
            // A second click mid-turn would snap both faces to the start of the new
            // rotation, since the animation below pins its first keyframe.
            if (_flipping)
            {
                return;
            }

            _flipping = true;

            // Where the front face is and where this click sends it. Going to the back
            // it turns one way; returning it unwinds along the same path rather than
            // continuing round, giving right to left then left to right.
            var from = _flipped ? FlipDegrees : 0;
            _flipped = !_flipped;
            var to = _flipped ? FlipDegrees : 0;

            // Input follows the turn immediately, for the reason given in the
            // constructor: the culled face is off the screen but still hit-tested.
            CardFront.IsHitTestVisible = !_flipped;
            CardBack.IsHitTestVisible = _flipped;

            var compositor = ElementComposition.GetElementVisual(CardFront).Compositor;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += OnFlipCompleted;

            // The faces stay half a turn apart at every instant, so they are never both
            // visible and never disagree about where the card is. Culling handles the
            // visibility half on its own.
            AnimateFace(CardFront, from, to);
            AnimateFace(CardBack, from - FlipDegrees, to - FlipDegrees);

            batch.End();
        }

        private void OnFlipCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= OnFlipCompleted;
            }

            _flipping = false;
        }

        /// <summary>
        /// Turns one face of the card half a turn about its vertical axis. Visibility
        /// needs no handling: both faces set BackfaceVisibility to Hidden, so each is
        /// culled the instant it passes edge on.
        /// </summary>
        private static void AnimateFace(FrameworkElement element, float from, float to)
        {
            var visual = ElementComposition.GetElementVisual(element);
            var compositor = visual.Compositor;

            // Both faces pivot about the middle of the card; any other centre gives them
            // different arcs, which reads as two objects. Set here rather than in the
            // constructor because it requires a measured element.
            visual.RotationAxis = new Vector3(0, 1, 0);
            visual.CenterPoint = new Vector3(
                (float)element.ActualWidth / 2,
                (float)element.ActualHeight / 2,
                0);

            var rotation = compositor.CreateScalarKeyFrameAnimation();
            rotation.InsertKeyFrame(0, from);

            // Ease in and out, matching a card turned over by hand. Nothing else depends
            // on the curve: the faces swap on geometry rather than on the clock, so this
            // remains correct whatever it is changed to.
            rotation.InsertKeyFrame(1, to, compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.42f, 0), new Vector2(0.58f, 1)));
            rotation.Duration = FlipDuration;

            visual.StartAnimation("RotationAngleInDegrees", rotation);
        }
    }

    /// <summary>
    /// Arc-length parameterization of a rounded rectangle, walked clockwise from the middle of
    /// the top edge. No rendering dependency.
    /// </summary>
    public readonly struct RoundedRectPath
    {
        private readonly float _width;
        private readonly float _height;
        private readonly float _radius;
        private readonly float _edgeH;
        private readonly float _edgeV;
        private readonly float _arc;

        private const float HalfPi = (float)(Math.PI / 2);
        private const float Pi = (float)Math.PI;

        public RoundedRectPath(float width, float height, float radius)
        {
            _width = width;
            _height = height;
            _radius = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
            _edgeH = width - 2 * _radius;
            _edgeV = height - 2 * _radius;
            _arc = _radius * HalfPi;
        }

        public float Length => 2 * _edgeH + 2 * _edgeV + 4 * _arc;

        /// <summary>
        /// Maps a distance along the perimeter to a point and the tangent angle there.
        /// Distance 0 is the middle of the top edge and the walk is clockwise, so distance
        /// Length / 2 lands exactly on the middle of the bottom edge.
        /// </summary>
        public void Evaluate(float distance, out Vector2 point, out float angle)
        {
            var total = Length;
            var d = total > 0 ? distance % total : 0;
            if (d < 0)
            {
                d += total;
            }

            // The top edge is split across the two ends of the sweep.
            var half = _edgeH / 2;

            if (d < half)                                   // top edge, middle to right
            {
                point = new Vector2(_width / 2 + d, 0);
                angle = 0;
                return;
            }

            d -= half;

            if (d < _arc)                                   // top-right corner
            {
                EvaluateArc(new Vector2(_width - _radius, _radius), -HalfPi + d / _radius, out point, out angle);
                return;
            }

            d -= _arc;

            if (d < _edgeV)                                 // right edge, top to bottom
            {
                point = new Vector2(_width, _radius + d);
                angle = HalfPi;
                return;
            }

            d -= _edgeV;

            if (d < _arc)                                   // bottom-right corner
            {
                EvaluateArc(new Vector2(_width - _radius, _height - _radius), d / _radius, out point, out angle);
                return;
            }

            d -= _arc;

            if (d < _edgeH)                                 // bottom edge, right to left,
            {                                               // crossing its midpoint at Length / 2
                point = new Vector2(_width - _radius - d, _height);
                angle = Pi;
                return;
            }

            d -= _edgeH;

            if (d < _arc)                                   // bottom-left corner
            {
                EvaluateArc(new Vector2(_radius, _height - _radius), HalfPi + d / _radius, out point, out angle);
                return;
            }

            d -= _arc;

            if (d < _edgeV)                                 // left edge, bottom to top
            {
                point = new Vector2(0, _height - _radius - d);
                angle = -HalfPi;
                return;
            }

            d -= _edgeV;

            if (d < _arc)                                   // top-left corner
            {
                EvaluateArc(new Vector2(_radius, _radius), Pi + d / _radius, out point, out angle);
                return;
            }

            d -= _arc;

            point = new Vector2(_radius + d, 0);            // top edge, left to middle
            angle = 0;
        }

        private void EvaluateArc(Vector2 center, float theta, out Vector2 point, out float angle)
        {
            point = center + new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * _radius;
            angle = theta + HalfPi;
        }
    }

    /// <summary>
    /// Lays a group-separated monospace string around the perimeter of a rounded rectangle,
    /// justified so the text covers the path exactly once.
    ///
    /// The panel's own bounds are the path. Glyphs are drawn outside those bounds, so size it
    /// to the card plus the outset you want (a negative Margin over the card is the easy way).
    /// </summary>
    public sealed class RingTextPresenter : Panel
    {
        private struct Cell
        {
            public TextBlock Glyph;             // null for a separator
            public MatrixTransform Transform;
            public double Weight;
        }

        private readonly List<Cell> _cells = new List<Cell>();
        private double _totalWeight;

        public RingTextPresenter()
        {
            IsHitTestVisible = false;
        }

        #region Dependency properties

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(RingTextPresenter),
                new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RingTextPresenter)d).Rebuild();
        }

        #endregion

        #region Layout properties

        /// <summary>Corner radius of the path. Match the card's radius plus the outset.</summary>
        public double Radius
        {
            get => _radius;
            set { _radius = value; InvalidateArrange(); }
        }
        private double _radius = 36;

        /// <summary>Distance from the path to the baseline. Positive pushes the text outwards.</summary>
        public double Gap
        {
            get => _gap;
            set { _gap = value; InvalidateArrange(); }
        }
        private double _gap = 2;

        /// <summary>
        /// Width of the gap between groups, relative to one letter cell. 1 gives an ordinary
        /// monospace space; the reference design sits closer to 1.5.
        /// </summary>
        public double SeparatorWeight
        {
            get => _separatorWeight;
            set { _separatorWeight = value; Rebuild(); }
        }
        private double _separatorWeight = 1.5;

        /// <summary>Sum of all cell weights. One weight unit is Length / TotalWeight long.</summary>
        public double TotalWeight => _totalWeight;

        /// <summary>
        /// Shifts the whole ring along the path, as a fraction of the perimeter. Zero centres
        /// the first glyph on the middle of the top edge.
        /// </summary>
        public double StartOffset
        {
            get => _startOffset;
            set { _startOffset = value; InvalidateArrange(); }
        }
        private double _startOffset;

        #endregion

        private void Rebuild()
        {
            Children.Clear();
            _cells.Clear();
            _totalWeight = 0;

            var groups = (Text ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var group in groups)
            {
                foreach (var character in group)
                {
                    var glyph = new TextBlock
                    {
                        Text = character.ToString(),
                        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                        FontSize = 12,
                        // Rotated elements should not be snapped to the layout grid; rounding
                        // makes the ring wobble.
                        UseLayoutRounding = false,
                        TextLineBounds = Windows.UI.Xaml.TextLineBounds.Tight
                    };

                    var transform = new MatrixTransform();
                    glyph.RenderTransform = transform;

                    Children.Add(glyph);

                    _cells.Add(new Cell { Glyph = glyph, Transform = transform, Weight = 1 });
                    _totalWeight += 1;
                }

                // A separator after every group, including the last one. That is what keeps the
                // seam gap identical to the other eleven.
                _cells.Add(new Cell { Weight = _separatorWeight });
                _totalWeight += _separatorWeight;
            }

            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }

            return new Size(
                double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_totalWeight <= 0)
            {
                return finalSize;
            }

            var path = new RoundedRectPath((float)finalSize.Width, (float)finalSize.Height, (float)_radius);

            // Monospace plus full justification means every cell is the same fraction of the
            // perimeter. Nothing here depends on the measured advance.
            var unit = path.Length / (float)_totalWeight;

            // Centre the first cell on the path origin rather than butting its leading edge
            // against it, so a leading separator glyph lands on the top edge midpoint itself.
            var cursor = (float)(_startOffset * path.Length) - (float)_cells[0].Weight * unit / 2;

            foreach (var cell in _cells)
            {
                var length = (float)(cell.Weight * unit);

                if (cell.Glyph != null)
                {
                    var size = cell.Glyph.DesiredSize;
                    cell.Glyph.Arrange(new Rect(0, 0, size.Width, size.Height));

                    path.Evaluate(cursor + length / 2, out var point, out var angle);

                    // The glyph-local point that should land on the path: horizontally centred,
                    // vertically on the baseline, pushed out by Gap.
                    var anchor = new Vector2(
                        (float)(size.Width / 2),
                        (float)(cell.Glyph.BaselineOffset + _gap));

                    var matrix =
                        Matrix3x2.CreateTranslation(-anchor) *
                        Matrix3x2.CreateRotation(angle) *
                        Matrix3x2.CreateTranslation(point);

                    cell.Transform.Matrix = new Matrix(
                        matrix.M11, matrix.M12,
                        matrix.M21, matrix.M22,
                        matrix.M31, matrix.M32);
                }

                cursor += length;
            }

            return finalSize;
        }
    }
}
