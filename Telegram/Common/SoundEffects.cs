//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
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
        private static AudioGraph _temporaryGraph;
        private static AudioGraph _permanentGraph;

        private static int _permanentCount;

        public static void Stop()
        {
            StopTemporary();
            StopPermanent();
        }

        private static void StopTemporary()
        {
            try
            {
                _temporaryGraph?.Stop();
                _temporaryGraph?.Dispose();
            }
            catch { }

            _temporaryGraph = null;
        }

        private static void StopPermanent()
        {
            try
            {
                _permanentGraph?.Stop();
                _permanentGraph?.Dispose();
            }
            catch { }

            _permanentGraph = null;
            _permanentCount = 0;
        }

        public static void Play(SoundEffect effect)
        {
            switch (effect)
            {
                case SoundEffect.Sent:
                    _ = Play(StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/sent.mp3")));
                    break;
                case SoundEffect.VoipIncoming:
                    _ = Play(StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voip_incoming.mp3")), null, true);
                    break;
                case SoundEffect.VoipRingback:
                    _ = Play(StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voip_ringback.mp3")), null, true);
                    break;
                case SoundEffect.VoipConnecting:
                    _ = Play(StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voip_connecting.mp3")), permanent: true);
                    break;
                case SoundEffect.VoipBusy:
                    _ = Play(StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voip_busy.mp3")), 4, permanent: true);
                    break;
                case SoundEffect.VideoChatJoin:
                    _ = Play(StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voicechat_join.mp3")), permanent: true);
                    break;
                case SoundEffect.VideoChatLeave:
                    _ = Play(StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/voicechat_leave.mp3")), permanent: true);
                    break;
            }
        }

        public static void Play(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                _ = Play(StorageFile.GetFileFromPathAsync(file.Local.Path), permanent: true);
            }
        }

        private static async Task Play(IAsyncOperation<StorageFile> fileTask, int? loopCount = 0, bool permanent = false)
        {
            try
            {
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

                if (permanent)
                {
                    StopPermanent();
                    _permanentGraph = result.Graph;
                    _permanentCount = loopCount ?? -1;

                    void handler(AudioFileInputNode node, object args)
                    {
                        try
                        {
                            if (_permanentCount == 0)
                            {
                                node.FileCompleted -= handler;
                                StopPermanent();
                            }
                            else
                            {
                                _permanentCount--;
                            }
                        }
                        catch
                        {
                            StopPermanent();
                        }
                    }

                    if (loopCount.HasValue)
                    {
                        fileInputNodeResult.FileInputNode.FileCompleted += handler;
                    }
                }
                else
                {
                    StopTemporary();
                    _temporaryGraph = result.Graph;

                    void handler(AudioFileInputNode node, object args)
                    {
                        node.FileCompleted -= handler;
                        StopTemporary();
                    }

                    fileInputNodeResult.FileInputNode.FileCompleted += handler;
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