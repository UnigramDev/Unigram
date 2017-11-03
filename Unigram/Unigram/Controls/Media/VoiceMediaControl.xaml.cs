using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Views;
using Windows.Foundation;
using Windows.Media.Audio;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Unigram.Common;
using Telegram.Api.Services;
using Telegram.Api.Aggregator;
using Unigram.Helpers;

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

                    if (ViewModel.Media is TLMessageMediaDocument mediaDocument)
                    {
                        if (mediaDocument.Document is TLDocument document)
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
            if (ViewModel?.Media is TLMessageMediaDocument documentMedia)
            {
                if (documentMedia.Document is TLDocument document)
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
        private TimeSpan _position = TimeSpan.Zero;
        private string _fileName;

        private async void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_state == PlaybackState.Paused)
            {
                if (AudioGraphHelper.FileInputNode != null && string.Equals(_fileName, AudioGraphHelper.CurrentFileName, StringComparison.OrdinalIgnoreCase) && _position.TotalMilliseconds > 0)
                {
                    AudioGraphHelper.FileInputNode.Seek(_position);
                    Slide.Value = _position.TotalMilliseconds;
                    PlayOrResume();
                }
                else if (ViewModel is TLMessage message && message.Media is TLMessageMediaDocument documentMedia)
                {
                    if (documentMedia.Document is TLDocument document)
                    {
                        _fileName = document.GetFileName();
                        if (File.Exists(FileUtils.GetTempFileName(_fileName)) == false)
                        {
                            _state = PlaybackState.Loading;
                            UpdateGlyph();
                            var manager = UnigramContainer.Current.ResolveType<IDownloadAudioFileManager>();
                            var download = await manager.DownloadFileAsync(_fileName, document.DCId, document.ToInputFileLocation(), document.Size).AsTask(documentMedia.Document.Download());
                        }

                        if (message.IsMediaUnread && !message.IsOut)
                        {
                            var vector = new TLVector<int> { message.Id };
                            if (message.Parent is TLChannel channel)
                            {
                                TelegramEventAggregator.Instance.Publish(new TLUpdateChannelReadMessagesContents { ChannelId = channel.Id, Messages = vector });
                                MTProtoService.Current.ReadMessageContentsAsync(channel.ToInputChannel(), vector, affected =>
                                {
                                    message.IsMediaUnread = false;
                                    message.RaisePropertyChanged(() => message.IsMediaUnread);
                                });
                            }
                            else
                            {
                                TelegramEventAggregator.Instance.Publish(new TLUpdateReadMessagesContents { Messages = vector });
                                MTProtoService.Current.ReadMessageContentsAsync(vector, affected =>
                                {
                                    message.IsMediaUnread = false;
                                    message.RaisePropertyChanged(() => message.IsMediaUnread);
                                });
                            }
                        }

                        var result = await AudioGraphHelper.LoadAsync(this, _fileName);
                        if (result != AudioGraphCreationStatus.Success)
                        {
                            return;
                        }

                        AudioGraphHelper.FileInputNode.FileCompleted += OnFileCompleted;

                        if (_position.Milliseconds > 0)
                        {
                            AudioGraphHelper.FileInputNode.Seek(_position);
                            Slide.Value = _position.TotalMilliseconds;
                        }
                        else
                        {
                            _position = TimeSpan.Zero;
                            Slide.Value = 0;
                        }

                        Slide.Maximum = AudioGraphHelper.GetGraphTotalDuration();
                        PlayOrResume();
                    }
                }
            }
            else if (_state == PlaybackState.Playing)
            {
                Pause();
            }
        }

        private void PlayOrResume()
        {
            AudioGraphHelper.PlayGraph();
            _timer.Start();
            _state = PlaybackState.Playing;
            UpdateGlyph();
        }

        public void Pause()
        {
            AudioGraphHelper.StopGraph();
            _timer?.Stop();
            _state = PlaybackState.Paused;
            UpdateGlyph();
        }

        private void OnFileCompleted(AudioFileInputNode sender, object args)
        {
            this.BeginOnUIThread(() =>
            {
                Pause();

                _position = TimeSpan.Zero;
                AudioGraphHelper.FileInputNode.Seek(_position);

                DurationLabel.Text = AudioGraphHelper.FileInputNode.Duration.ToString("mm\\:ss");
                Slide.Value = 0;
            });
        }

        private void OnTick(object sender, object e)
        {
            _position = AudioGraphHelper.FileInputNode.Position;
            DurationLabel.Text = _position.ToString("mm\\:ss") + " / " + AudioGraphHelper.FileInputNode.Duration.ToString("mm\\:ss");        
            Slide.Value = _position.TotalMilliseconds;
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
