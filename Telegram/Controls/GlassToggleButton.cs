using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
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
        }

        protected override void OnApplyTemplate()
        {
            CheckedPart = GetTemplateChild(nameof(CheckedPart)) as Border;
            ContentPresenter = GetTemplateChild(nameof(ContentPresenter)) as TextBlock;
            ContentPresenter.SizeChanged += OnSizeChanged;

            var background = ElementCompositionPreview.GetElementVisual(ContentPresenter);
            var foreground = ElementCompositionPreview.GetElementVisual(CheckedPart);

            var show = IsChecked == true;

            background.Clip = background.Compositor.CreateInsetClip(show ? 48 : 0, show ? 48 : 0, 0, 0);
            foreground.Clip = background.Compositor.CreateInsetClip(show ? 0 : 48, show ? 0 : 48, 0, 0);

            base.OnApplyTemplate();
        }

        private void OnSizeChanged(object sender, object e)
        {
            var colorEffect = new ColorSourceEffect
            {
                Color = Colors.White
            };

            var compositeEffect = new CompositeEffect
            {
                Mode = CanvasComposite.Xor
            };

            compositeEffect.Sources.Add(colorEffect);
            compositeEffect.Sources.Add(new CompositionEffectSourceParameter("Source"));

            var compositor = BootStrapper.Current.Compositor;
            var effectFactory = compositor.CreateEffectFactory(compositeEffect);

            // Create a VisualSurface positioned at the same location as this control and feed that
            // through the color effect.
            var surfaceBrush = compositor.CreateSurfaceBrush();
            surfaceBrush.Stretch = CompositionStretch.None;
            var surface = compositor.CreateVisualSurface();

            var testVisual = compositor.CreateSpriteVisual();
            testVisual.Brush = ContentPresenter.GetAlphaMask();
            testVisual.Size = ContentPresenter.ActualSize;

            // Select the source visual and the offset/size of this control in that element's space.
            surface.SourceVisual = testVisual; //ElementCompositionPreview.GetElementVisual(Part3);
            surface.SourceOffset = Vector2.Zero;
            surface.SourceSize = ActualSize;
            surfaceBrush.Offset = (ActualSize - ContentPresenter.ActualSize) / 2;
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.None;

            var effectBrush = effectFactory.CreateBrush();
            effectBrush.SetSourceParameter("Source", surfaceBrush);

            var visual = compositor.CreateSpriteVisual();
            //visual.Size = actualSize;
            visual.RelativeSizeAdjustment = Vector2.One;
            visual.Brush = effectBrush;

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

            //OnLoaded(null, null);

            var background = ElementCompositionPreview.GetElementVisual(ContentPresenter);
            var foreground = ElementCompositionPreview.GetElementVisual(CheckedPart);

            var compositor = background.Compositor;

            //var back = compositor.CreateEllipseGeometry();
            //var fore = compositor.CreateEllipseGeometry();

            var device = ElementComposition.GetSharedDevice();
            var rect1 = CanvasGeometry.CreateRectangle(device, 0, 0, 48, 48);

            var elli1 = CanvasGeometry.CreateCircle(device, 24, 24, 24);
            var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, elli1 }, CanvasFilledRegionDetermination.Alternate);

            var elli2 = CanvasGeometry.CreateCircle(device, 24, 24, 0);
            var group2 = CanvasGeometry.CreateGroup(device, new[] { elli2, elli1 }, CanvasFilledRegionDetermination.Alternate);

            var back = compositor.CreateEllipseGeometry();
            var fore = compositor.CreatePathGeometry(new CompositionPath(group2));

            back.Center = new Vector2(24, 12);
            //fore.Center = new Vector2(24);

            background.Clip = compositor.CreateGeometricClip(back);
            foreground.Clip = compositor.CreateGeometricClip(fore);

            var show = IsChecked == true;

            var backRadius = compositor.CreateVector2KeyFrameAnimation();
            backRadius.InsertKeyFrame(show ? 0 : 1, new Vector2(24));
            backRadius.InsertKeyFrame(show ? 1 : 0, new Vector2(0));
            //backRadius.Duration = TimeSpan.FromSeconds(5);

            //var foreRadius = compositor.CreateVector2KeyFrameAnimation();
            //foreRadius.InsertKeyFrame(show ? 0 : 1, new Vector2(0));
            //foreRadius.InsertKeyFrame(show ? 1 : 0, new Vector2(24));
            var foreRadius = compositor.CreatePathKeyFrameAnimation();
            foreRadius.InsertKeyFrame(show ? 0 : 1, new CompositionPath(group1));
            foreRadius.InsertKeyFrame(show ? 1 : 0, new CompositionPath(group2));
            //foreRadius.Duration = TimeSpan.FromSeconds(5);

            back.StartAnimation("Radius", backRadius);
            fore.StartAnimation("Path", foreRadius);
        }
    }
}
