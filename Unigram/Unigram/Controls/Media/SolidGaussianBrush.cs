using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Media
{
    public class SolidGaussianBrush : XamlCompositionBrushBase
    {
        private CompositionEffectBrush _brush;

        protected override void OnConnected()
        {
            if (CompositionBrush == null || _brush == null)
            {
                var gaussianBlur = new GaussianBlurEffect
                {
                    Name = "Blur",
                    BlurAmount = 30,
                    Optimization = EffectOptimization.Speed,
                    BorderMode = EffectBorderMode.Hard,
                    Source = new CompositionEffectSourceParameter("Backdrop")
                };

                var saturationEffect = new SaturationEffect
                {
                    Name = "Saturation",
                    Saturation = 1.7f,
                    Source = gaussianBlur
                };

                var tintColorEffect = new ColorSourceEffect
                {
                    Name = "TintColor",
                    Color = Color.FromArgb(52, 0, 0, 0)
                };

                var compositeEffect = new CompositeEffect();
                compositeEffect.Mode = CanvasComposite.SourceOver;
                compositeEffect.Sources.Add(saturationEffect);
                compositeEffect.Sources.Add(tintColorEffect);

                var effectFactory = Window.Current.Compositor.CreateEffectFactory(compositeEffect);
                var backdrop = Window.Current.Compositor.CreateBackdropBrush();

                _brush = effectFactory.CreateBrush();
                _brush.SetSourceParameter("Backdrop", backdrop);

                CompositionBrush = _brush;
            }

            base.OnConnected();
        }

        protected override void OnDisconnected()
        {
            if (_brush != null)
            {
                _brush.Dispose();
                _brush = null;
            }

            CompositionBrush = null;

            base.OnDisconnected();
        }
    }
}
