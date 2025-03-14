//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Storage;

namespace Telegram.Common
{
    public static class SoundEffects
    {
        enum EffectType
        {
            Generic,
            Voip,
            VideoChat,
            Custom
        }

        private static readonly Dictionary<EffectType, AudioGraph> _graphs = new();
        private static readonly Dictionary<EffectType, int> _counts = new();

        private static readonly DisposableMutex _lock = new();

        private static bool _suspended;

        public static void Suspend()
        {
            _suspended = true;
            Stop();
        }

        public static void Resume()
        {
            _suspended = false;
        }

        public static async void Stop()
        {
            using (await _lock.WaitAsync())
            {
                try
                {
                    foreach (var graph in _graphs.Values)
                    {
                        graph.Stop();
                        graph.Dispose();
                    }
                }
                catch { }

                _graphs.Clear();
            }
        }

        private static void Stop(EffectType type)
        {
            try
            {
                if (_graphs.TryGetValue(type, out AudioGraph graph))
                {
                    graph.Stop();
                    graph.Dispose();
                }
            }
            catch { }

            _graphs.Remove(type);
        }

        public static void Play(SoundEffect effect)
        {
            if (_suspended)
            {
                return;
            }

            static Task PlayImpl(string url, int? loopCount = 0, EffectType type = EffectType.Generic)
            {
                return Play(StorageFile.GetFileFromApplicationUriAsync(new Uri(url)), loopCount, type);
            }

            switch (effect)
            {
                case SoundEffect.Sent:
                    _ = PlayImpl("ms-appx:///Assets/Audio/sent.mp3");
                    break;
                case SoundEffect.VoipIncoming:
                    _ = PlayImpl("ms-appx:///Assets/Audio/voip_incoming.mp3", null, EffectType.Voip);
                    break;
                case SoundEffect.VoipRingback:
                    _ = PlayImpl("ms-appx:///Assets/Audio/voip_ringback.mp3", null, EffectType.Voip);
                    break;
                case SoundEffect.VoipConnecting:
                    _ = PlayImpl("ms-appx:///Assets/Audio/voip_connecting.mp3", type: EffectType.Voip);
                    break;
                case SoundEffect.VoipBusy:
                    _ = PlayImpl("ms-appx:///Assets/Audio/voip_busy.mp3", 4, type: EffectType.Voip);
                    break;
                case SoundEffect.VoipEnd:
                    _ = PlayImpl("ms-appx:///Assets/Audio/voip_end.mp3", type: EffectType.Voip);
                    break;
                case SoundEffect.VoipFailed:
                    _ = PlayImpl("ms-appx:///Assets/Audio/voip_failed.mp3", type: EffectType.Voip);
                    break;
                case SoundEffect.VideoChatJoin:
                    _ = PlayImpl("ms-appx:///Assets/Audio/voicechat_join.mp3", type: EffectType.VideoChat);
                    break;
                case SoundEffect.VideoChatLeave:
                    _ = PlayImpl("ms-appx:///Assets/Audio/voicechat_leave.mp3", type: EffectType.VideoChat);
                    break;
            }
        }

        public static void Play(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                _ = Play(StorageFile.GetFileFromPathAsync(file.Local.Path), type: EffectType.Custom);
            }
        }

        private static async Task Play(IAsyncOperation<StorageFile> fileTask, int? loopCount = 0, EffectType type = EffectType.Generic)
        {
            try
            {
                if (_suspended)
                {
                    return;
                }

                var file = await fileTask;

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

                async void handler(AudioFileInputNode node, object args)
                {
                    using (await _lock.WaitAsync())
                    {
                        try
                        {
                            if (_counts[type] == 0)
                            {
                                node.FileCompleted -= handler;
                                Stop(type);
                            }
                            else
                            {
                                _counts[type]--;
                            }
                        }
                        catch
                        {
                            Stop(type);
                        }
                    }
                }

                using (await _lock.WaitAsync())
                {
                    Stop(type);

                    _graphs[type] = result.Graph;
                    _counts[type] = loopCount ?? -1;

                    fileInputNodeResult.FileInputNode.FileCompleted += handler;
                    result.Graph.Start();
                }
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
        VoipFailed,
        VoipEnd,
        VoipConnecting,
        VideoChatJoin,
        VideoChatLeave
    }
}