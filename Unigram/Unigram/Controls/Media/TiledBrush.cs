using Microsoft.Graphics.Canvas.Effects;
using System.Collections.Generic;
using Windows.Graphics.Effects;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Media
{
    public class TiledBrush : XamlCompositionBrushBase
    {
        public LoadedImageSurface Surface { get; set; }

        public bool IsInverted { get; set; }

        public double Intensity { get; set; }

        protected override void OnConnected()
        {
            _connected = true;
            _negative = IsInverted;

            if (_recreate || (CompositionBrush == null && Surface != null))
            {
                _recreate = false;

                var surface = Surface;
                var surfaceBrush = Window.Current.Compositor.CreateSurfaceBrush(surface);
                surfaceBrush.Stretch = CompositionStretch.None;

                var borderEffect = new BorderEffect()
                {
                    Source = new CompositionEffectSourceParameter("Source"),
                    ExtendX = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap,
                    ExtendY = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap
                };

                IGraphicsEffect effect;
                IEnumerable<string> animatableProperties;
                if (IsInverted)
                {
                    //var matrix = new ColorMatrixEffect
                    //{
                    //    ColorMatrix = new Matrix5x4
                    //    {
                    //        M11 = 1, M12 = 0, M13 = 0, M14 = 0,
                    //        M21 = 0, M22 = 1, M23 = 0, M24 = 0,
                    //        M31 = 0, M32 = 0, M33 = 1, M34 = 0,
                    //        M41 = 0, M42 = 0, M43 = 0, M44 =-1,
                    //        M51 = 0, M52 = 0, M53 = 0, M54 = 1
                    //    },
                    //    Source = borderEffect
                    //};
                    _tintEffect = null;

                    animatableProperties = new string[0];
                    effect = new GammaTransferEffect()
                    {
                        AlphaAmplitude = -1,
                        AlphaOffset = 1,
                        RedDisable = true,
                        GreenDisable = true,
                        BlueDisable = true,
                        Source = new InvertEffect { Source = borderEffect }
                    };
                }
                else
                {
                    var tintEffect = _tintEffect = new TintEffect
                    {
                        Name = "Tint",
                        Source = borderEffect,
                        Color = Color.FromArgb((byte)(Intensity * 255), 0, 0, 0)
                    };

                    animatableProperties = new[] { "Tint.Color" };
                    effect = new BlendEffect
                    {
                        Background = tintEffect,
                        Foreground = new CompositionEffectSourceParameter("Backdrop"),
                        Mode = BlendEffectMode.Overlay
                    };
                }

                var backdrop = Window.Current.Compositor.CreateBackdropBrush();

                var borderEffectFactory = Window.Current.Compositor.CreateEffectFactory(effect, animatableProperties);
                var borderEffectBrush = borderEffectFactory.CreateBrush();
                borderEffectBrush.SetSourceParameter("Source", surfaceBrush);

                if (_tintEffect != null)
                {
                    borderEffectBrush.SetSourceParameter("Backdrop", backdrop);
                }

                CompositionBrush = borderEffectBrush;
            }
        }

        protected override void OnDisconnected()
        {
            _connected = false;
            _tintEffect = null;

            if (CompositionBrush != null)
            {
                CompositionBrush.Dispose();
                CompositionBrush = null;
            }

            if (Surface != null)
            {
                Surface.Dispose();
                Surface = null;
            }
        }

        private bool _connected;
        private bool _negative;
        private bool _recreate;
        private TintEffect _tintEffect;

        public void Update()
        {
            if (_connected && CompositionBrush != null && Surface != null)
            {
                if (_negative != IsInverted)
                {
                    _recreate = true;
                    OnConnected();
                    return;
                }

                var surface = Surface;
                var surfaceBrush = Window.Current.Compositor.CreateSurfaceBrush(surface);
                surfaceBrush.Stretch = CompositionStretch.None;

                if (CompositionBrush is CompositionEffectBrush effectBrush)
                {
                    effectBrush.SetSourceParameter("Source", surfaceBrush);
                    effectBrush.Properties.InsertColor("Tint.Color", Color.FromArgb((byte)(Intensity * 255), 0, 0, 0));
                }
            }
        }
    }
}
