using Unigram.Native;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Common
{
    public static class ApiInfo
    {
        private static bool? _isStoreRelease;
        public static bool IsStoreRelease => _isStoreRelease ??= (Package.Current.SignatureKind == PackageSignatureKind.Store);

        public static bool IsPackagedRelease => !IsStoreRelease;

        private static bool? _canUseDirectComposition;
        public static bool CanUseDirectComposition => _canUseDirectComposition ??= ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7);

        private static bool? _canUseViewports;
        public static bool CanUseViewports => _canUseViewports ??= ApiInformation.IsEventPresent("Windows.UI.Xaml.FrameworkElement", "EffectiveViewportChanged");

        private static bool? _canUseWindowManagement;
        public static bool CanUseWindowManagement => _canUseWindowManagement ??= ApiInformation.IsTypePresent("Windows.UI.WindowManagement.DisplayRegion");

        private static bool? _canUnconstrainFromBounds;
        public static bool CanUnconstrainFromBounds => _canUnconstrainFromBounds ??= ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.Primitives.Popup", "ShouldConstrainToRootBounds");

        private static bool? _canCheckThreadAccess;
        public static bool CanCheckThreadAccess => _canCheckThreadAccess ??= ApiInformation.IsPropertyPresent("Windows.System.DispatcherQueue", "HasThreadAccess");

        private static bool? _isMediaSupported;
        public static bool IsMediaSupported => _isMediaSupported ??= NativeUtils.IsMediaSupported();

        private static ulong? _build;
        public static bool IsBuildOrGreater(ulong compare)
        {
            if (_build == null)
            {
                string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
                ulong version = ulong.Parse(deviceFamilyVersion);
                ulong build = (version & 0x00000000FFFF0000L) >> 16;

                _build = build;
            }

            return _build >= compare;
        }

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
        public static FlowDirection FlowDirection => _flowDirection ??= LoadFlowDirection();

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
