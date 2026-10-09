//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System.Numerics;
using Telegram.Navigation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public partial class GlassToggleButton : GlyphToggleButton
    {
        private Border CheckedPart;
        private TextBlock ContentPresenter;

        public GlassToggleButton()
        {
            DefaultStyleKey = typeof(GlassToggleButton);

            Checked += OnToggle;
            Unchecked += OnToggle;

            SizeChanged += OnSizeChanged;
        }

        protected override void OnApplyTemplate()
        {
            CheckedPart = GetTemplateChild(nameof(CheckedPart)) as Border;
            ContentPresenter = GetTemplateChild(nameof(ContentPresenter)) as TextBlock;

            var background = ElementCompositionPreview.GetElementVisual(ContentPresenter);
            var foreground = ElementCompositionPreview.GetElementVisual(CheckedPart);

            var show = IsChecked == true;

            background.Clip = background.Compositor.CreateInsetClip(show ? 48 : 0, show ? 48 : 0, 0, 0);
            foreground.Clip = background.Compositor.CreateInsetClip(show ? 0 : 48, show ? 0 : 48, 0, 0);

            base.OnApplyTemplate();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCheckedPart();
        }

        protected override void OnGlyphChanged(string newValue, string oldValue)
        {
            UpdateCheckedPart();
        }

        private void UpdateCheckedPart()
        {
            if (CheckedPart == null || ContentPresenter == null || ActualWidth == 0 || ActualHeight == 0)
            {
                return;
            }

            // Glyph rather than ContentPresenter.Text: the template binding that feeds the text
            // block is not guaranteed to have run by the time this callback does.
            var text = Glyph;
            if (string.IsNullOrEmpty(text))
            {
                ElementCompositionPreview.SetElementChildVisual(CheckedPart, null);
                return;
            }

            var size = ActualSize;
            var device = ElementComposition.GetSharedDevice();

            // The checked state is a white plate with the glyph knocked out of it, so that the
            // acrylic behind the button shows through the icon. Cutting the outline out of the
            // plate keeps it a plain shape: rendering the glyph to an alpha mask instead would
            // cost a render target and an effect graph evaluated on every composited frame.
            //
            // Rooted with using: Win2D's UWP projection has no GC.KeepAlive, so a geometry whose
            // only reference is a temporary can be finalized while CombineWith is still running.
            using var format = new CanvasTextFormat
            {
                FontFamily = ContentPresenter.FontFamily.Source,
                FontSize = (float)ContentPresenter.FontSize,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            };

            using var layout = new CanvasTextLayout(device, text, format, size.X, size.Y);
            using var glyph = CanvasGeometry.CreateText(layout);
            using var plate = CanvasGeometry.CreateRectangle(device, 0, 0, size.X, size.Y);
            using var knockout = plate.CombineWith(glyph, Matrix3x2.Identity, CanvasGeometryCombine.Exclude);

            var compositor = BootStrapper.Current.Compositor;

            var shape = compositor.CreateSpriteShape(compositor.CreatePathGeometry(new CompositionPath(knockout)));
            shape.FillBrush = compositor.CreateColorBrush(Colors.White);
            shape.StrokeThickness = 0;

            var visual = compositor.CreateShapeVisual();
            visual.RelativeSizeAdjustment = Vector2.One;
            visual.Shapes.Add(shape);

            ElementCompositionPreview.SetElementChildVisual(CheckedPart, visual);
        }

        protected override void OnToggle()
        {
            // We ignore clicks and control the checked state manually
        }

        private void OnToggle(object sender, RoutedEventArgs e)
        {
            if (ContentPresenter == null || CheckedPart == null)
            {
                return;
            }

            var background = ElementCompositionPreview.GetElementVisual(ContentPresenter);
            var foreground = ElementCompositionPreview.GetElementVisual(CheckedPart);

            var compositor = background.Compositor;

            var elli1 = CanvasGeometry.CreateCircle(null, 24, 24, 24);
            var group1 = CanvasGeometry.CreateGroup(null, new[] { elli1, elli1 }, CanvasFilledRegionDetermination.Alternate);

            var elli2 = CanvasGeometry.CreateCircle(null, 24, 24, 0);
            var group2 = CanvasGeometry.CreateGroup(null, new[] { elli2, elli1 }, CanvasFilledRegionDetermination.Alternate);

            var back = compositor.CreateEllipseGeometry();
            var fore = compositor.CreatePathGeometry(new CompositionPath(group2));

            back.Center = new Vector2(24, 12);

            background.Clip = compositor.CreateGeometricClip(back);
            foreground.Clip = compositor.CreateGeometricClip(fore);

            var show = IsChecked == true;

            var backRadius = compositor.CreateVector2KeyFrameAnimation();
            backRadius.InsertKeyFrame(show ? 0 : 1, new Vector2(24));
            backRadius.InsertKeyFrame(show ? 1 : 0, new Vector2(0));

            var foreRadius = compositor.CreatePathKeyFrameAnimation();
            foreRadius.InsertKeyFrame(show ? 0 : 1, new CompositionPath(group1));
            foreRadius.InsertKeyFrame(show ? 1 : 0, new CompositionPath(group2));

            back.StartAnimation("Radius", backRadius);
            fore.StartAnimation("Path", foreRadius);
        }
    }
}
