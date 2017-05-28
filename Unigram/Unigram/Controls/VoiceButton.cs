using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Unigram.Controls.Views;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.Devices.Enumeration;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class VoiceButton : GlyphToggleButton
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private bool _video;
        private DispatcherTimer _timer;
        private OpusRecorder _recorder;
        private StorageFile _file;
        private bool _cancelOnRelease;
        private bool _pressed;
        private bool _recording;
        private DateTime _start;

        private ManualResetEvent _startReset = new ManualResetEvent(true);
        private ManualResetEvent _stopReset = new ManualResetEvent(false);

        private RoundVideoView _roundView = new RoundVideoView();

        public TimeSpan Elapsed
        {
            get
            {
                return DateTime.Now - _start;
            }
        }

        public bool IsVideo
        {
            get
            {
                return IsChecked.HasValue && IsChecked.Value;
            }
            set
            {
                IsChecked = value;
            }
        }

        public bool IsVoice
        {
            get
            {
                return IsChecked.HasValue && !IsChecked.Value;
            }
            set
            {
                IsChecked = !value;
            }
        }

        public VoiceButton()
        {
            DefaultStyleKey = typeof(VoiceButton);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(300);
            _timer.Tick += (s, args) =>
            {
                _timer.Stop();

                if (_pressed)
                {
                    Start();
                }
            };
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            //Start();
            _timer.Stop();
            _timer.Start();

            _pressed = true;
            CapturePointer(e.Pointer);

            base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);

            _timer.Stop();
            _pressed = false;

            //Stop();
            ReleasePointerCapture(e.Pointer);

            if (_recording)
            {
                Stop();
            }
            else
            {
                IsVideo = !IsVideo;
            }
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);

            if (_pressed)
            {
                _cancelOnRelease = false;
            }
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);

            if (_pressed)
            {
                _cancelOnRelease = true;
            }
        }

        private void Start()
        {
            _video = IsVideo;

            Task.Run(async () =>
            {
                _startReset.WaitOne();
                _startReset.Reset();

                _recording = true;
                _start = DateTime.Now;

                Execute.BeginOnUIThread(() =>
                {
                    if (_video)
                    {
                        _roundView.IsOpen = true;
                    }

                    RecordingStarted?.Invoke(this, EventArgs.Empty);
                });

                //_stopReset.Set();
                //return;

                _file = await ApplicationData.Current.LocalFolder.CreateFileAsync(_video ? "temp\\recording.mp4" : "temp\\recording.ogg", CreationCollisionOption.ReplaceExisting);
                _recorder = new OpusRecorder(_file, _video);

                /* This following was moved from sub thread, because of a exception which comes from that device initializiation
                 * is only allowed from UI thread!
                 */
                if (_recorder.m_mediaCapture != null)
                {
                    Debug.WriteLine("Cannot start while recording");
                }

                try
                {
                    _recorder.m_mediaCapture = new MediaCapture();

                    _recorder.settings.VideoDeviceId = await _recorder.GetVideoProfileSupportedDeviceIdAsync(Windows.Devices.Enumeration.Panel.Front);
                    await _recorder.m_mediaCapture.InitializeAsync(_recorder.settings);

                    if (_video)
                    {
                        await _roundView.SetAsync(_recorder.m_mediaCapture);
                    }

                    await _recorder.StartAsync();
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("The access to microphone was denied!");
                    return;
                }
                catch (Exception)
                {
                    Debug.WriteLine("The app couldn't initialize microphone!");
                    return;
                }

                _pressed = true;
                _cancelOnRelease = false;
                _start = DateTime.Now;
                _stopReset.Set();

                Debug.WriteLine("Start: " + _start);
                Debug.WriteLine("Stop unlocked");
            });
        }

        private void Stop()
        {
            Task.Run(async () =>
            {
                _stopReset.WaitOne();
                _stopReset.Reset();

                _recording = false;

                Execute.BeginOnUIThread(() =>
                {
                    if (_video)
                    {
                        _roundView.IsOpen = false;
                    }

                    RecordingStopped?.Invoke(this, EventArgs.Empty);
                });

                //_startReset.Set();
                //return;

                var now = DateTime.Now;
                var elapsed = now - _start;

                Debug.WriteLine("Stop reached");
                Debug.WriteLine("Stop: " + now);

                if (_recorder == null)
                {
                    _startReset.Set();
                    return;
                }

                if (_recorder.IsRecording)
                {
                    await _recorder.StopAsync();
                }

                if (_cancelOnRelease || elapsed < TimeSpan.FromSeconds(1))
                {
                    await _file.DeleteAsync();
                }
                else if (_file != null)
                {
                    Debug.WriteLine("Sending voice message");

                    Execute.BeginOnUIThread(async () =>
                    {
                        if (_video)
                        {
                            var props = await _file.Properties.GetVideoPropertiesAsync();
                            var width = props.Width;
                            var height = props.Height;
                            var x = 0d;
                            var y = 0d;

                            if (width > height)
                            {
                                x = (width - height) / 2;
                                width = height;
                            }

                            if (height > width)
                            {
                                y = (height - width) / 2;
                                height = width;
                            }

                            var transform = new VideoTransformEffectDefinition();
                            transform.CropRectangle = new Windows.Foundation.Rect(x, y, width, height);
                            transform.OutputSize = new Windows.Foundation.Size(240, 240);

                            var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
                            profile.Video.Width = 240;
                            profile.Video.Height = 240;
                            profile.Video.Bitrate = 300000;

                            await ViewModel.SendVideoAsync(_file, null, true, transform, profile);
                        }
                        else
                        {
                            await ViewModel.SendAudioAsync(_file, (int)elapsed.TotalSeconds, true, null, null, null);
                        }
                    });
                }

                _startReset.Set();
            });
        }

        internal sealed class OpusRecorder
        {
            #region fields

            private bool m_isRecording;
            private bool m_isVideo;

            private StorageFile m_file;
            private IMediaExtension m_opusSink;
            private LowLagMediaRecording m_lowLag;
            public MediaCapture m_mediaCapture;
            public MediaCaptureInitializationSettings settings;

            #endregion

            #region properties

            public StorageFile File
            {
                get { return m_file; }
            }

            public bool IsRecording
            {
                get { return m_mediaCapture != null && m_isRecording; }
            }

            #endregion

            #region constructors

            public OpusRecorder(StorageFile file, bool video)
            {
                m_file = file;
                m_isVideo = video;
                InitializeSettings();
            }

            #endregion

            #region methods

            private void InitializeSettings()
            {
                settings = new MediaCaptureInitializationSettings();
                settings.MediaCategory = MediaCategory.Speech;
                settings.AudioProcessing = AudioProcessing.Default;
                settings.MemoryPreference = MediaCaptureMemoryPreference.Auto;
                settings.SharingMode = MediaCaptureSharingMode.SharedReadOnly;
                settings.StreamingCaptureMode = m_isVideo ? StreamingCaptureMode.AudioAndVideo : StreamingCaptureMode.Audio;
            }

            public async Task<string> GetVideoProfileSupportedDeviceIdAsync(Windows.Devices.Enumeration.Panel panel)
            {
                var deviceId = string.Empty;
                var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                foreach (var device in devices)
                {
                    if (MediaCapture.IsVideoProfileSupported(device.Id) && device.EnclosureLocation.Panel == panel)
                    {
                        deviceId = device.Id;
                        break;
                    }
                }

                return deviceId;
            }

            public async Task StartAsync()
            {
                m_isRecording = true;

                if (m_isVideo)
                {
                    var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);

                    m_lowLag = await m_mediaCapture.PrepareLowLagRecordToStorageFileAsync(profile, m_file);

                    await m_lowLag.StartAsync();
                }
                else
                {
                    var wavEncodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                    wavEncodingProfile.Audio.BitsPerSample = 16;
                    wavEncodingProfile.Audio.SampleRate = 48000;
                    wavEncodingProfile.Audio.ChannelCount = 1;

                    m_opusSink = await OpusCodec.CreateMediaSinkAsync(m_file);

                    await m_mediaCapture.StartRecordToCustomSinkAsync(wavEncodingProfile, m_opusSink);
                }
            }

            public async Task StopAsync()
            {
                if (m_lowLag != null)
                {
                    await m_lowLag.StopAsync();
                }
                else
                {
                    await m_mediaCapture.StopRecordAsync();
                }

                m_mediaCapture.Dispose();
                m_mediaCapture = null;

                if (m_opusSink is IDisposable disposable)
                {
                    disposable.Dispose();
                    m_opusSink = null;
                }
            }

            #endregion
        }

        public event EventHandler RecordingStarted;
        public event EventHandler RecordingStopped;
    }
}
