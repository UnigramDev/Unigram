using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Core.Services;
using Unigram.Views;
using Windows.Foundation.Metadata;
using Windows.Phone.Devices.Notification;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class VisualUtilities
    {
        public static async void ShakeView(FrameworkElement view, float x = 2, bool vibrate = false)
        {
            // We use first child inside the control (usually a Grid)
            // so we don't have to worry about absolute offset
            var inner = VisualTreeHelper.GetChild(view, 0) as FrameworkElement;
            var visual = ElementCompositionPreview.GetElementVisual(inner);

            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(50 * 6);

            for (int i = 1; i < 6; i++)
            {
                x = -x;
                animation.InsertKeyFrame(i * (1f / 5f), i == 5 ? 0 : x);
            }

            animation.InsertKeyFrame(0, 0);
            animation.InsertKeyFrame(1, 0);

            visual.StartAnimation("Offset.X", animation);

            if (vibrate)
            {
                var service = UnigramContainer.Current.ResolveType<IVibrationService>();
                await service.VibrateAsync();
            }
        }
    }
}
