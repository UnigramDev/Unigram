using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Unigram.Controls.Media;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.System.Display;

namespace Unigram.Helpers
{
    public static class AudioGraphHelper
    {
        public static AudioFileInputNode FileInputNode { get; private set; }

        public static string CurrentFileName { get; private set; }

        public static VoiceMediaControl CurrentVoiceMedia { get; private set; }

        private static AudioGraph _graph;
        private static AudioDeviceOutputNode _deviceOutputNode;
        private static DisplayRequest _displayRequest;

        public static async Task<AudioGraphCreationStatus> LoadAsync(VoiceMediaControl voiceMediaControl, string fileName)
        {
            if (_graph != null)
            {
                _graph.Stop();
            }

            var settings = new AudioGraphSettings(AudioRenderCategory.Media);
            settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;

            var result = await AudioGraph.CreateAsync(settings);
            if (result.Status != AudioGraphCreationStatus.Success)
            {
                return result.Status;
            }

            _graph = result.Graph;

            if (CurrentVoiceMedia != null)
            {
                CurrentVoiceMedia.Pause();
            }

            CurrentVoiceMedia = voiceMediaControl;
            CurrentFileName = fileName;

            var file = await FileUtils.GetTempFileAsync(fileName);

            var fileInputNodeResult = await _graph.CreateFileInputNodeAsync(file);
            if (fileInputNodeResult.Status != AudioFileNodeCreationStatus.Success)
            {
                switch (fileInputNodeResult.Status)
                {
                    case AudioFileNodeCreationStatus.FormatNotSupported:
                        {
                            return AudioGraphCreationStatus.FormatNotSupported;
                        }
                    default:
                        {
                            return AudioGraphCreationStatus.UnknownFailure;
                        }
                }
            }

            var deviceOutputNodeResult = await _graph.CreateDeviceOutputNodeAsync();
            if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                switch (deviceOutputNodeResult.Status)
                {
                    case AudioDeviceNodeCreationStatus.FormatNotSupported:
                        {
                            return AudioGraphCreationStatus.FormatNotSupported;
                        }
                    case AudioDeviceNodeCreationStatus.DeviceNotAvailable:
                        {
                            return AudioGraphCreationStatus.DeviceNotAvailable;
                        }
                    default:
                        {
                            return AudioGraphCreationStatus.UnknownFailure;
                        }
                }
            }

            _deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;
            FileInputNode = fileInputNodeResult.FileInputNode;
            FileInputNode.AddOutgoingConnection(_deviceOutputNode);

            return AudioGraphCreationStatus.Success;
        }

        public static double GetGraphTotalDuration()
        {
            if (FileInputNode == null)
            {
                return 0;
            }

            return FileInputNode.Duration.TotalMilliseconds;
        }

        public static void PlayGraph()
        {
            _graph?.Start();

            if (_displayRequest != null)
            {
                _displayRequest.RequestRelease();
            }

            _displayRequest = new DisplayRequest();
            _displayRequest.RequestActive();
        }

        public static void StopGraph()
        {
            _graph?.Stop();
            
            if (_displayRequest != null)
            {
                _displayRequest.RequestRelease();
                _displayRequest = null;
            }
        }
    }
}
