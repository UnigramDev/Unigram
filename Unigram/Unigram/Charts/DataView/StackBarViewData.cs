//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Unigram.Charts.Data;
using Unigram.Common;
using Windows.UI;

namespace Unigram.Charts.DataView
{
    public class StackBarViewData : LineViewData
    {

        public readonly Paint unselectedPaint = new Paint();
        public Color blendColor;

        public StackBarViewData(ChartData.Line line)
            : base(line)
        {
            paint.StrokeWidth = 1;
            //paint.setStyle(Paint.Style.STROKE);
            //unselectedPaint.setStyle(Paint.Style.STROKE);
            //paint.setAntiAlias(false);
        }

        //public void updateColors()
        //{
        //    super.updateColors();
        //}

        public override void UpdateColors(ElementTheme theme)
        {
            base.UpdateColors(theme);

            if (Navigation.BootStrapper.Current.Resources.TryGet("ApplicationPageBackgroundThemeBrush", out SolidColorBrush brush))
            {
                blendColor = Extensions.blendARGB(brush.Color, lineColor, 0.3f);
            }
        }
    }
}
