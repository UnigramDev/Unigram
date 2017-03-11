using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Core.Dependency;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Media.Audio;
using Windows.Media.Render;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Unigram.Common;

namespace Unigram.Controls.Media
{
    public sealed partial class VoiceMediaControl : UserControl
    {
        public TLMessage ViewModel => DataContext as TLMessage;

        public VoiceMediaControl()
        {
            InitializeComponent();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(200);
            _timer.Tick += OnTick;

            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null)
                {
                    Loading?.Invoke(s, null);

                    var mediaDocument = ViewModel.Media as TLMessageMediaDocument;
                    if (mediaDocument != null)
                    {
                        var document = mediaDocument.Document as TLDocument;
                        if (document != null)
                        {
                            UpdateGlyph();

                            var audioAttribute = document.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                            if (audioAttribute != null)
                            {
                                DurationLabel.Text = TimeSpan.FromSeconds(audioAttribute.Duration).ToString("mm\\:ss");

                                //if (audioAttribute.HasWaveform)
                                //{
                                //    UpdateSlide(audioAttribute.Waveform);
                                //}
                                //else
                                //{
                                //    UpdateSlide(new byte[] { 0, 0, 0 });
                                //}
                            }
                        }
                    }
                }
            };
        }

        private void UpdateGlyph()
        {
            var documentMedia = ViewModel?.Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                var document = documentMedia.Document as TLDocument;
                if (document != null)
                {
                    var fileName = Path.GetFileNameWithoutExtension(document.GetFileName()) + ".ogg";
                    if (File.Exists(FileUtils.GetTempFileName(fileName)))
                    {
                        StatusGlyph.Glyph = _state == PlaybackState.Playing ? "\uE103" : "\uE102";
                    }
                    else
                    {
                        StatusGlyph.Glyph = _state == PlaybackState.Loading ? "\uE10A" : "\uE118";
                    }
                }
            }
        }

        #region Drawing

        private byte[] _oldWaveform;

        private void UpdateSlide(byte[] waveform)
        {
            if (waveform == _oldWaveform) return;

            _oldWaveform = waveform;

            var backgroundColor = (Color)((SolidColorBrush)Resources["SliderTrackFill"]).Color;
            var foregroundColor = (Color)((SolidColorBrush)Resources["SliderTrackValueFill"]).Color;

            var result = new double[waveform.Length * 8 / 5];
            for (int i = 0; i < result.Length; i++)
            {
                int j = (i * 5) / 8, shift = (i * 5) % 8;
                result[i] = ((waveform[j] | (waveform[j + 1] << 8)) >> shift & 0x1F) / 31.0;
            }

            var imageWidth = 209.0;
            var imageHeight = 28;

            var space = 1.0;
            var lineWidth = 2.0;
            var lines = waveform.Length * 8 / 5;
            var maxLines = (imageWidth - space) / (lineWidth + space);
            var maxWidth = (double)lines / maxLines;

            var background = new WriteableBitmap((int)imageWidth, imageHeight);
            var foreground = new WriteableBitmap((int)imageWidth, imageHeight);

            var backgroundBuffer = new byte[background.PixelWidth * background.PixelHeight * 4];
            var foregroundBuffer = new byte[foreground.PixelWidth * foreground.PixelHeight * 4];

            for (int index = 0; index < maxLines; index++)
            {
                var lineIndex = (int)(index * maxWidth);
                var lineHeight = result[lineIndex] * (double)(imageHeight - 4.0) + 4.0;

                var x1 = (int)(index * (lineWidth + space));
                var y1 = imageHeight - (int)lineHeight;
                var x2 = (int)(index * (lineWidth + space) + lineWidth);
                var y2 = imageHeight;

                DrawFilledRectangle(ref backgroundBuffer, background.PixelWidth, background.PixelHeight, x1, y1, x2, y2, backgroundColor);
                DrawFilledRectangle(ref foregroundBuffer, foreground.PixelWidth, foreground.PixelHeight, x1, y1, x2, y2, foregroundColor);
            }

            using (Stream backgroundStream = background.PixelBuffer.AsStream())
            using (Stream foregroundStream = foreground.PixelBuffer.AsStream())
            {
                backgroundStream.Write(backgroundBuffer, 0, backgroundBuffer.Length);
                foregroundStream.Write(foregroundBuffer, 0, foregroundBuffer.Length);
            }

            Slide.Background = new ImageBrush { ImageSource = background, AlignmentX = AlignmentX.Right, AlignmentY = AlignmentY.Center, Stretch = Stretch.None };
            Slide.Foreground = new ImageBrush { ImageSource = foreground, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Center, Stretch = Stretch.None };
        }

        private void DrawFilledRectangle(ref byte[] pixels, int width, int height, int x1, int y1, int x2, int y2, Color color)
        {
            if (x1 < 0) x1 = 0;
            if (y1 < 0) y1 = 0;
            if (x2 < 0) x2 = 0;
            if (y2 < 0) y2 = 0;

            if (x1 >= width) x1 = width - 1;
            if (y1 >= height) y1 = height - 1;
            if (x2 >= width) x2 = width;
            if (y2 >= height) y2 = height;

            var line = y1 * width;
            for (int i = y1; i < y2; i++)
            {
                var j = line + x1;
                while (j < line + x2)
                {
                    pixels[j * 4 + 0] = color.B;
                    pixels[j * 4 + 1] = color.G;
                    pixels[j * 4 + 2] = color.R;
                    pixels[j * 4 + 3] = color.A;
                    j++;
                }

                line += width;
            }
        }

        #endregion

        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;

        #region Play

        private DispatcherTimer _timer;
        private PlaybackState _state = PlaybackState.Paused;
        private AudioGraph _graph;
        private AudioDeviceOutputNode _deviceOutputNode;
        private AudioFileInputNode _fileInputNode;

        private async void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_state == PlaybackState.Paused)
            {
                var documentMedia = ViewModel?.Media as TLMessageMediaDocument;
                if (documentMedia != null)
                {
                    var document = documentMedia.Document as TLDocument;
                    if (document != null)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(document.GetFileName()) + ".ogg";
                        if (File.Exists(FileUtils.GetTempFileName(fileName)) == false)
                        {
                            _state = PlaybackState.Loading;
                            UpdateGlyph();
                            var manager = UnigramContainer.Current.ResolveType<IDownloadDocumentFileManager>();
                            var download = await manager.DownloadFileAsync(fileName, document.DCId, document.ToInputFileLocation(), document.Size).AsTask(documentMedia.Download());
                        }

                        var settings = new AudioGraphSettings(AudioRenderCategory.Media);
                        settings.QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency;

                        var result = await AudioGraph.CreateAsync(settings);
                        if (result.Status != AudioGraphCreationStatus.Success)
                            return;

                        _graph = result.Graph;
                        Debug.WriteLine("Graph successfully created!");

                        var file = await FileUtils.GetTempFileAsync(fileName);

                        var fileInputNodeResult = await _graph.CreateFileInputNodeAsync(file);
                        if (fileInputNodeResult.Status != AudioFileNodeCreationStatus.Success)
                            return;

                        var deviceOutputNodeResult = await _graph.CreateDeviceOutputNodeAsync();
                        if (deviceOutputNodeResult.Status != AudioDeviceNodeCreationStatus.Success)
                            return;

                        _deviceOutputNode = deviceOutputNodeResult.DeviceOutputNode;
                        _fileInputNode = fileInputNodeResult.FileInputNode;
                        _fileInputNode.AddOutgoingConnection(_deviceOutputNode);
                        _fileInputNode.FileCompleted += OnFileCompleted;

                        _graph.Start();
                        _timer.Start();
                        _state = PlaybackState.Playing;
                        UpdateGlyph();
                        Slide.Maximum = _fileInputNode.Duration.TotalMilliseconds;
                        Slide.Value = 0;
                    }
                }
            }
            else if (_state == PlaybackState.Playing)
            {
                _graph?.Stop();
                _state = PlaybackState.Paused;
                UpdateGlyph();
            }
        }

        private void OnFileCompleted(AudioFileInputNode sender, object args)
        {
            Execute.BeginOnUIThread(() =>
            {
                _graph.Stop();
                _timer.Stop();
                _fileInputNode.Seek(TimeSpan.Zero);
                _state = PlaybackState.Paused;
                UpdateGlyph();
                DurationLabel.Text = _fileInputNode.Duration.ToString("mm\\:ss");
                Slide.Value = 0;
            });
        }

        private void OnTick(object sender, object e)
        {
            DurationLabel.Text = _fileInputNode.Position.ToString("mm\\:ss") + " / " + _fileInputNode.Duration.ToString("mm\\:ss");
            Slide.Value = _fileInputNode.Position.TotalMilliseconds;
            //_position += 200;
            ////Indicator.Value = _fileNode.Position.TotalMilliseconds;

            //if (_position >= Indicator.Maximum)
            //{
            //    _position = 200;
            //    _timer.Stop();
            //    _graph.Stop();
            //    _fileNode.Seek(TimeSpan.Zero);

            //    VisualStateManager.GoToState(this, "Paused", false);
            //    State = PlaybackState.Paused;
            //    Indicator.Value = 0;
            //    CurrentPlaying = null;
            //}
        }

        #endregion

        private enum PlaybackState
        {
            Loading,
            Playing,
            Paused
        }
    }
}
