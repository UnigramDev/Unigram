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
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Gifts
{
    public sealed partial class GiftAttributeGauge : UserControl
    {
        public GiftAttributeGauge(IClientService clientService, UpgradedGiftBackdrop backdrop, float probability)
        {
            InitializeComponent();
            InitializeProbability(probability);

            if (backdrop != null)
            {
                var radial = new RadialGradientBrush();
                radial.Center = new Point(0.5f, 0.5f);
                radial.RadiusX = 0.5;
                radial.RadiusY = 0.5;
                radial.GradientStops.Add(new GradientStop { Color = backdrop.Colors.CenterColor.ToColor() });
                radial.GradientStops.Add(new GradientStop { Color = backdrop.Colors.EdgeColor.ToColor(), Offset = 1 });

                Backdrop.Background = radial;
            }
            else
            {
                Backdrop.Background = new SolidColorBrush(Color.FromArgb(0x55, 255, 255, 255));
            }
        }

        public GiftAttributeGauge(IClientService clientService, UpgradedGiftSymbol symbol, float probability)
        {
            InitializeComponent();
            InitializeProbability(probability);

            if (symbol != null)
            {
                Symbol.Source = DelayedFileSource.FromSticker(clientService, symbol.Sticker);
            }
            else
            {
                Backdrop.Background = new SolidColorBrush(Color.FromArgb(0x55, 255, 255, 255));
            }
        }

        private void InitializeProbability(float probability)
        {
            Probability.Text = (probability * 100).ToString("0.##") + "%";

            var compositor = BootStrapper.Current.Compositor;
            var visual = compositor.CreateShapeVisual();

            var background = compositor.CreateEllipseGeometry();
            background.Radius = new Vector2(15);
            background.Center = new Vector2(16);
            background.TrimStart = 0.25f;

            var backgroundShape = compositor.CreateSpriteShape(background);
            backgroundShape.StrokeBrush = compositor.CreateColorBrush(Color.FromArgb(0x55, 255, 255, 255));
            backgroundShape.StrokeThickness = 2;
            backgroundShape.StrokeStartCap = Windows.UI.Composition.CompositionStrokeCap.Round;
            backgroundShape.StrokeEndCap = Windows.UI.Composition.CompositionStrokeCap.Round;
            backgroundShape.RotationAngleInDegrees = 45 + 90;
            backgroundShape.CenterPoint = new Vector2(16);

            var foreground = compositor.CreateEllipseGeometry();
            foreground.Radius = new Vector2(15);
            foreground.Center = new Vector2(16);
            foreground.TrimStart = 0.25f;
            foreground.TrimEnd = 0.25f + (probability * 0.75f);

            var foregroundShape = compositor.CreateSpriteShape(foreground);
            foregroundShape.StrokeBrush = compositor.CreateColorBrush(Color.FromArgb(255, 255, 255, 255));
            foregroundShape.StrokeThickness = 2;
            foregroundShape.StrokeStartCap = Windows.UI.Composition.CompositionStrokeCap.Round;
            foregroundShape.StrokeEndCap = Windows.UI.Composition.CompositionStrokeCap.Round;
            foregroundShape.RotationAngleInDegrees = 45 + 90;
            foregroundShape.CenterPoint = new Vector2(16);

            visual.Shapes.Add(backgroundShape);
            visual.Shapes.Add(foregroundShape);
            visual.Size = new Vector2(32);

            ElementCompositionPreview.SetElementChildVisual(Gauge, visual);
        }
    }
}
