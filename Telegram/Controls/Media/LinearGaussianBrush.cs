//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Effects;
using System.Numerics;
using Telegram.Navigation;
using Windows.UI;
using Windows.UI.Composition;

namespace Telegram.Controls.Media
{
    public partial class LinearGaussianBrush : PowerSavingBrushBase
    {
        protected override CompositionBrush OnUpdateBrush()
        {
            var compositor = BootStrapper.Current.Compositor;

            var blurEffect = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 20,
                BorderMode = EffectBorderMode.Hard,
                Source = new CompositionEffectSourceParameter("Backdrop")
            };

            var effectFactory = compositor.CreateEffectFactory(blurEffect, new[] { "Blur.BlurAmount" });
            var effectBrush = effectFactory.CreateBrush();
            var backdrop = compositor.CreateBackdropBrush();
            effectBrush.SetSourceParameter("Backdrop", backdrop);

            var gradientBrush = compositor.CreateLinearGradientBrush();
            gradientBrush.StartPoint = new Vector2(0, 0);
            gradientBrush.EndPoint = new Vector2(0, 1);
            gradientBrush.ExtendMode = CompositionGradientExtendMode.Wrap;

            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(0, Colors.Transparent));
            gradientBrush.ColorStops.Add(compositor.CreateColorGradientStop(1, Colors.White));

            var maskBrush = compositor.CreateMaskBrush();
            maskBrush.Source = effectBrush;
            maskBrush.Mask = gradientBrush;

            return maskBrush;
        }
    }
}
