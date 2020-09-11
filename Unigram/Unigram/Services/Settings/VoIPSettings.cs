using Windows.Storage;

namespace Unigram.Services.Settings
{
    public class VoIPSettings : SettingsServiceBase
    {
        public VoIPSettings()
            : base(ApplicationData.Current.LocalSettings.CreateContainer("VoIP", ApplicationDataCreateDisposition.Always))
        {
        }

        private string _inputDevice;
        public string InputDevice
        {
            get
            {
                if (_inputDevice == null)
                    _inputDevice = GetValueOrDefault("InputDevice", "default");

                return _inputDevice ?? "default";
            }
            set
            {
                _inputDevice = value;
                AddOrUpdateValue("InputDevice", value);
            }
        }

        private string _outputDevice;
        public string OutputDevice
        {
            get
            {
                if (_outputDevice == null)
                    _outputDevice = GetValueOrDefault("OutputDevice", "default");

                return _outputDevice ?? "default";
            }
            set
            {
                _outputDevice = value;
                AddOrUpdateValue("OutputDevice", value);
            }
        }

        private string _videoDevice;
        public string VideoDevice
        {
            get
            {
                if (_videoDevice == null)
                    _videoDevice = GetValueOrDefault("VideoDevice", "default");

                return _videoDevice ?? "default";
            }
            set
            {
                _videoDevice = value;
                AddOrUpdateValue("VideoDevice", value);
            }
        }
    }
}
