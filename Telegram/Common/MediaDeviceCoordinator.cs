using System.Collections.Generic;
using System.Linq;
using Windows.Devices.Enumeration;

namespace Telegram.Common
{
    public static class MediaDeviceCoordinator
    {
        private static readonly MediaDeviceList _videoInput = new MediaDeviceList(DeviceClass.VideoCapture);
        private static readonly MediaDeviceList _audioInput = new MediaDeviceList(DeviceClass.AudioCapture);
        private static readonly MediaDeviceList _audioOutput = new MediaDeviceList(DeviceClass.AudioRender);

        public static IList<MediaDevice2> VideoInput => _videoInput.Values;
        public static IList<MediaDevice2> AudioInput => _audioInput.Values;
        public static IList<MediaDevice2> AudioOutput => _audioOutput.Values;

        public static bool HasVideoInput => _videoInput.HasValues;
        public static bool HasAudioInput => _audioInput.HasValues;
        public static bool HasAudioOutput => _audioOutput.HasValues;

        // TODO: implement storage of chosen devices

        public static void Start()
        {
            // Does nothing, just initializes all the stuff
        }
    }

    public record MediaDevice2(string Id, string Name, DeviceClass Class);

    public partial class MediaDeviceList
    {
        private readonly DeviceClass _class;
        private readonly DeviceWatcher _watcher;

        private readonly object _lock = new();

        private readonly Dictionary<string, MediaDevice2> _cache = new();

        public bool HasValues
        {
            get
            {
                lock (_cache)
                {
                    return _cache.Count > 0;
                }
            }
        }

        public IList<MediaDevice2> Values
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Values
                        .OrderBy(x => x.Name)
                        .ToList();
                }
            }
        }

        public MediaDeviceList(DeviceClass deviceClass)
        {
            _class = deviceClass;

            try
            {
                _watcher = DeviceInformation.CreateWatcher(deviceClass);
                _watcher.Added += OnAdded;
                _watcher.Removed += OnRemoved;

                _watcher.Start();
            }
            catch { }
        }

        private void OnAdded(DeviceWatcher sender, DeviceInformation args)
        {
            lock (_lock)
            {
                var item = new MediaDevice2(args.Id, args.Name, _class);
                _cache[item.Id] = item;
                //Add(item);
            }
        }

        private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(args.Id, out var item))
                {
                    _cache.Remove(item.Id);
                    //Remove(item);
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
    }
}
