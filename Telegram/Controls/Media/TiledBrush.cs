//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Effects;
using System.Numerics;
using Windows.Graphics.Effects;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Media
{
    public class TiledBrush : XamlCompositionBrushBase
    {
        public LoadedImageSurface ImageSource { get; set; }

        public bool IsNegative { get; set; }

        public byte Intensity { get; set; } = 255;

        protected override void OnConnected()
        {
            _connected = true;
            _negative = IsNegative;

            if (_recreate || (CompositionBrush == null && ImageSource != null))
            {
                _recreate = false;

                var surface = ImageSource;
                var logical = surface.DecodedSize.ToVector2();
                var physical = surface.DecodedPhysicalSize.ToVector2();

                var surfaceBrush = Window.Current.Compositor.CreateSurfaceBrush(surface);
                surfaceBrush.Stretch = CompositionStretch.None;
                surfaceBrush.SnapToPixels = true;
                surfaceBrush.Scale = logical / physical;

                var borderEffect = new BorderEffect()
                {
                    Source = new CompositionEffectSourceParameter("Source"),
                    ExtendX = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap,
                    ExtendY = Microsoft.Graphics.Canvas.CanvasEdgeBehavior.Wrap
                };

                IGraphicsEffect effect;
                IGraphicsEffect blend;
                if (IsNegative)
                {
                    var tintEffect = _tintEffect = new TintEffect
                    {
                        Name = "Tint",
                        Source = borderEffect,
                        Color = Color.FromArgb(Intensity, 0, 0, 0)
                    };

                    blend = null;

                    effect = new ColorMatrixEffect
                    {
                        Source = tintEffect,
                        ColorMatrix = new Matrix5x4
                        {
                            M11 = 1,
                            M22 = 1,
                            M33 = 1,
                            M44 = -1,
                            M54 = 1
                        }
                    };
                }
                else
                {
                    var tintEffect = _tintEffect = new TintEffect
                    {
                        Name = "Tint",
                        Source = borderEffect,
                        Color = Color.FromArgb(Intensity, 0, 0, 0)
                    };

                    effect = blend = new BlendEffect
                    {
                        Background = tintEffect,
                        Foreground = new CompositionEffectSourceParameter("Backdrop"),
                        Mode = BlendEffectMode.Overlay
                    };

                    //effect = borderEffect;
                }

                var borderEffectFactory = Window.Current.Compositor.CreateEffectFactory(effect, new[] { "Tint.Color" });
                var borderEffectBrush = borderEffectFactory.CreateBrush();
                borderEffectBrush.SetSourceParameter("Source", surfaceBrush);

                if (blend != null)
                {
                    var backdrop = Window.Current.Compositor.CreateBackdropBrush();
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

            if (ImageSource != null)
            {
                //ImageSource.Dispose();
                //ImageSource = null;
            }
        }

        private bool _connected;
        private bool _negative;
        private bool _recreate;
        private TintEffect _tintEffect;

        public void Update()
        {
            if (_connected && CompositionBrush != null && ImageSource != null)
            {
                if (_negative != IsNegative)
                {
                    _recreate = true;
                    OnConnected();
                    return;
                }

                try
                {
                    var surface = ImageSource;
                    var logical = surface.DecodedSize.ToVector2();
                    var physical = surface.DecodedPhysicalSize.ToVector2();

                    var surfaceBrush = Window.Current.Compositor.CreateSurfaceBrush(surface);
                    surfaceBrush.Stretch = CompositionStretch.None;
                    surfaceBrush.SnapToPixels = true;
                    surfaceBrush.Scale = logical / physical;

                    if (CompositionBrush is CompositionEffectBrush effectBrush)
                    {
                        effectBrush.SetSourceParameter("Source", surfaceBrush);

                        if (_tintEffect != null)
                        {
                            effectBrush.Properties.InsertColor("Tint.Color", Color.FromArgb(Intensity, 0, 0, 0));
                        }
                    }
                }
                catch
                {
                    _recreate = true;
                    OnConnected();
                }
            }
        }
    }
}
