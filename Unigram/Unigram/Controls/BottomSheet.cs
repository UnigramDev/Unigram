using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System.Profile;
using Windows.UI.Xaml;

namespace Unigram.Controls
{
    public class BottomSheet : ContentDialogBase
    {
        protected override void UpdateView(Rect bounds)
        {
            if (BackgroundElement == null) return;

            if ((HorizontalAlignment == HorizontalAlignment.Stretch && VerticalAlignment == VerticalAlignment.Stretch) || (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile" && (bounds.Width < 500 || bounds.Height < 500)))
            {
                BackgroundElement.MinWidth = bounds.Width;
                BackgroundElement.MinHeight = 0;
                BackgroundElement.BorderThickness = new Thickness(0, 1, 0, 0);
                BackgroundElement.VerticalAlignment = VerticalAlignment.Bottom;
            }
            else
            {
                BackgroundElement.MinWidth = Math.Min(360, bounds.Width);
                BackgroundElement.MinHeight = Math.Min(0, bounds.Height);
                BackgroundElement.MaxWidth = Math.Min(360, bounds.Width);
                BackgroundElement.MaxHeight = Math.Min(500, bounds.Height);
                BackgroundElement.VerticalAlignment = VerticalAlignment.Center;

                if (BackgroundElement.MinWidth == bounds.Width && BackgroundElement.MinHeight == bounds.Height)
                {
                    BackgroundElement.BorderThickness = new Thickness(0);
                }
                else if (BackgroundElement.MinWidth == bounds.Width && BackgroundElement.MinHeight != bounds.Height)
                {
                    BackgroundElement.BorderThickness = new Thickness(0, 0, 0, 1);
                }
                else if (BackgroundElement.MinWidth != bounds.Width && BackgroundElement.MinHeight == bounds.Height)
                {
                    BackgroundElement.BorderThickness = new Thickness(1, 0, 1, 0);
                }
                else
                {
                    var left = 0;
                    var right = 0;
                    var top = 0;
                    var bottom = 0;
                    switch (HorizontalAlignment)
                    {
                        case HorizontalAlignment.Left:
                            left = 0;
                            right = 1;
                            break;
                        case HorizontalAlignment.Right:
                            left = 1;
                            right = 0;
                            break;
                        default:
                            left = 1;
                            right = 1;
                            break;
                    }
                    switch (VerticalAlignment)
                    {
                        case VerticalAlignment.Top:
                            top = 0;
                            bottom = 1;
                            break;
                        case VerticalAlignment.Bottom:
                            top = 1;
                            bottom = 0;
                            break;
                        default:
                            top = 1;
                            bottom = 1;
                            break;
                    }

                    BackgroundElement.BorderThickness = new Thickness(left, top, right, bottom);
                }
            }
        }

    }
}
