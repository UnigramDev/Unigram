//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Services.Settings
{
    public class DiagnosticsSettings : SettingsServiceBase
    {
        public DiagnosticsSettings()
            : base("Diagnostics")
        {
        }

        private bool? _legacyScrollBars;
        public bool LegacyScrollBars
        {
            get => _legacyScrollBars ??= GetValueOrDefault("LegacyScrollBars", true);
            set => AddOrUpdateValue(ref _legacyScrollBars, "LegacyScrollBars", value);
        }

        private bool? _legacyScrollViewers;
        public bool LegacyScrollViewers
        {
            get => _legacyScrollViewers ??= GetValueOrDefault("LegacyScrollViewers", true);
            set => AddOrUpdateValue(ref _legacyScrollViewers, "LegacyScrollViewers", value);
        }

        private bool? _disableDatabase;
        public bool DisableDatabase
        {
            get => _disableDatabase ??= GetValueOrDefault("DisableDatabase", false);
            set => AddOrUpdateValue(ref _disableDatabase, "DisableDatabase", value);
        }

        private bool? _allowRightToLeft;
        public bool AllowRightToLeft
        {
            get => _allowRightToLeft ??= GetValueOrDefault("AllowRightToLeft", false);
            set => AddOrUpdateValue(ref _allowRightToLeft, "AllowRightToLeft", value);
        }

        private string? _deviceName;
        public string DeviceName
        {
            get => _deviceName ??= GetValueOrDefault("DeviceName", string.Empty);
            set => AddOrUpdateValue(ref _deviceName, "DeviceName", value);
        }

        private int? _updateCount;
        public int UpdateCount
        {
            get => _updateCount ??= GetValueOrDefault("UpdateCount", 0);
            set => AddOrUpdateValue(ref _updateCount, "UpdateCount", value);
        }

        private int? _lastUpdateVersion;
        public int LastUpdateVersion
        {
            get => _lastUpdateVersion ??= GetValueOrDefault("LastUpdateVersion", 0);
            set => AddOrUpdateValue(ref _lastUpdateVersion, "LastUpdateVersion", value);
        }

        private int? _lastUpdateTime;
        public int LastUpdateTime
        {
            get => _lastUpdateTime ??= GetValueOrDefault("LastUpdateTime", 0);
            set => AddOrUpdateValue(ref _lastUpdateTime, "LastUpdateTime", value);
        }

        private bool? _enableWebViewDevTools;
        public bool EnableWebViewDevTools
        {
            get => _enableWebViewDevTools ??= GetValueOrDefault("EnableWebViewDevTools", Constants.DEBUG);
            set => AddOrUpdateValue(ref _enableWebViewDevTools, "EnableWebViewDevTools", value);
        }

        private long? _storageMaxTimeFromLastAccess;
        public long StorageMaxTimeFromLastAccess
        {
            get => _storageMaxTimeFromLastAccess ??= GetValueOrDefault("StorageMaxTimeFromLastAccess", 0L);
            set => AddOrUpdateValue(ref _storageMaxTimeFromLastAccess, "StorageMaxTimeFromLastAccess", value);
        }

        private bool? _useStorageOptimizer;
        public bool UseStorageOptimizer
        {
            get => _useStorageOptimizer ??= GetValueOrDefault("UseStorageOptimizer", false);
            set => AddOrUpdateValue(ref _useStorageOptimizer, "UseStorageOptimizer", value);
        }

        private bool? _useLayoutRounding;
        public bool UseLayoutRounding
        {
            get => _useLayoutRounding ??= GetValueOrDefault("UseLayoutRounding", true);
            set => AddOrUpdateValue(ref _useLayoutRounding, "UseLayoutRounding", value);
        }

        private bool? _lastCrashWasLayoutCycle;
        public bool LastCrashWasLayoutCycle
        {
            get => _lastCrashWasLayoutCycle ??= GetValueOrDefault("LastCrashWasLayoutCycle", false);
            set => AddOrUpdateValue(ref _lastCrashWasLayoutCycle, "LastCrashWasLayoutCycle", value);
        }

        private bool? _hidePhoneNumber;
        public bool HidePhoneNumber
        {
            get => _hidePhoneNumber ??= GetValueOrDefault("HidePhoneNumber", false);
            set => AddOrUpdateValue(ref _hidePhoneNumber, "HidePhoneNumber", value);
        }

        private bool? _showMemoryUsage;
        public bool ShowMemoryUsage
        {
            get => _showMemoryUsage ??= GetValueOrDefault("ShowMemoryUsage", false);
            set => AddOrUpdateValue(ref _showMemoryUsage, "ShowMemoryUsage", value);
        }

        private bool? _showIds;
        public bool ShowIds
        {
            get => _showIds ??= GetValueOrDefault("ShowIds", false);
            set => AddOrUpdateValue(ref _showIds, "ShowIds", value);
        }

        private bool? _forceRawAudio;
        public bool ForceRawAudio
        {
            get => _forceRawAudio ??= GetValueOrDefault("ForceRawAudio", false);
            set => AddOrUpdateValue(ref _forceRawAudio, "ForceRawAudio", value);
        }

        public bool IsLastErrorDiskFull { get; set; }
    }
}
