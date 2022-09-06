using Unigram.Common;

namespace Unigram.Services.Settings
{
    public class DiagnosticsSettings : SettingsServiceBase
    {
        public DiagnosticsSettings()
            : base("Diagnostics")
        {
        }

        private bool? _disableDatabase;
        public bool DisableDatabase
        {
            get => _disableDatabase ??= GetValueOrDefault("DisableDatabase", false);
            set => AddOrUpdateValue(ref _disableDatabase, "DisableDatabase", value);
        }

        private bool? _copyFormattedCode;
        public bool CopyFormattedCode
        {
            get => _copyFormattedCode ??= GetValueOrDefault("CopyFormattedCode", true);
            set => AddOrUpdateValue(ref _copyFormattedCode, "CopyFormattedCode", value);
        }

        private bool? _minithumbnails;
        public bool Minithumbnails
        {
            get => _minithumbnails ??= GetValueOrDefault("Minithumbnails", true);
            set => AddOrUpdateValue(ref _minithumbnails, "Minithumbnails", value);
        }

        private bool? _allowRightToLeft;
        public bool AllowRightToLeft
        {
            get => _allowRightToLeft ??= GetValueOrDefault("AllowRightToLeft", ApiInfo.IsPackagedRelease);
            set => AddOrUpdateValue(ref _allowRightToLeft, "AllowRightToLeft", value);
        }

        private bool? _lowLatencyGC;
        public bool LowLatencyGC
        {
            get => _lowLatencyGC ??= GetValueOrDefault("LowLatencyGC", ApiInfo.IsPackagedRelease);
            set => AddOrUpdateValue(ref _lowLatencyGC, "LowLatencyGC", value);
        }

        private string _lastErrorMessage;
        public string LastErrorMessage
        {
            get => _lastErrorMessage ??= GetValueOrDefault("LastErrorMessage", string.Empty);
            set => AddOrUpdateValue(ref _lastErrorMessage, "LastErrorMessage", value);
        }

        private string _lastErrorProperties;
        public string LastErrorProperties
        {
            get => _lastErrorProperties ??= GetValueOrDefault("LastErrorProperties", string.Empty);
            set => AddOrUpdateValue(ref _lastErrorProperties, "LastErrorProperties", value);
        }

        private int? _lastErrorVersion;
        public int LastErrorVersion
        {
            get => _lastErrorVersion ??= GetValueOrDefault("LastErrorVersion", 0);
            set => AddOrUpdateValue(ref _lastErrorVersion, "LastErrorVersion", value);
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

        public bool IsLastErrorDiskFull { get; set; }
    }
}
