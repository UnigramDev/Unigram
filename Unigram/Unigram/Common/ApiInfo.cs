using Unigram.Native;
using Windows.ApplicationModel;
using Windows.System.Profile;
using Windows.UI.Xaml;

namespace Unigram.Common
{
    public static class ApiInfo
    {
        private static bool? _isStoreRelease;
        public static bool IsStoreRelease => _isStoreRelease ??= (Package.Current.SignatureKind == PackageSignatureKind.Store);

        public static bool IsPackagedRelease => !IsStoreRelease;

        private static bool? _isMediaSupported;
        public static bool IsMediaSupported => _isMediaSupported ??= NativeUtils.IsMediaSupported();

        private static bool? _isWindows11;
        public static bool IsWindows11 => _isWindows11 ??= IsBuildOrGreater(22000);

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
