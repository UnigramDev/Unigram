﻿using Unigram.Charts.Data;
using Unigram.Common;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

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

            if (App.Current.Resources.TryGet("ApplicationPageBackgroundThemeBrush", out SolidColorBrush brush))
            {
                blendColor = Extensions.blendARGB(brush.Color, lineColor, 0.3f);
            }
        }
    }
}
