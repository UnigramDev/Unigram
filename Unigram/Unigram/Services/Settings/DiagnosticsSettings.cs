namespace Unigram.Services.Settings
{
    public class DiagnosticsSettings : SettingsServiceBase
    {
        public DiagnosticsSettings()
            : base("Diagnostics")
        {
        }

        private bool? _bubbleMeasureAlpha;
        public bool BubbleMeasureAlpha
        {
            get
            {
                if (_bubbleMeasureAlpha == null)
                    _bubbleMeasureAlpha = GetValueOrDefault("BubbleMeasureAlpha", true);

                return _bubbleMeasureAlpha ?? true;
            }
            set
            {
                _bubbleMeasureAlpha = value;
                AddOrUpdateValue("BubbleMeasureAlpha", value);
            }
        }
    }
}
