//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Native;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.System.Profile;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Common
{
    public static class ApiInfo
    {
        private static bool? _isStoreRelease;
        public static bool IsStoreRelease => _isStoreRelease ??= (Package.Current.SignatureKind == PackageSignatureKind.Store);

        public static bool IsPackagedRelease => !IsStoreRelease;

        private static bool? _isDesktop;
        public static bool IsDesktop => _isDesktop ??= string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Desktop");

        private static bool? _isXbox;
        public static bool IsXbox => _isXbox ??= string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Xbox");

        private static bool? _isMediaSupported;
        public static bool IsMediaSupported => _isMediaSupported ??= NativeUtils.IsMediaSupported();

        private static bool? _hasDownloadFolder;
        public static bool HasDownloadFolder => _hasDownloadFolder ??= IsDesktop;

        public static bool HasCacheOnly => !HasDownloadFolder;

        public static bool HasMultipleViews => !IsXbox;

        private static bool? _hasKnownFolders;
        public static bool HasKnownFolders => _hasKnownFolders ??= ApiInformation.IsEnumNamedValuePresent("Windows.Storage.KnownFolderId", "DownloadsFolder");

        private static bool? _isVoipSupported;
        public static bool IsVoipSupported => _isVoipSupported ??= ApiInformation.IsApiContractPresent("Windows.ApplicationModel.Calls.CallsVoipContract", 1);

        private static bool? _canCreateRectangleClip;
        public static bool CanCreateRectangleClip => _canCreateRectangleClip ??= ApiInformation.IsMethodPresent("Windows.UI.Composition.Compositor", "CreateRectangleClip");

        // We only enable shadows on Windows 11 for three reasons:
        // First: they look terrible on Windows 10
        // Second: they are way more optimized on Windows 11 (they use a nine-grid instead of dynamically casted shadows)
        // Third: there seems to be no way to create a custom shadow that only casts below messages without overlaps
        private static bool? _canCreateThemeShadow;
        public static bool CanCreateThemeShadow => IsWindows11 && (_canCreateThemeShadow ??= ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "Shadow"));

        private static bool? _canAnimatePaths;
        public static bool CanAnimatePaths => _canAnimatePaths ??= IsBuildOrGreater(19043);

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

        public static NavigationCacheMode NavigationCacheMode => IsXbox
                ? NavigationCacheMode.Disabled
                : Constants.DEBUG
                ? NavigationCacheMode.Enabled
                : NavigationCacheMode.Enabled;
    }
}
