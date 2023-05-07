//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using System;
using Telegram.Common;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Media
{
    public class SolidGaussianBrush : XamlCompositionBrushBase
    {
        private bool m_isConnected;
        private CompositionBrush m_brush;

        public SolidGaussianBrush()
        {
            PowerSavingPolicy.Changed += PowerSavingPolicy_Changed;
        }

        private void PowerSavingPolicy_Changed(object sender, System.EventArgs e)
        {
            if (m_isConnected)
            {
                try
                {
                    UpdateBrush();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }

        private void UpdateBrush()
        {
            if (m_brush is CompositionEffectBrush && !PowerSavingPolicy.AreMaterialsEnabled)
            {
                m_brush.Dispose();
                m_brush = null;
            }
            else if (m_brush is CompositionColorBrush && PowerSavingPolicy.AreMaterialsEnabled)
            {
                m_brush.Dispose();
                m_brush = null;
            }

            if (m_brush == null)
            {
                if (!PowerSavingPolicy.AreMaterialsEnabled)
                {
                    m_brush = Window.Current.Compositor.CreateColorBrush(FallbackColor);
                    CompositionBrush = m_brush;
                }
                else
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

                    var brush = effectFactory.CreateBrush();
                    brush.SetSourceParameter("Backdrop", backdrop);

                    m_brush = brush;
                    CompositionBrush = m_brush;
                }
            }
        }

        protected override void OnConnected()
        {
            try
            {
                UpdateBrush();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            m_isConnected = true;
            base.OnConnected();
        }

        protected override void OnDisconnected()
        {
            m_isConnected = false;

            if (m_brush != null)
            {
                m_brush.Dispose();
                m_brush = null;
            }

            CompositionBrush = null;

            base.OnDisconnected();
        }
    }
}
