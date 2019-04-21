using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation.Metadata;
using Windows.Graphics.Imaging;
using Windows.System.Profile;
using Windows.UI.Xaml;
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

        private static bool? _canUseAccelerators;
        public static bool CanUseAccelerators => (_canUseAccelerators = _canUseAccelerators ?? ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "KeyboardAccelerators")) ?? false;

        private static bool? _isFullExperience;
        public static bool IsFullExperience => (_isFullExperience = _isFullExperience ?? AnalyticsInfo.VersionInfo.DeviceFamily != "Windows.Mobile") ?? true;

        public static TransitionCollection CreateSlideTransition()
        {
            //if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Media.Animation.SlideNavigationTransitionInfo", "Effect"))
            if (CanUseDirectComposition)
            {
                return new TransitionCollection { new NavigationThemeTransition { DefaultNavigationTransitionInfo = new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight } } };
            }

            return null;
        }



        private static FlowDirection? _flowDirection;
        public static FlowDirection FlowDirection => (_flowDirection = _flowDirection ?? LoadFlowDirection()) ?? FlowDirection.LeftToRight;

        private static FlowDirection LoadFlowDirection()
        {
#if DEBUG
            var flowDirectionSetting = ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];
            return flowDirectionSetting == "RTL" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
#else
            return FlowDirection.LeftToRight;
#endif
        }
    }
}
