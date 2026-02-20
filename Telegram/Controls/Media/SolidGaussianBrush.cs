//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Telegram.Navigation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace Telegram.Controls.Media
{
    public partial class SolidGaussianBrush : PowerSavingBrushBase
    {
        protected override CompositionBrush OnUpdateBrush()
        {
            var gaussianBlur = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 30,
                Optimization = EffectOptimization.Speed,
                BorderMode = EffectBorderMode.Hard,
                Source = new CompositionEffectSourceParameter("Backdrop"),
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
                Color = TintColor
            };

            var compositeEffect = new CompositeEffect();
            compositeEffect.Mode = CanvasComposite.SourceOver;
            compositeEffect.Sources.Add(saturationEffect);
            compositeEffect.Sources.Add(tintColorEffect);

            var effectFactory = BootStrapper.Current.Compositor.CreateEffectFactory(compositeEffect);
            var backdrop = BootStrapper.Current.Compositor.CreateBackdropBrush();

            var brush = effectFactory.CreateBrush();
            brush.SetSourceParameter("Backdrop", backdrop);

            return brush;
        }

        #region TintColor

        public Color TintColor
        {
            get { return (Color)GetValue(TintColorProperty); }
            set { SetValue(TintColorProperty, value); }
        }

        public static readonly DependencyProperty TintColorProperty =
            DependencyProperty.Register("TintColor", typeof(Color), typeof(SolidGaussianBrush), new PropertyMetadata(default(Color)));

        #endregion
    }
}
