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
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Wallet.Popups
{
    public sealed partial class WalletSharePopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        public WalletSharePopup(IClientService clientService, INavigationService navigationService, string address)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

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

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var response = await _clientService.SendAsync(new GetOnRampProviders("gram"));
            if (response is OnRampProviders providers)
            {
                if (providers.Providers.Count == 1)
                {
                    var provider = providers.Providers[0];

                    response = await _clientService.SendAsync(new CreateOnRampPaymentSession(provider.Id, "gram", _clientService.TonWalletState.Address, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, /*_navigationService.Window.ThemeParameters*/ null, string.Empty, string.Empty));
                    
                    if (response is OnRampPaymentSession session)
                    {
                        response = await _clientService.SendAsync(new GetInternalLinkType(session.Url));

                        if (response is InternalLinkType internalLink)
                        {
                            MessageHelper.OpenTelegramUrl(_clientService, _navigationService, internalLink, null);
                        }
                    }
                }
            }
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
            ContentDialog_PrimaryButtonClick(null, null);

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

        /// <summary>
        /// The distances a keyframe is needed at to trace the whole path: every segment boundary,
        /// and <paramref name="cornerSteps"/> samples across each corner.
        /// </summary>
        /// <remarks>
        /// The straight runs need nothing between their ends, interpolating exactly. The corners do
        /// not: a chord across a quarter turn of radius r misses the arc by r(1 - cos(θ/2)), which
        /// at this size is a tenth of a pixel by the eighth step and half a pixel by the fourth.
        /// </remarks>
        public void GetKeyDistances(int cornerSteps, List<float> distances)
        {
            distances.Clear();

            var distance = 0f;
            distances.Add(distance);

            distance += _edgeH / 2;
            AddCorner(distances, cornerSteps, ref distance);        // top-right

            distance += _edgeV;
            AddCorner(distances, cornerSteps, ref distance);        // bottom-right

            distance += _edgeH;
            AddCorner(distances, cornerSteps, ref distance);        // bottom-left

            distance += _edgeV;
            AddCorner(distances, cornerSteps, ref distance);        // top-left

            distance += _edgeH / 2;
            distances.Add(distance);                                // back to where it started
        }

        private void AddCorner(List<float> distances, int steps, ref float distance)
        {
            distances.Add(distance);

            for (int i = 1; i < steps; i++)
            {
                distances.Add(distance + _arc * i / steps);
            }

            distance += _arc;
            distances.Add(distance);
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

        // Whether the glyphs are being carried by the compositor. False leaves them where arrange
        // put them, which is what a machine asking for less motion gets.
        private bool _running;

        public RingTextPresenter()
        {
            IsHitTestVisible = false;

            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // A popup that has been dismissed would otherwise keep the compositor busy turning a
            // ring nobody is looking at.
            Stop();
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
        /// How long the band takes to travel the path once. Zero leaves it still.
        /// </summary>
        /// <remarks>
        /// Slow: the band is ambient, and a lap the eye can follow reads as something loading.
        /// </remarks>
        public TimeSpan LapDuration
        {
            get => _lapDuration;
            set { _lapDuration = value; InvalidateArrange(); }
        }
        private TimeSpan _lapDuration = TimeSpan.FromSeconds(30);

        // Enough to keep the chord error inside a tenth of a pixel at this radius.
        private const int CornerSteps = 8;

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
            // The children the animations were running on are about to go.
            _running = false;

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
                        FontFamily = Theme.MonospaceFontFamily,
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
            // Whatever is running belongs to the size that has just changed.
            Stop();

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

            // The band travels along the path rather than the panel turning: on a rounded
            // rectangle a rotation would swing the glyphs off the edges and back. Since every cell
            // covers the same fraction of the perimeter, every glyph walks the same poses and
            // differs only in where it starts - so one pair of animations serves all of them, each
            // visual seeked to its own place in the lap.
            var running = _lapDuration > TimeSpan.Zero && PowerSavingPolicy.AreSmoothTransitionsEnabled;

            CompositionAnimation offset = null;
            CompositionAnimation rotation = null;

            if (running)
            {
                Build(path, out offset, out rotation);
            }

            foreach (var cell in _cells)
            {
                var length = (float)(cell.Weight * unit);

                if (cell.Glyph != null)
                {
                    var size = cell.Glyph.DesiredSize;
                    cell.Glyph.Arrange(new Rect(0, 0, size.Width, size.Height));

                    var distance = cursor + length / 2;
                    path.Evaluate(distance, out var point, out var angle);

                    // The glyph-local point that should land on the path: horizontally centred,
                    // vertically on the baseline, pushed out by Gap.
                    var anchor = new Vector2(
                        (float)(size.Width / 2),
                        (float)(cell.Glyph.BaselineOffset + _gap));

                    if (running)
                    {
                        // Only the anchor stays here, so that the visual's own origin is the point
                        // riding the path; the compositor supplies the place and the turn. Started
                        // after Arrange rather than before, because layout writes Offset too and
                        // the last writer wins.
                        cell.Transform.Matrix = new Matrix(1, 0, 0, 1, -anchor.X, -anchor.Y);

                        var visual = ElementComposition.GetElementVisual(cell.Glyph);

                        visual.StartAnimation("Offset", offset);
                        visual.StartAnimation("RotationAngleInDegrees", rotation);

                        var progress = distance / path.Length;

                        Seek(visual, "Offset", progress);
                        Seek(visual, "RotationAngleInDegrees", progress);
                    }
                    else
                    {
                        var matrix =
                            Matrix3x2.CreateTranslation(-anchor) *
                            Matrix3x2.CreateRotation(angle) *
                            Matrix3x2.CreateTranslation(point);

                        cell.Transform.Matrix = new Matrix(
                            matrix.M11, matrix.M12,
                            matrix.M21, matrix.M22,
                            matrix.M31, matrix.M32);
                    }
                }

                cursor += length;
            }

            _running = running;
            return finalSize;
        }

        /// <summary>
        /// One lap of the path, as the pair of animations every glyph runs.
        /// </summary>
        /// <remarks>
        /// Keyframed rather than expressed: the path is eight segments, and an expression that
        /// walked them would be worse to read than the geometry it came from. Straight runs need
        /// nothing between their ends - see <see cref="RoundedRectPath.GetKeyDistances"/>.
        /// </remarks>
        private void Build(RoundedRectPath path, out CompositionAnimation offset, out CompositionAnimation rotation)
        {
            var compositor = ElementComposition.GetElementVisual(this).Compositor;

            // Linear at every keyframe. The default easing is a cubic, which would have each glyph
            // slowing into every sample and pulsing its way round.
            var linear = compositor.CreateLinearEasingFunction();

            var position = compositor.CreateVector3KeyFrameAnimation();
            var angle = compositor.CreateScalarKeyFrameAnimation();

            var distances = new List<float>();
            path.GetKeyDistances(CornerSteps, distances);

            var turns = 0f;
            var previous = 0f;

            for (int i = 0; i < distances.Count; i++)
            {
                var distance = distances[i];
                path.Evaluate(distance, out var point, out var radians);

                // Evaluate answers with a direction, not a running total: the left edge comes back
                // as a quarter turn anticlockwise where the band is three quarters of the way
                // round. Unwrapped here so the keyframes climb once to a full turn, which makes the
                // loop's restart a whole turn rather than a rewind.
                if (i > 0)
                {
                    while (radians + turns < previous - Pi)
                    {
                        turns += Tau;
                    }

                    while (radians + turns > previous + Pi)
                    {
                        turns -= Tau;
                    }
                }

                previous = radians + turns;

                var progress = distance / path.Length;

                position.InsertKeyFrame(1 - progress, new Vector3(point, 0), linear);
                angle.InsertKeyFrame(1 - progress, previous * 180 / Pi, linear);
            }

            position.Duration = _lapDuration;
            position.IterationBehavior = AnimationIterationBehavior.Forever;

            angle.Duration = _lapDuration;
            angle.IterationBehavior = AnimationIterationBehavior.Forever;

            offset = position;
            rotation = angle;
        }

        /// <summary>
        /// Puts one visual at its own place in the lap. The animation is shared; where each glyph
        /// is in it is not.
        /// </summary>
        private static void Seek(Visual visual, string property, float progress)
        {
            var controller = visual.TryGetAnimationController(property);
            if (controller != null)
            {
                // A fraction of a lap, wrapped: StartOffset can put a glyph past the end of the
                // path or before its start.
                controller.Progress = progress - MathF.Floor(progress);
            }
        }

        private void Stop()
        {
            if (!_running)
            {
                return;
            }

            _running = false;

            foreach (var cell in _cells)
            {
                if (cell.Glyph != null)
                {
                    var visual = ElementComposition.GetElementVisual(cell.Glyph);

                    visual.StopAnimation("Offset");
                    visual.StopAnimation("RotationAngleInDegrees");
                }
            }
        }

        private const float Pi = (float)Math.PI;
        private const float Tau = (float)(Math.PI * 2);
    }
}
