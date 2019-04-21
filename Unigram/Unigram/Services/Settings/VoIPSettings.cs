using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private float? _inputVolume;
        public float InputVolume
        {
            get
            {
                if (_inputVolume == null)
                    _inputVolume = GetValueOrDefault("InputVolume", 1.0f);

                return _inputVolume ?? 1.0f;
            }
            set
            {
                _inputVolume = value;
                AddOrUpdateValue("InputVolume", value);
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

        private float? _outputVolume;
        public float OutputVolume
        {
            get
            {
                if (_outputVolume == null)
                    _outputVolume = GetValueOrDefault("OutputVolume", 1.0f);

                return _outputVolume ?? 1.0f;
            }
            set
            {
                _outputVolume = value;
                AddOrUpdateValue("OutputVolume", value);
            }
        }
    }
}
