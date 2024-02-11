//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

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

        private bool? _bridgeDebug;
        public bool BridgeDebug
        {
            get => _bridgeDebug ??= GetValueOrDefault("BridgeDebug", false);
            set => AddOrUpdateValue(ref _bridgeDebug, "BridgeDebug", value);
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

        private bool? _lastCrashWasLayoutCycle;
        public bool LastCrashWasLayoutCycle
        {
            get => _lastCrashWasLayoutCycle ??= GetValueOrDefault("LastCrashWasLayoutCycle", false);
            set => AddOrUpdateValue(ref _lastCrashWasLayoutCycle, "LastCrashWasLayoutCycle", value);
        }

        private DateTime? _lasCrashReported;
        public DateTime LastCrashReported
        {
            get => _lasCrashReported ??= DateTime.FromFileTimeUtc(GetValueOrDefault("LastCrashReported", 2650467743999999999 /* DateTime.MaxValue */));
            set
            {
                _lasCrashReported = value;
                AddOrUpdateValue("LastCrashReported", value.ToFileTimeUtc());
            }
        }

        private bool? _hidePhoneNumber;
        public bool HidePhoneNumber
        {
            get => _hidePhoneNumber ??= GetValueOrDefault("HidePhoneNumber", Constants.DEBUG);
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

        private bool? _forceEdgeHtml;
        public bool ForceEdgeHtml
        {
            get => _forceEdgeHtml ??= GetValueOrDefault("ForceEdgeHtml", false);
            set => AddOrUpdateValue(ref _forceEdgeHtml, "ForceEdgeHtml", value);
        }

        private bool? _disablePackageManager;
        public bool DisablePackageManager
        {
            get => _disablePackageManager ??= GetValueOrDefault("DisablePackageManager", false);
            set => AddOrUpdateValue(ref _disablePackageManager, "DisablePackageManager", value);
        }

        private bool? _sendLargePhotos;
        public bool SendLargePhotos
        {
            get => _sendLargePhotos ??= GetValueOrDefault("SendLargePhotos", false);
            set => AddOrUpdateValue(ref _sendLargePhotos, "SendLargePhotos", value);
        }

        public bool IsLastErrorDiskFull { get; set; }
    }
}
