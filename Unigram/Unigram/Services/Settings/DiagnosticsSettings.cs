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

        private bool? _minithumbnails;
        public bool Minithumbnails
        {
            get
            {
                if (_minithumbnails == null)
                    _minithumbnails = GetValueOrDefault("Minithumbnails", true);

                return _minithumbnails ?? true;
            }
            set
            {
                _minithumbnails = value;
                AddOrUpdateValue("Minithumbnails", value);
            }
        }
    }
}
