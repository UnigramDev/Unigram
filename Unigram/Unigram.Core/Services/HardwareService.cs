using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Mvvm;
using Windows.Devices.Enumeration;

namespace Unigram.Core.Services
{
    public interface IHardwareService
    {
        bool IsMicrophoneAvailable { get; }
    }

    public class HardwareService : BindableBase, IHardwareService
    {
        private DeviceWatcher _microphoneWatcher;
        private DeviceWatcher _videoWatcher;

        public HardwareService()
        {
            _microphoneWatcher = DeviceInformation.CreateWatcher(DeviceClass.AudioCapture);
            _microphoneWatcher.Added += OnMicrophoneAdded;
            _microphoneWatcher.Removed += OnMicrophoneRemoved;
            _microphoneWatcher.Start();

            _videoWatcher = DeviceInformation.CreateWatcher(DeviceClass.VideoCapture);
            _videoWatcher.Added += OnVideoAdded;
            _videoWatcher.Removed += OnVideoRemoved;
            _videoWatcher.Start();
        }

        private void OnMicrophoneAdded(DeviceWatcher sender, DeviceInformation args)
        {
            IsMicrophoneAvailable = true;
        }

        private async void OnMicrophoneRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
            IsMicrophoneAvailable = devices.Count > 0;
        }

        private void OnVideoAdded(DeviceWatcher sender, DeviceInformation args)
        {
            IsVideoAvailable = true;
        }

        private async void OnVideoRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            IsVideoAvailable = devices.Count > 0;
        }

        private bool _isMicrophoneAvailable;
        public bool IsMicrophoneAvailable
        {
            get
            {
                return _isMicrophoneAvailable;
            }
            private set
            {
                Set(ref _isMicrophoneAvailable, value);
            }
        }

        private bool _isVideoAvailable;
        public bool IsVideoAvailable
        {
            get
            {
                return _isVideoAvailable;
            }
            private set
            {
                Set(ref _isVideoAvailable, value);
            }
        }
    }
}
