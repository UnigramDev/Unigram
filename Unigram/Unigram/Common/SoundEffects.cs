using System;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Storage;
using Telegram.Td.Api;

namespace Unigram.Common
{
    public static class SoundEffects
    {
        public static async void Play(SoundEffect effect)
        {
            Play(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/sent.mp3")));
        }

        public static async void Play(File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Play(await StorageFile.GetFileFromPathAsync(file.Local.Path));
            }
        }

        private static async void Play(StorageFile file)
        {
            try
            {
                // This seems to fail in some conditions.
                var settings = new AudioGraphSettings(AudioRenderCategory.SoundEffects);
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

                var deviceOutputNodeResult = await result.Graph.CreateDeviceOutputNodeAsync();
                if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
                {
                    return;
                }

                fileInputNodeResult.FileInputNode
                    .AddOutgoingConnection(deviceOutputNodeResult.DeviceOutputNode);

                result.Graph.Start();
            }
            catch { }
        }
    }

    public enum SoundEffect
    {
        Sent
    }
}