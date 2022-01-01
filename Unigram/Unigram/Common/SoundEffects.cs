using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Storage;

namespace Unigram.Common
{
    public static class SoundEffects
    {
        public static void Stop()
        {
            foreach (var reference in _prevGraph.Values.ToArray())
            {
                if (reference.TryGetTarget(out AudioGraph target))
                {
                    reference.SetTarget(null);
                    target.Stop();
                }
            }

            _prevGraph.Clear();
        }

        public static async void Play(SoundEffect effect)
        {
            switch (effect)
            {
                case SoundEffect.Sent:
                    await Play(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/sent.mp3")));
                    break;
                case SoundEffect.VoipIncoming:
                    await Play(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voip_incoming.mp3")), null, 0);
                    break;
                case SoundEffect.VoipRingback:
                    await Play(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voip_ringback.mp3")), null, 0);
                    break;
                case SoundEffect.VoipConnecting:
                    await Play(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voip_connecting.mp3")), tag: 0);
                    break;
                case SoundEffect.VoipBusy:
                    await Play(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voip_busy.mp3")), 5, 0);
                    break;
                case SoundEffect.VideoChatJoin:
                    await Play(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voicechat_join.mp3")), tag: 0);
                    break;
                case SoundEffect.VideoChatLeave:
                    await Play(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voicechat_leave.mp3")), tag: 0);
                    break;
            }
        }

        public static async void Play(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                await Play(await StorageFile.GetFileFromPathAsync(file.Local.Path), tag: 1);
            }
        }

        private static readonly ConcurrentDictionary<int, WeakReference<AudioGraph>> _prevGraph = new ConcurrentDictionary<int, WeakReference<AudioGraph>>();

        private static async Task Play(StorageFile file, int? loopCount = 0, int? tag = null)
        {
            try
            {
                await Task.Yield();

                // This seems to fail in some conditions.
                var settings = new AudioGraphSettings(AudioRenderCategory.Media);
                settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.SystemDefault;

                var result = await AudioGraph.CreateAsync(settings);
                if (result.Status != AudioGraphCreationStatus.Success)
                {
                    return;
                }

                var fileInputNodeResult = await result.Graph.CreateFileInputNodeAsync(file);
                if (fileInputNodeResult.Status != AudioFileNodeCreationStatus.Success)
                {
                    return;
                }

                fileInputNodeResult.FileInputNode.LoopCount = loopCount;

                var deviceOutputNodeResult = await result.Graph.CreateDeviceOutputNodeAsync();
                if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
                {
                    return;
                }

                fileInputNodeResult.FileInputNode
                    .AddOutgoingConnection(deviceOutputNodeResult.DeviceOutputNode);

                if (tag != null)
                {
                    if (_prevGraph.TryGetValue(tag.Value, out WeakReference<AudioGraph> reference) && reference.TryGetTarget(out AudioGraph target))
                    {
                        reference.SetTarget(null);
                        target.Stop();
                    }

                    _prevGraph[tag.Value] = new WeakReference<AudioGraph>(result.Graph);
                }

                result.Graph.Start();
            }
            catch { }
        }
    }

    public enum SoundEffect
    {
        Sent,
        VoipIncoming,
        VoipRingback,
        VoipBusy,
        VoipConnecting,
        VideoChatJoin,
        VideoChatLeave
    }
}