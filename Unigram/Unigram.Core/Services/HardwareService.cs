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

        public HardwareService()
        {
            _microphoneWatcher = DeviceInformation.CreateWatcher(DeviceClass.AudioRender);
            _microphoneWatcher.Added += OnMicrophoneAdded;
            _microphoneWatcher.Removed += OnMicrophoneRemoved;
            _microphoneWatcher.Start();
        }

        private void OnMicrophoneAdded(DeviceWatcher sender, DeviceInformation args)
        {
            IsMicrophoneAvailable = true;
        }

        private async void OnMicrophoneRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.AudioRender);
            IsMicrophoneAvailable = devices.Count > 0;
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
    }
}
