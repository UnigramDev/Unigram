using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Xaml.Media;
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
using Point = Windows.Foundation.Point;

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

        #region InitializeContent

        private Grid HeaderRoot;
        private Border HeaderGlow;
        private ProfileHeaderPattern Pattern;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            HeaderRoot = GetTemplateChild(nameof(HeaderRoot)) as Grid;
            HeaderGlow = GetTemplateChild(nameof(HeaderGlow)) as Border;
            Pattern = GetTemplateChild(nameof(Pattern)) as ProfileHeaderPattern;

            _templateApplied = true;

            if (_pattern != null)
            {
                Update(_pattern, _centerColor, _edgeColor);
            }

            base.OnApplyTemplate();
        }

        #endregion

        public void Update(IClientService clientService, UpgradedGift gift)
        {
            var source = DelayedFileSource.FromSticker(clientService, gift.Symbol.Sticker);
            var centerColor = gift.Backdrop.Colors.CenterColor.ToColor();
            var edgeColor = gift.Backdrop.Colors.EdgeColor.ToColor();

            Update(source, centerColor, edgeColor);
        }

        public void Update(AnimatedImageSource pattern, Color centerColor, Color edgeColor)
        {
            _pattern = pattern;
            _centerColor = centerColor;
            _edgeColor = edgeColor;

            if (!_templateApplied)
            {
                return;
            }

            //Identity.Foreground = new SolidColorBrush(Colors.White);
            //BotVerified.ReplacementColor = new SolidColorBrush(Colors.White);

            HeaderRoot.RequestedTheme = ElementTheme.Dark;

            var gradient = new LinearGradientBrush();
            gradient.StartPoint = new Point(0, 0);
            gradient.EndPoint = new Point(0, 1);
            gradient.GradientStops.Add(new GradientStop
            {
                Color = centerColor,
                Offset = 0
            });

            gradient.GradientStops.Add(new GradientStop
            {
                Color = edgeColor,
                Offset = 1
            });

            HeaderRoot.Background = gradient;

            Pattern.Source = pattern;

            var compositor = BootStrapper.Current.Compositor;

            // Create a VisualSurface positioned at the same location as this control and feed that
            // through the color effect.
            var surfaceBrush = compositor.CreateSurfaceBrush();
            surfaceBrush.Stretch = CompositionStretch.None;
            var surface = compositor.CreateVisualSurface();

            // Select the source visual and the offset/size of this control in that element's space.
            surface.SourceVisual = ElementComposition.GetElementVisual(Pattern);
            surface.SourceOffset = new Vector2(0, 0);
            surface.SourceSize = new Vector2(1000, 320);
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.None;

            CompositionBrush brush;
            var linear = compositor.CreateLinearGradientBrush();
            linear.StartPoint = new Vector2();
            linear.EndPoint = new Vector2(0, 1);
            linear.ColorStops.Add(compositor.CreateColorGradientStop(0, centerColor));
            linear.ColorStops.Add(compositor.CreateColorGradientStop(1, edgeColor));

            brush = linear;

            var radial3 = compositor.CreateRadialGradientBrush();
            //radial.CenterPoint = new Vector2(0.5f, 0.0f);
            radial3.EllipseCenter = new Vector2(0.5f, 0.3f);
            radial3.EllipseRadius = new Vector2(0.4f, 0.6f);
            radial3.ColorStops.Add(compositor.CreateColorGradientStop(0, centerColor));
            radial3.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, edgeColor));
            brush = radial3;

            var radial = compositor.CreateRadialGradientBrush();
            //radial.CenterPoint = new Vector2(0.5f, 0.0f);
            radial.EllipseCenter = new Vector2(0.5f, 0.3f);
            radial.EllipseRadius = new Vector2(0.4f, 0.6f);
            radial.ColorStops.Add(compositor.CreateColorGradientStop(0, Color.FromArgb(200, 0, 0, 0)));
            radial.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(0, 0, 0, 0)));

            var blend = new BlendEffect
            {
                Background = new CompositionEffectSourceParameter("Background"),
                Foreground = new CompositionEffectSourceParameter("Foreground"),
                Mode = BlendEffectMode.SoftLight
            };

            var borderEffectFactory = BootStrapper.Current.Compositor.CreateEffectFactory(blend);
            var borderEffectBrush = borderEffectFactory.CreateBrush();
            borderEffectBrush.SetSourceParameter("Foreground", brush);
            borderEffectBrush.SetSourceParameter("Background", radial); // compositor.CreateColorBrush(Color.FromArgb(80, 0x00, 0x00, 0x00)));

            CompositionMaskBrush maskBrush = compositor.CreateMaskBrush();
            maskBrush.Source = borderEffectBrush; // Set source to content that is to be masked 
            maskBrush.Mask = surfaceBrush; // Set mask to content that is the opacity mask 

            var visual = compositor.CreateSpriteVisual();
            visual.Size = new Vector2(1000, 320);
            visual.Offset = new Vector3(0, 0, 0);
            visual.Brush = maskBrush;

            ElementCompositionPreview.SetElementChildVisual(HeaderGlow, visual);

            var radial2 = new RadialGradientBrush();
            //radial.CenterPoint = new Vector2(0.5f, 0.0f);
            radial2.Center = new Point(0.5f, 0.3f);
            radial2.RadiusX = 0.4;
            radial2.RadiusY = 0.6;
            radial2.GradientStops.Add(new GradientStop { Color = Color.FromArgb(50, 255, 255, 255) });
            radial2.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 0.5 });

            HeaderGlow.Background = radial2;
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

        #region ScaleXY

        public double ScaleXY
        {
            get { return (double)GetValue(ScaleXYProperty); }
            set { SetValue(ScaleXYProperty, value); }
        }

        public static readonly DependencyProperty ScaleXYProperty =
            DependencyProperty.Register("ScaleXY", typeof(double), typeof(PatternBackground), new PropertyMetadata(0.85));

        #endregion
    }
}
