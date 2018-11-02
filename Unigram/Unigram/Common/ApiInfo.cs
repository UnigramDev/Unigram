using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Common
{
    public static class ApiInfo
    {
        private static bool? _canRenderWebP;
        public static bool CanDecodeWebp => (_canRenderWebP = _canRenderWebP ?? ApiInformation.IsReadOnlyPropertyPresent("Windows.Graphics.Imaging.BitmapDecoder", "WebpDecoderId")) ?? false;

        private static bool? _canAddContextRequestedEvent;
        public static bool CanAddContextRequestedEvent => (_canAddContextRequestedEvent = _canAddContextRequestedEvent ?? ApiInformation.IsReadOnlyPropertyPresent("Windows.UI.Xaml.UIElement", "ContextRequestedEvent")) ?? false;

        private static bool? _canCheckTextTrimming;
        public static bool CanCheckTextTrimming => (_canCheckTextTrimming = _canCheckTextTrimming ?? ApiInformation.IsReadOnlyPropertyPresent("Windows.UI.Xaml.Controls.TextBlock", "IsTextTrimmed")) ?? false;

        private static bool? _canUseDirectComposition;
        public static bool CanUseDirectComposition => (_canUseDirectComposition = _canUseDirectComposition ?? ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7)) ?? false;

        public static TransitionCollection CreateSlideTransition()
        {
            //if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo", "Effect"))
            if (CanUseDirectComposition)
            {
                return new TransitionCollection { new NavigationThemeTransition { DefaultNavigationTransitionInfo = new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight } } };
            }

            return null;
        }
    }
}
