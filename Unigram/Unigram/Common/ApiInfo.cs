using Unigram.Native;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Common
{
    public static class ApiInfo
    {
        private static bool? _canAddContextRequestedEvent;
        public static bool CanAddContextRequestedEvent => (_canAddContextRequestedEvent = _canAddContextRequestedEvent ?? ApiInformation.IsReadOnlyPropertyPresent("Windows.UI.Xaml.UIElement", "ContextRequestedEvent")) ?? false;

        private static bool? _canCheckTextTrimming;
        public static bool CanCheckTextTrimming => (_canCheckTextTrimming = _canCheckTextTrimming ?? ApiInformation.IsReadOnlyPropertyPresent("Windows.UI.Xaml.Controls.TextBlock", "IsTextTrimmed")) ?? false;

        private static bool? _canUseDirectComposition;
        public static bool CanUseDirectComposition => (_canUseDirectComposition = _canUseDirectComposition ?? ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7)) ?? false;

        private static bool? _canUseAccelerators;
        public static bool CanUseAccelerators => (_canUseAccelerators = _canUseAccelerators ?? ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "KeyboardAccelerators")) ?? false;

        private static bool? _canUseViewports;
        public static bool CanUseViewports => (_canUseViewports = _canUseViewports ?? ApiInformation.IsEventPresent("Windows.UI.Xaml.FrameworkElement", "EffectiveViewportChanged")) ?? false;

        private static bool? _canUseWindowManagement;
        public static bool CanUseWindowManagement => (_canUseWindowManagement = _canUseWindowManagement ?? ApiInformation.IsTypePresent("Windows.UI.WindowManagement.DisplayRegion")) ?? false;

        private static bool? _canUnconstrainFromBounds;
        public static bool CanUnconstrainFromBounds => (_canUnconstrainFromBounds = _canUnconstrainFromBounds ?? ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.Primitives.Popup", "ShouldConstrainToRootBounds")) ?? false;

        private static bool? _isMediaSupported;
        public static bool IsMediaSupported => (_isMediaSupported = _isMediaSupported ?? NativeUtils.IsMediaSupported()) ?? true;

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
            var flowDirectionSetting = Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];
            return flowDirectionSetting == "RTL" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
#else
            return FlowDirection.LeftToRight;
#endif
        }
    }
}
