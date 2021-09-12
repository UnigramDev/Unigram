using Microsoft.Graphics.Canvas.Effects;
using System;
using Windows.Foundation;
using Windows.Graphics.Effects;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Media
{
    public class TiledBrush : XamlCompositionBrushBase
    {
        public Uri Source { get; set; }

        public LoadedImageSurface SvgSource { get; set; }

        public bool IsNegative { get; set; }

        private readonly int _width;
        private readonly int _height;

        public TiledBrush()
        {
            _width = 480;
            _height = 750;
        }

        public TiledBrush(int width, int height)
        {
            _width = width;
            _height = height;
        }

        protected override void OnConnected()
        {
            if (CompositionBrush == null && (Source != null || SvgSource != null))
            {
                var surface = SvgSource ?? LoadedImageSurface.StartLoadFromUri(Source, new Size(_width, _height));
                var surfaceBrush = Window.Current.Compositor.CreateSurfaceBrush(surface);
                surfaceBrush.Stretch = CompositionStretch.None;

                var borderEffect = new BorderEffect()
                {
                    Source = new CompositionEffectSourceParameter("source"),
                    ExtendX = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap,
                    ExtendY = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap
                };

                IGraphicsEffect effect = borderEffect;
                if (IsNegative)
                { 
                    var matrix = new ColorMatrixEffect
                    {
                        ColorMatrix = new Matrix5x4
                        {
                            M11 = 1, M12 = 0, M13 = 0, M14 = 0,
                            M21 = 0, M22 = 1, M23 = 0, M24 = 0,
                            M31 = 0, M32 = 0, M33 = 1, M34 = 0,
                            M41 = 0, M42 = 0, M43 = 0, M44 =-1,
                            M51 = 0, M52 = 0, M53 = 0, M54 = 1
                        },
                        Source = borderEffect
                    };

                    effect = new GammaTransferEffect()
                    {
                        AlphaAmplitude = -1,
                        AlphaOffset = 1,
                        RedDisable = true,
                        GreenDisable = true,
                        BlueDisable = true,
                        Source = borderEffect
                    };
                }

                var borderEffectFactory = Window.Current.Compositor.CreateEffectFactory(effect);
                var borderEffectBrush = borderEffectFactory.CreateBrush();
                borderEffectBrush.SetSourceParameter("source", surfaceBrush);

                CompositionBrush = borderEffectBrush;
            }
        }

        protected override void OnDisconnected()
        {
            if (CompositionBrush != null)
            {
                CompositionBrush.Dispose();
                CompositionBrush = null;
            }
        }
    }
}
