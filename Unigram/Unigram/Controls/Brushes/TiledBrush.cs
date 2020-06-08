using Microsoft.Graphics.Canvas.Effects;
using System;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Brushes
{
    public class TiledBrush : XamlCompositionBrushBase
    {
        public Uri Source { get; set; }

        public LoadedImageSurface SvgSource { get; set; }

        private int _width;
        private int _height;

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

                var borderEffectFactory = Window.Current.Compositor.CreateEffectFactory(borderEffect);
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
