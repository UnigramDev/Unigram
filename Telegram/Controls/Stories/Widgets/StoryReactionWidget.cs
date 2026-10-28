//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Stories.Widgets
{
    /// <summary>
    /// A suggested reaction's speech bubble, in the story viewer and in the editor alike. Its
    /// circle fills the control, which is how a story area places it, and its tail reaches past the
    /// bottom corner. The reaction goes in <see cref="Child"/>, which gets the square the
    /// reaction's image takes in the middle of the circle.
    /// </summary>
    public sealed partial class StoryReactionWidget : Grid
    {
        // The share of the circle's side the reaction's image takes.
        private const double IconScale = 0.61;

        // The tail's two circles, toward the bottom right corner: their centres and radii as shares
        // of the circle's side.
        private static readonly Vector3 TailLarge = new(0.843f, 0.812f, 0.132f);
        private static readonly Vector3 TailSmall = new(1.027f, 0.956f, 0.055f);

        // How far the tail reaches past the circle, sideways and downward.
        private const double OverhangX = 0.09;
        private const double OverhangY = 0.02;

        private readonly Canvas _canvas = new();
        private readonly Border _shadow = new();
        private readonly XamlPath _bubble = new();
        private readonly Border _slot = new();

        private readonly EllipseGeometry _circle = new();
        private readonly EllipseGeometry _large = new();
        private readonly EllipseGeometry _small = new();

        private double _side;

        private bool _isDark;
        private bool _isFlipped;

        public StoryReactionWidget()
        {
            var data = new GeometryGroup
            {
                FillRule = FillRule.Nonzero
            };

            data.Children.Add(_circle);
            data.Children.Add(_large);
            data.Children.Add(_small);

            _bubble.Data = data;
            _bubble.Fill = new SolidColorBrush(Colors.White);

            _canvas.Children.Add(_shadow);
            _canvas.Children.Add(_bubble);

            _slot.HorizontalAlignment = HorizontalAlignment.Center;
            _slot.VerticalAlignment = VerticalAlignment.Center;

            Children.Add(_canvas);
            Children.Add(_slot);

            VisualUtilities.DropShadow(_bubble, 4, 0.18f, _shadow, offset: new Vector3(0, 0.7f, 0));

            SizeChanged += OnSizeChanged;
        }

        public UIElement Child
        {
            get => _slot.Child;
            set => _slot.Child = value;
        }

        /// <summary>
        /// A translucent black bubble rather than a white one.
        /// </summary>
        public bool IsDark
        {
            get => _isDark;
            set
            {
                if (_isDark != value)
                {
                    _isDark = value;
                    UpdateStyle();
                }
            }
        }

        /// <summary>
        /// The tail toward the bottom left corner rather than the right one.
        /// </summary>
        public bool IsFlipped
        {
            get => _isFlipped;
            set
            {
                if (_isFlipped != value)
                {
                    _isFlipped = value;
                    UpdateGeometry();
                }
            }
        }

        /// <summary>
        /// The colour a reaction drawn in a single colour takes on this bubble.
        /// </summary>
        public Color TintColor => _isDark ? Colors.White : Colors.Black;

        private void UpdateStyle()
        {
            // The shadow would show through the translucent bubble.
            _bubble.Fill = new SolidColorBrush(_isDark ? Color.FromArgb(0x7F, 0, 0, 0) : Colors.White);
            _shadow.Visibility = _isDark
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _side = e.NewSize.Width;

            _slot.Width = _side * IconScale;
            _slot.Height = _side * IconScale;

            UpdateGeometry();
        }

        // The path starts where the tail reaches on the left, so that none of it lies at negative
        // coordinates, which its shadow's mask would cut off.
        private void UpdateGeometry()
        {
            var side = _side;
            var left = side * OverhangX;

            _bubble.Width = _shadow.Width = side * (1 + OverhangX * 2);
            _bubble.Height = _shadow.Height = side * (1 + OverhangY);

            Canvas.SetLeft(_bubble, -left);
            Canvas.SetLeft(_shadow, -left);

            _circle.Center = new Point(left + side / 2, side / 2);
            _circle.RadiusX = _circle.RadiusY = side / 2;

            UpdateTail(_large, TailLarge, left, side);
            UpdateTail(_small, TailSmall, left, side);
        }

        private void UpdateTail(EllipseGeometry geometry, Vector3 circle, double left, double side)
        {
            var x = _isFlipped ? 1 - circle.X : circle.X;

            geometry.Center = new Point(left + x * side, circle.Y * side);
            geometry.RadiusX = geometry.RadiusY = circle.Z * side;
        }
    }
}
