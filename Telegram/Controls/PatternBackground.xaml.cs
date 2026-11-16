//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class PatternBackground : ContentControl
    {
        public PatternBackground()
        {
            DefaultStyleKey = typeof(PatternBackground);
        }

        private AnimatedImageSource _pattern;
        private Color _centerColor;
        private Color _edgeColor;
        private Color _symbolColor;

        #region InitializeContent

        private Grid HeaderRoot;
        private Border HeaderGlow;
        private Border RollLayer;
        private ProfilePatternCover Pattern;
        private ContentPresenter ContentPresenter;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            HeaderRoot = GetTemplateChild(nameof(HeaderRoot)) as Grid;
            HeaderGlow = GetTemplateChild(nameof(HeaderGlow)) as Border;
            RollLayer = GetTemplateChild(nameof(RollLayer)) as Border;
            Pattern = GetTemplateChild(nameof(Pattern)) as ProfilePatternCover;
            ContentPresenter = GetTemplateChild(nameof(ContentPresenter)) as ContentPresenter;
            ContentPresenter.SizeChanged += OnSizeChanged;

            _templateApplied = true;

            if (_pattern != null)
            {
                Update(_pattern, _centerColor, _edgeColor, _symbolColor);
            }

            base.OnApplyTemplate();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Pattern.Center = new RectangleF(new Vector2(0, 36), e.NewSize.ToVector2());
        }

        #endregion

        public void Update(IClientService clientService, UpgradedGift gift)
        {
            var source = DelayedFileSource.FromSticker(clientService, gift.Symbol.Sticker);
            var centerColor = gift.Backdrop.Colors.CenterColor.ToColor();
            var edgeColor = gift.Backdrop.Colors.EdgeColor.ToColor();
            var symbolColor = gift.Backdrop.Colors.SymbolColor.ToColor();

            Update(source, centerColor, edgeColor, symbolColor);
        }

        public void Update(IClientService clientService, UpgradedGiftBackdrop backdrop, UpgradedGiftSymbol symbol)
        {
            var source = DelayedFileSource.FromSticker(clientService, symbol.Sticker);
            var centerColor = backdrop.Colors.CenterColor.ToColor();
            var edgeColor = backdrop.Colors.EdgeColor.ToColor();
            var symbolColor = backdrop.Colors.SymbolColor.ToColor();

            Update(source, centerColor, edgeColor, symbolColor);
        }

        public void Update(AnimatedImageSource pattern, Color centerColor, Color edgeColor, Color symbolColor)
        {
            _pattern = pattern;
            _centerColor = centerColor;
            _edgeColor = edgeColor;
            _symbolColor = symbolColor;

            if (!_templateApplied)
            {
                return;
            }

            // TODO: support for ProfileColors here.
            // Currently only used for gifts, would be nice to use for profile too
            var radial = new RadialGradientBrush();
            radial.Center = new Point(0.5f, 0.5f);
            radial.RadiusX = 0.5;
            radial.RadiusY = 0.5;
            radial.GradientStops.Add(new GradientStop { Color = centerColor });
            radial.GradientStops.Add(new GradientStop { Color = edgeColor, Offset = 1 });

            HeaderRoot.Background = radial;
            HeaderRoot.RequestedTheme = ElementTheme.Dark;

            Pattern.Foreground = new SolidColorBrush(symbolColor);
            Pattern.Source = pattern;
        }

        /// <summary>
        /// Swaps the symbol and its colour without touching the backdrop.
        /// </summary>
        public void UpdatePattern(AnimatedImageSource pattern, Color symbolColor)
        {
            _pattern = pattern;
            _symbolColor = symbolColor;

            if (_templateApplied)
            {
                Pattern.Foreground = new SolidColorBrush(symbolColor);
                Pattern.Source = pattern;
            }
        }

        public void UpdatePatternColor(Color symbolColor)
        {
            _symbolColor = symbolColor;

            if (_templateApplied)
            {
                Pattern.Foreground = new SolidColorBrush(symbolColor);
            }
        }

        #region Roll

        private sealed class RollSlot
        {
            public SpriteVisual Visual;
            public CompositionColorGradientStop Center;
            public CompositionColorGradientStop Edge;
            public CompositionRadialGradientBrush Radial;
            public CompositionColorGradientStop FeatherIn;
            public CompositionColorGradientStop FeatherOut;
            public UpgradedGiftBackdrop Backdrop;
            public Vector2 Size;
        }

        private ContainerVisual _roll;
        private RollSlot[] _rollSlots;

        // The edges of a sliding backdrop fade out over this width, so neighbours blend instead of
        // meeting at a seam.
        private const float RollFeather = 90;

        /// <summary>
        /// Draws one of the backdrops sliding across the header while an upgrade rolls. At a
        /// position of 0.5 the backdrop is centred and opaque, and it matches the static one.
        /// </summary>
        public void SetRollBackdrop(int index, UpgradedGiftBackdrop backdrop, float position)
        {
            if (!_templateApplied)
            {
                return;
            }

            if (_roll == null)
            {
                var compositor = BootStrapper.Current.Compositor;

                _roll = compositor.CreateContainerVisual();
                _roll.RelativeSizeAdjustment = Vector2.One;
                _roll.Clip = compositor.CreateInsetClip();
                _rollSlots = new RollSlot[3];

                ElementCompositionPreview.SetElementChildVisual(RollLayer, _roll);
            }

            var slot = _rollSlots[index] ??= CreateRollSlot();

            if (backdrop == null)
            {
                slot.Visual.IsVisible = false;
                return;
            }

            var width = (float)HeaderRoot.ActualWidth;
            var height = (float)HeaderRoot.ActualHeight;
            var radius = Math.Max(0.8f * width, 180);

            if (slot.Backdrop != backdrop)
            {
                slot.Backdrop = backdrop;
                slot.Center.Color = backdrop.Colors.CenterColor.ToColor();
                slot.Edge.Color = backdrop.Colors.EdgeColor.ToColor();
            }

            var size = new Vector2(radius * 2, height);
            if (slot.Size != size)
            {
                slot.Size = size;
                slot.Visual.Size = size;
                slot.Radial.EllipseCenter = new Vector2(radius, height / 2);
                slot.Radial.EllipseRadius = new Vector2(width / 2, height / 2);
                slot.FeatherIn.Offset = RollFeather / size.X;
                slot.FeatherOut.Offset = 1 - RollFeather / size.X;
            }

            var tt = (position - 0.5f) / 1.5f;
            var center = width / 2 - tt * radius * 1.8f;

            slot.Visual.Offset = new Vector3(center - radius, 0, 0);
            slot.Visual.Opacity = Math.Clamp(1 - Math.Abs(tt), 0, 1);
            slot.Visual.IsVisible = true;
        }

        private RollSlot CreateRollSlot()
        {
            var compositor = _roll.Compositor;
            var slot = new RollSlot();

            slot.Center = compositor.CreateColorGradientStop(0, Colors.Transparent);
            slot.Edge = compositor.CreateColorGradientStop(1, Colors.Transparent);

            // Absolute, so the ellipse keeps the header's proportions however wide the visual is.
            slot.Radial = compositor.CreateRadialGradientBrush();
            slot.Radial.MappingMode = CompositionMappingMode.Absolute;
            slot.Radial.ColorStops.Add(slot.Center);
            slot.Radial.ColorStops.Add(slot.Edge);

            slot.FeatherIn = compositor.CreateColorGradientStop(0, Colors.White);
            slot.FeatherOut = compositor.CreateColorGradientStop(1, Colors.White);

            var mask = compositor.CreateLinearGradientBrush();
            mask.StartPoint = Vector2.Zero;
            mask.EndPoint = Vector2.UnitX;
            mask.ColorStops.Add(compositor.CreateColorGradientStop(0, Colors.Transparent));
            mask.ColorStops.Add(slot.FeatherIn);
            mask.ColorStops.Add(slot.FeatherOut);
            mask.ColorStops.Add(compositor.CreateColorGradientStop(1, Colors.Transparent));

            var brush = compositor.CreateMaskBrush();
            brush.Source = slot.Radial;
            brush.Mask = mask;

            slot.Visual = compositor.CreateSpriteVisual();
            slot.Visual.Brush = brush;
            slot.Visual.IsVisible = false;

            _roll.Children.InsertAtTop(slot.Visual);
            return slot;
        }

        public void ClearRoll()
        {
            if (_rollSlots == null)
            {
                return;
            }

            foreach (var slot in _rollSlots)
            {
                if (slot != null)
                {
                    slot.Visual.IsVisible = false;
                    slot.Backdrop = null;
                }
            }
        }

        #endregion

        public void Clear()
        {
            if (!_templateApplied)
            {
                return;
            }

            HeaderRoot.Background = null;
            Pattern.Source = null;
        }

        #region Footer

        public object Footer
        {
            get { return (object)GetValue(FooterProperty); }
            set { SetValue(FooterProperty, value); }
        }

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register("Footer", typeof(object), typeof(PatternBackground), new PropertyMetadata(null));

        #endregion
    }
}
