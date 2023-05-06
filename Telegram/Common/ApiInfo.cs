//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Native;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.System.Profile;

namespace Telegram.Common
{
    public static class ApiInfo
    {
        private static bool? _isStoreRelease;
        public static bool IsStoreRelease => _isStoreRelease ??= (Package.Current.SignatureKind == PackageSignatureKind.Store);

        public static bool IsPackagedRelease => !IsStoreRelease;

        private static bool? _isDesktop;
        public static bool IsDesktop => _isDesktop ??= string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Desktop");

        private static bool? _isMediaSupported;
        public static bool IsMediaSupported => _isMediaSupported ??= NativeUtils.IsMediaSupported();

        private static bool? _hasDownloadFolder;
        public static bool HasDownloadFolder => _hasDownloadFolder ??= IsDesktop;

        public static bool HasCacheOnly => !HasDownloadFolder;

        private static bool? _hasKnownFolders;
        public static bool HasKnownFolders => _hasKnownFolders ??= ApiInformation.IsEnumNamedValuePresent("Windows.Storage.KnownFolderId", "DownloadsFolder");

        private static bool? _isVoipSupported;
        public static bool IsVoipSupported => _isVoipSupported ??= ApiInformation.IsApiContractPresent("Windows.ApplicationModel.Calls.CallsVoipContract", 1);

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
    }
}
