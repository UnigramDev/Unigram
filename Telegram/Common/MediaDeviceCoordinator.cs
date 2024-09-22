using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace Telegram.Common
{
    internal class MediaDeviceCoordinator
    {
        private static readonly MediaDeviceList _videoInput = new MediaDeviceList(DeviceClass.VideoCapture);
        private static readonly MediaDeviceList _audioInput = new MediaDeviceList(DeviceClass.AudioCapture);
        private static readonly MediaDeviceList _audioOutput = new MediaDeviceList(DeviceClass.AudioRender);

        public static ISet<MediaDevice2> VideoInput => _videoInput;
        public static ISet<MediaDevice2> AudioInput => _audioInput;
        public static ISet<MediaDevice2> AudioOutput => _audioOutput;

        // TODO: implement storage of chosen devices
    }

    public record MediaDevice2(string Id, string Name, DeviceClass Class);

    public partial class MediaDeviceList : SortedSet<MediaDevice2>
    {
        private readonly DeviceClass _class;
        private readonly DeviceWatcher _watcher;

        private readonly Dictionary<string, MediaDevice2> _cache = new();

        public MediaDeviceList(DeviceClass deviceClass)
            : base(new MediaDeviceComparer())
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
            var item = new MediaDevice2(args.Id, args.Name, _class);
            _cache[item.Id] = item;
            Add(item);
        }

        private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            if (_cache.TryGetValue(args.Id, out var item))
            {
                _cache.Remove(item.Id);
                Remove(item);
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
