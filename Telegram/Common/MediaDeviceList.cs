using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

namespace Telegram.Common
{
    public partial class MediaDeviceList
    {
        private readonly Dictionary<string, MediaDevice2> _cache = new();
        private readonly object _lock = new();

        private readonly MediaDeviceClass _class;
        private readonly DeviceWatcher _watcher;

        private MediaDeviceId _tracked;

        public MediaDeviceList(MediaDeviceClass deviceClass)
        {
            _class = deviceClass;
            _tracked = GetDefaultId(deviceClass);

            try
            {
                var nativeClass = deviceClass switch
                {
                    MediaDeviceClass.VideoInput => DeviceClass.VideoCapture,
                    MediaDeviceClass.AudioInput => DeviceClass.AudioCapture,
                    MediaDeviceClass.AudioOutput => DeviceClass.AudioRender,
                    _ => DeviceClass.AudioRender
                };

                _watcher = DeviceInformation.CreateWatcher(nativeClass);
                _watcher.Added += OnAdded;
                _watcher.Removed += OnRemoved;

                _watcher.Start();

                if (deviceClass == MediaDeviceClass.AudioInput)
                {
                    MediaDevice.DefaultAudioCaptureDeviceChanged += OnDefaultAudioCaptureDeviceChanged;
                }
                else if (deviceClass == MediaDeviceClass.AudioOutput)
                {
                    MediaDevice.DefaultAudioRenderDeviceChanged += OnDefaultAudioRenderDeviceChanged;
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public void Stop()
        {
            try
            {
                _watcher.Added -= OnAdded;
                _watcher.Removed -= OnRemoved;

                _watcher.Stop();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        private void OnDefaultAudioCaptureDeviceChanged(object sender, DefaultAudioCaptureDeviceChangedEventArgs args)
        {
            if (args.Role == AudioDeviceRole.Communications)
            {
                OnDefaultDeviceChanged(args.Id);
            }
        }

        private void OnDefaultAudioRenderDeviceChanged(object sender, DefaultAudioRenderDeviceChangedEventArgs args)
        {
            if (args.Role == AudioDeviceRole.Communications)
            {
                OnDefaultDeviceChanged(args.Id);
            }
        }

        private void OnDefaultDeviceChanged(string defaultId)
        {
            lock (_lock)
            {
                if (_tracked.IsDefault && _tracked.Id != defaultId && _cache.ContainsKey(defaultId))
                {
                    _tracked = new MediaDeviceId(defaultId, true);
                    Changed?.Invoke(this, new MediaDeviceChangedEventArgs(_class, Constants.DefaultDeviceId));
                }
            }
        }

        public event EventHandler<MediaDeviceChangedEventArgs> Changed;

        public bool? HasValues
        {
            get
            {
                lock (_cache)
                {
                    return _cache.Count > 0 ? true : _watcher?.Status == DeviceWatcherStatus.EnumerationCompleted ? false : null;
                }
            }
        }

        public IList<MediaDevice2> GetValues()
        {
            lock (_lock)
            {
                var devices = _cache.Values
                    .OrderBy(x => x.Name)
                    .ToList();

                if (_class != MediaDeviceClass.VideoInput)
                {
                    devices.Insert(0, new MediaDevice2(string.Empty, Strings.Default, _class));
                }

                return devices;
            }
        }

        public void Track(string deviceId)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(deviceId))
                {
                    _tracked = GetDefaultId(_class);
                }
                else
                {
                    _tracked = new MediaDeviceId(deviceId, false);
                }
            }
        }

        private void OnAdded(DeviceWatcher sender, DeviceInformation args)
        {
            lock (_lock)
            {
                var item = new MediaDevice2(args.Id, args.Name, _class);
                _cache[item.Id] = item;

                if (_tracked.IsDefault && _class != MediaDeviceClass.VideoInput)
                {
                    var defaultId = GetDefaultId(_class);
                    if (defaultId.Id == item.Id && defaultId.Id != _tracked.Id)
                    {
                        _tracked = defaultId;
                        Changed?.Invoke(this, new MediaDeviceChangedEventArgs(_class, Constants.DefaultDeviceId));
                    }
                }
            }
        }

        private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(args.Id, out var item))
                {
                    _cache.Remove(item.Id);
                }

                if (_tracked.Id == args.Id)
                {
                    _tracked = GetDefaultId(_class);
                    Changed?.Invoke(this, new MediaDeviceChangedEventArgs(_class, Constants.DefaultDeviceId));
                }
            }
        }

        class MediaDeviceComparer : IComparer<MediaDevice2>
        {
            public int Compare(MediaDevice2 x, MediaDevice2 y)
            {
                return x.Name.CompareTo(y.Name);
            }
        }

        private static MediaDeviceId GetDefaultId(MediaDeviceClass deviceClass)
        {
            try
            {
                return deviceClass switch
                {
                    MediaDeviceClass.AudioInput => new MediaDeviceId(MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Communications), true),
                    MediaDeviceClass.AudioOutput => new MediaDeviceId(MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Communications), true),
                    _ => new MediaDeviceId(Constants.DefaultDeviceId, true)
                };
            }
            catch
            {
                return new MediaDeviceId(Constants.DefaultDeviceId, true);
            }
        }
    }
}
