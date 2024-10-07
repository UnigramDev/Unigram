using System;
using System.Collections.Generic;

namespace Telegram.Common
{
    public enum MediaDeviceClass
    {
        VideoInput,
        AudioInput,
        AudioOutput
    }

    public record MediaDevice2(string Id, string Name, MediaDeviceClass Class);

    public record MediaDeviceId(string Id, bool IsDefault);

    public record MediaDeviceChangedEventArgs(MediaDeviceClass DeviceClass, string DeviceId);

    public partial class MediaDeviceTracker
    {
        private readonly Dictionary<MediaDeviceClass, MediaDeviceList> _devices = new()
        {
            { MediaDeviceClass.VideoInput, new MediaDeviceList(MediaDeviceClass.VideoInput) },
            { MediaDeviceClass.AudioInput, new MediaDeviceList(MediaDeviceClass.AudioInput) },
            { MediaDeviceClass.AudioOutput, new MediaDeviceList(MediaDeviceClass.AudioOutput) },
        };

        // TODO: implement storage of chosen devices

        public void Stop()
        {
            _devices[MediaDeviceClass.VideoInput].Stop();
            _devices[MediaDeviceClass.AudioInput].Stop();
            _devices[MediaDeviceClass.AudioOutput].Stop();
        }

        public event EventHandler<MediaDeviceChangedEventArgs> Changed
        {
            add
            {
                _devices[MediaDeviceClass.VideoInput].Changed += value;
                _devices[MediaDeviceClass.AudioInput].Changed += value;
                _devices[MediaDeviceClass.AudioOutput].Changed += value;
            }
            remove
            {
                _devices[MediaDeviceClass.VideoInput].Changed -= value;
                _devices[MediaDeviceClass.AudioInput].Changed -= value;
                _devices[MediaDeviceClass.AudioOutput].Changed -= value;
            }
        }

        public bool? HasDevices(MediaDeviceClass deviceClass)
        {
            if (_devices.TryGetValue(deviceClass, out var devices))
            {
                return devices.HasValues;
            }

            return false;
        }

        public IList<MediaDevice2> GetDevices(MediaDeviceClass deviceClass)
        {
            if (_devices.TryGetValue(deviceClass, out var devices))
            {
                return devices.GetValues();
            }

            return Array.Empty<MediaDevice2>();
        }

        public void Track(MediaDeviceClass deviceClass, string deviceId)
        {
            if (_devices.TryGetValue(deviceClass, out var devices))
            {
                devices.Track(deviceId);
            }
        }
    }
}
