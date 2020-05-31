using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Controls.Media;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Storage;
using Windows.System.Display;

namespace Unigram.Common
{
    public static class SoundEffects
    {
        public static async void Play(SoundEffect effect)
        {
            var settings = new AudioGraphSettings(AudioRenderCategory.SoundEffects);
            settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;

            var result = await AudioGraph.CreateAsync(settings);
            if (result.Status != AudioGraphCreationStatus.Success)
            {
                return;
            }

            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/sent.mp3"));

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
    }

    public enum SoundEffect
    {
        Sent
    }
}