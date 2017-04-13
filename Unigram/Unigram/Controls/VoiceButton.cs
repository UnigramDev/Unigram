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
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class VoiceButton : GlyphToggleButton
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private OpusRecorder _recorder;
        private StorageFile _file;
        private bool _cancelOnRelease;
        private bool _pressed;
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

        public VoiceButton()
        {
            DefaultStyleKey = typeof(VoiceButton);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            Start();
            CapturePointer(e.Pointer);

            base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);

            _pressed = false;

            Stop();

            ReleasePointerCapture(e.Pointer);
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
            Task.Run(async () =>
            {
                _startReset.WaitOne();
                _startReset.Reset();

                _start = DateTime.Now;

                Execute.BeginOnUIThread(() =>
                {
                    //_roundView.IsOpen = true;
                    RecordingStarted?.Invoke(this, EventArgs.Empty);
                });

                _file = await ApplicationData.Current.LocalFolder.CreateFileAsync("temp\\recording.ogg", CreationCollisionOption.ReplaceExisting);
                _recorder = new OpusRecorder(_file);

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
                    await _recorder.m_mediaCapture.InitializeAsync(_recorder.settings);
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

                Execute.BeginOnUIThread(() =>
                {
                    //_roundView.IsOpen = false;
                    RecordingStopped?.Invoke(this, EventArgs.Empty);
                });

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
                        await ViewModel.SendAudioAsync(_file, (int)elapsed.TotalSeconds, true, null, null, null);
                    });
                }

                _startReset.Set();
            });
        }

        internal sealed class OpusRecorder
        {
            #region fields

            private bool m_isRecording;

            private StorageFile m_file;
            private IMediaExtension m_opusSink;
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

            public OpusRecorder(StorageFile file)
            {
                m_file = file;

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
                settings.StreamingCaptureMode = StreamingCaptureMode.Audio;
            }

            public async Task StartAsync()
            {
                m_isRecording = true;
                m_opusSink = await OpusCodec.CreateMediaSinkAsync(m_file);

                var wavEncodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                wavEncodingProfile.Audio.BitsPerSample = 16;
                wavEncodingProfile.Audio.SampleRate = 48000;
                wavEncodingProfile.Audio.ChannelCount = 1;

                await m_mediaCapture.StartRecordToCustomSinkAsync(wavEncodingProfile, m_opusSink);
            }

            public async Task StopAsync()
            {
                await m_mediaCapture.StopRecordAsync();

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
