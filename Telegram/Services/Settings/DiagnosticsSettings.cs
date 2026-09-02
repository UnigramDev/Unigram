//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Composition;

namespace Telegram.Services.Settings
{
    public partial class DiagnosticsSettings : SettingsServiceBase
    {
        public DiagnosticsSettings()
            : base("Diagnostics")
        {
        }

        private int? _messageDust;
        public MessageDustEffect MessageDust
        {
            get => (MessageDustEffect)(_messageDust ??= GetValueOrDefault("MessageDust", (int)MessageDustEffect.Layers));
            set => AddOrUpdateValue(ref _messageDust, "MessageDust", (int)value);
        }

        private bool? _legacyScrollBars;
        public bool LegacyScrollBars
        {
            get => _legacyScrollBars ??= GetValueOrDefault("LegacyScrollBars", false);
            set => AddOrUpdateValue(ref _legacyScrollBars, "LegacyScrollBars", value);
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

        private bool? _measureDeserialization;
        public bool MeasureDeserialization
        {
            get => _measureDeserialization ??= GetValueOrDefault("MeasureDeserialization", false);
            set
            {
                AddOrUpdateValue(ref _measureDeserialization, "MeasureDeserialization", value);

                // Assigned here as well as at startup so the checkbox takes effect on the next
                // update rather than the next session.
                TdThroughput.Enabled = value;
            }
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

        private bool? _forceWebView2;
        public bool ForceWebView2
        {
            get => _forceWebView2 ??= GetValueOrDefault("ForceWebView2", false);
            set => AddOrUpdateValue(ref _forceWebView2, "ForceWebView2", value);
        }

        private bool? _disablePackageManager;
        public bool DisablePackageManager
        {
            get => _disablePackageManager ??= GetValueOrDefault("DisablePackageManager", false);
            set => AddOrUpdateValue(ref _disablePackageManager, "DisablePackageManager", value);
        }

        private bool? _disableXamlGcCollect;
        public bool DisableXamlGcCollect
        {
            get => _disableXamlGcCollect ??= GetValueOrDefault("DisableXamlGcCollect", false);
            set => AddOrUpdateValue(ref _disableXamlGcCollect, "DisableXamlGcCollect", value);
        }

        private bool? _disableMemoryPressure;
        public bool DisableMemoryPressure
        {
            get => _disableMemoryPressure ??= GetValueOrDefault("DisableMemoryPressure", true);
            set => AddOrUpdateValue(ref _disableMemoryPressure, "DisableMemoryPressure", value);
        }

        private bool? _forumTabsDebug;
        public bool ForumTabsDebug
        {
            get => _forumTabsDebug ??= GetValueOrDefault("ForumTabsDebug", Constants.DEBUG);
            set => AddOrUpdateValue(ref _forumTabsDebug, "ForumTabsDebug", value);
        }

        private bool? _bubbleElevationDebug;
        public bool BubbleElevationDebug
        {
            get => _bubbleElevationDebug ??= GetValueOrDefault("BubbleElevationDebug", true);
            set => AddOrUpdateValue(ref _bubbleElevationDebug, "BubbleElevationDebug", value);
        }

        // Off by default, unlike its neighbours: it costs a set insertion on every chat read in
        // the app, and it answers a question asked once rather than a setting anyone lives with.
        private bool? _chatReadsDebug;
        public bool ChatReadsDebug
        {
            get => _chatReadsDebug ??= GetValueOrDefault("ChatReadsDebug", false);
            set => AddOrUpdateValue(ref _chatReadsDebug, "ChatReadsDebug", value);
        }

        private bool? _bubbleRecyclingDebug;
        public bool BubbleRecyclingDebug
        {
            get => _bubbleRecyclingDebug ??= GetValueOrDefault("BubbleRecyclingDebug", true);
            set => AddOrUpdateValue(ref _bubbleRecyclingDebug, "BubbleRecyclingDebug", value);
        }

        private bool? _bubbleContentRecyclingDebug;
        public bool BubbleContentRecyclingDebug
        {
            get => _bubbleContentRecyclingDebug ??= GetValueOrDefault("BubbleContentRecyclingDebug", ApiInfo.IsPackagedRelease);
            set => AddOrUpdateValue(ref _bubbleContentRecyclingDebug, "BubbleContentRecyclingDebug", value);
        }

        private bool? _windowResizeDebug;
        public bool WindowResizeDebug
        {
            get => _windowResizeDebug ??= GetValueOrDefault("WindowResizeDebug", Constants.DEBUG);
            set => AddOrUpdateValue(ref _windowResizeDebug, "WindowResizeDebug", value);
        }

        private bool? _deleteFilesDebug;
        public bool DeleteFilesDebug
        {
            get => _deleteFilesDebug ??= GetValueOrDefault("DeleteFilesDebug", Constants.DEBUG);
            set => AddOrUpdateValue(ref _deleteFilesDebug, "DeleteFilesDebug", value);
        }

        private bool? _albumPreloadDebug;
        public bool AlbumPreloadDebug
        {
            get => _albumPreloadDebug ??= GetValueOrDefault("AlbumPreloadDebug", ApiInfo.IsPackagedRelease);
            set => AddOrUpdateValue(ref _albumPreloadDebug, "AlbumPreloadDebug", value);
        }

        private bool? _videoPreloadDebug;
        public bool VideoPreloadDebug
        {
            get => _videoPreloadDebug ??= GetValueOrDefault("VideoPreloadDebug", false);
            set => AddOrUpdateValue(ref _videoPreloadDebug, "VideoPreloadDebug", value);
        }

        private bool? _connectedAnimationsDebug;
        public bool ConnectedAnimationsDebug
        {
            get => _connectedAnimationsDebug ??= GetValueOrDefault("ConnectedAnimationsDebug", ApiInfo.IsPackagedRelease);
            set => AddOrUpdateValue(ref _connectedAnimationsDebug, "ConnectedAnimationsDebug", value);
        }

        private bool? _useTlottieRenderer;
        public bool UseTLottieRenderer
        {
            get => _useTlottieRenderer ??= GetValueOrDefault("UseTLottieRenderer", ApiInfo.IsPackagedRelease);
            set => AddOrUpdateValue(ref _useTlottieRenderer, "UseTLottieRenderer", value);
        }

        public bool IsLastErrorDiskFull { get; set; }
    }
}
