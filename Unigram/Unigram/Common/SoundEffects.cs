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
        private static AudioGraph _graph;
        private static AudioDeviceOutputNode _deviceOutputNode;
        private static AudioFileInputNode _fileInputNode;

        public static async void Play(SoundEffect effect)
        {
            if (_graph != null)
            {
                _graph.Dispose();
            }

            var settings = new AudioGraphSettings(AudioRenderCategory.SoundEffects);
            settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;

            var result = await AudioGraph.CreateAsync(settings);
            if (result.Status != AudioGraphCreationStatus.Success)
            {
                return;
            }

            _graph = result.Graph;

            //CurrentFileName = fileName;

            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Audio/sent.mp3"));

            var fileInputNodeResult = await _graph.CreateFileInputNodeAsync(file);
            if (fileInputNodeResult.Status != AudioFileNodeCreationStatus.Success)
            {
                return;
            }

            var deviceOutputNodeResult = await _graph.CreateDeviceOutputNodeAsync();
            if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                return;
            }

            _deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;
            _fileInputNode = fileInputNodeResult.FileInputNode;
            _fileInputNode.AddOutgoingConnection(_deviceOutputNode);

            _graph.Start();
        }
    }

    public enum SoundEffect
    {
        Sent
    }
}