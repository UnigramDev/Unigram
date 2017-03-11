using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class VoiceButton : GlyphHyperlinkButton
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private OpusRecorder _recorder;
        private StorageFile _file;
        private bool _cancelOnRelease;
        private bool _isPressed;
        private DateTime _start;

        public VoiceButton()
        {
            DefaultStyleKey = typeof(VoiceButton);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            Start();

            CapturePointer(e.Pointer);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);

            _isPressed = false;

            Stop();

            ReleasePointerCapture(e.Pointer);
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);

            if (_isPressed)
            {
                _cancelOnRelease = false;
            }
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);

            if (_isPressed)
            {
                _cancelOnRelease = true;
            }
        }

        private async void Start()
        {
            if (_recorder?.IsRecording == true)
            {
                await _recorder.StopAsync();
            }

            _file = await ApplicationData.Current.LocalFolder.CreateFileAsync("temp\\recording.ogg", CreationCollisionOption.ReplaceExisting);
            _recorder = new OpusRecorder(_file);
            await _recorder.StartAsync();

            _isPressed = true;
            _cancelOnRelease = false;
            _start = DateTime.Now;
        }

        private async void Stop()
        {
            if (_recorder?.IsRecording == true)
            {
                await _recorder.StopAsync();
            }

            if (_cancelOnRelease)
            {
                await _file.DeleteAsync();
            }
            else if (_file != null)
            {
                await ViewModel.SendAudioAsync(_file, (int)(DateTime.Now - _start).TotalSeconds, true, null, null, null);
            }
        }

        internal sealed class OpusRecorder
        {
            #region fields

            private StorageFile m_file;
            private IMediaExtension m_opusSink;
            private MediaCapture m_mediaCapture;

            #endregion

            #region properties

            public StorageFile File
            {
                get { return m_file; }
            }

            public bool IsRecording
            {
                get { return m_mediaCapture != null; }
            }

            #endregion

            #region constructors

            public OpusRecorder(StorageFile file)
            {
                m_file = file;
            }

            #endregion

            #region methods

            public async Task StartAsync()
            {
                if (m_mediaCapture != null)
                    throw new InvalidOperationException("Cannot start while recording");

                m_mediaCapture = new MediaCapture();
                await m_mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings()
                {
                    MediaCategory = MediaCategory.Speech,
                    AudioProcessing = AudioProcessing.Default,
                    MemoryPreference = MediaCaptureMemoryPreference.Auto,
                    SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                    StreamingCaptureMode = StreamingCaptureMode.Audio,
                });

                m_opusSink = await OpusCodec.CreateMediaSinkAsync(m_file);

                var wavEncodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                wavEncodingProfile.Audio.BitsPerSample = 16;
                wavEncodingProfile.Audio.SampleRate = 48000;
                wavEncodingProfile.Audio.ChannelCount = 1;
                await m_mediaCapture.StartRecordToCustomSinkAsync(wavEncodingProfile, m_opusSink);
            }

            public async Task StopAsync()
            {
                if (m_mediaCapture == null)
                    throw new InvalidOperationException("Cannot stop while not recording");

                await m_mediaCapture.StopRecordAsync();

                m_mediaCapture.Dispose();
                m_mediaCapture = null;

                ((IDisposable)m_opusSink).Dispose();
                m_opusSink = null;
            }

            #endregion
        }
    }
}
