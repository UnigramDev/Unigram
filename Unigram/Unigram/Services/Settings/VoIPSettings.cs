namespace Unigram.Services.Settings
{
    public class VoIPSettings : SettingsServiceBase
    {
        public VoIPSettings()
            : base("VoIP")
        {
        }

        private string _inputDevice;
        public string InputDevice
        {
            get => _inputDevice ??= GetValueOrDefault("InputDevice", "default");
            set => AddOrUpdateValue(ref _inputDevice, "InputDevice", value);
        }

        private string _outputDevice;
        public string OutputDevice
        {
            get => _outputDevice ??= GetValueOrDefault("OutputDevice", "default");
            set => AddOrUpdateValue(ref _outputDevice, "OutputDevice", value);
        }

        private string _videoDevice;
        public string VideoDevice
        {
            get => _videoDevice ??= GetValueOrDefault("VideoDevice", "default");
            set => AddOrUpdateValue(ref _videoDevice, "VideoDevice", value);
        }
    }
}
