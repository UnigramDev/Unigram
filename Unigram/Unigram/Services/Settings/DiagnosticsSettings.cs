using Windows.ApplicationModel;

namespace Unigram.Services.Settings
{
    public class DiagnosticsSettings : SettingsServiceBase
    {
        public DiagnosticsSettings()
            : base("Diagnostics")
        {
        }

        private bool? _bubbleAnimations;
        public bool BubbleAnimations
        {
            get
            {
                if (_bubbleAnimations == null)
                    _bubbleAnimations = GetValueOrDefault("BubbleAnimations", Package.Current.SignatureKind != PackageSignatureKind.Store);

                return _bubbleAnimations ?? Package.Current.SignatureKind != PackageSignatureKind.Store;
            }
            set
            {
                _bubbleAnimations = value;
                AddOrUpdateValue("BubbleAnimations", value);
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
