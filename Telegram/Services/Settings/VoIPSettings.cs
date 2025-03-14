//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Services.Settings
{
    public partial class VoIPSettings : SettingsServiceBase
    {
        public VoIPSettings()
            : base("VoIP")
        {
        }

        private string _inputDevice;
        public string InputDevice
        {
            get => _inputDevice ??= GetValueOrDefault("InputDevice2", "");
            set => AddOrUpdateValue(ref _inputDevice, "InputDevice2", value);
        }

        private string _outputDevice;
        public string OutputDevice
        {
            get => _outputDevice ??= GetValueOrDefault("OutputDevice2", "");
            set => AddOrUpdateValue(ref _outputDevice, "OutputDevice2", value);
        }

        private string _videoDevice;
        public string VideoDevice
        {
            get => _videoDevice ??= GetValueOrDefault("VideoDevice2", "");
            set => AddOrUpdateValue(ref _videoDevice, "VideoDevice2", value);
        }

        private bool? _isNoiseSuppressionEnabled;
        public bool IsNoiseSuppressionEnabled
        {
            get => _isNoiseSuppressionEnabled ??= GetValueOrDefault("IsNoiseSuppressionEnabled", true);
            set => AddOrUpdateValue(ref _isNoiseSuppressionEnabled, "IsNoiseSuppressionEnabled", value);
        }
    }
}
