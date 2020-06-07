namespace Unigram.Services.Settings
{
    public class DiagnosticsSettings : SettingsServiceBase
    {
        public DiagnosticsSettings()
            : base("Diagnostics")
        {
        }

        private bool? _bubbleKnockout;
        public bool BubbleKnockout
        {
            get
            {
                if (_bubbleKnockout == null)
                    _bubbleKnockout = GetValueOrDefault("BubbleKnockout", false);

                return _bubbleKnockout ?? false;
            }
            set
            {
                _bubbleKnockout = value;
                AddOrUpdateValue("BubbleKnockout", value);
            }
        }
    }
}
