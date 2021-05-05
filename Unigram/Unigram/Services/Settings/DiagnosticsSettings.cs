namespace Unigram.Services.Settings
{
    public class DiagnosticsSettings : SettingsServiceBase
    {
        public DiagnosticsSettings()
            : base("Diagnostics")
        {
        }

        private bool? _minithumbnails;
        public bool Minithumbnails
        {
            get => _minithumbnails ??= GetValueOrDefault("Minithumbnails", true);
            set => AddOrUpdateValue(ref _minithumbnails, "Minithumbnails", value);
        }

        private string _lastErrorMessage;
        public string LastErrorMessage
        {
            get => _lastErrorMessage ??= GetValueOrDefault("LastErrorMessage", string.Empty);
            set => AddOrUpdateValue(ref _lastErrorMessage, "LastErrorMessage", value);
        }

        public bool IsLastErrorDiskFull { get; set; }
    }
}
