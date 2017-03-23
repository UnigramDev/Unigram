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
                if (_recorder.m_mediaCapture == null)
                    throw new InvalidOperationException("Cannot stop while not recording");

                await _recorder.m_mediaCapture.StopRecordAsync();
                _recorder.Stop();
            }

            _file = await ApplicationData.Current.LocalFolder.CreateFileAsync("temp\\recording.ogg", CreationCollisionOption.ReplaceExisting);
            _recorder = new OpusRecorder(_file);

            /* This following was moved from sub thread, because of a exception which comes from that device initializiation
             * is only allowed from UI thread!
             */
            if (_recorder.m_mediaCapture != null)
                throw new InvalidOperationException("Cannot start while recording");
            try
            {
                _recorder.m_mediaCapture = new MediaCapture();
                await _recorder.m_mediaCapture.InitializeAsync(_recorder.settings);
                await _recorder.StartAsync();
            }
            catch (UnauthorizedAccessException)
            {
                await new Windows.UI.Popups.MessageDialog("The access to microphone was denied!").ShowAsync();
                return;
            }
            catch(Exception)
            {
                await new Windows.UI.Popups.MessageDialog("The app couldn't initialize microphone!").ShowAsync();
                return;
            }

            _isPressed = true;
            _cancelOnRelease = false;
            _start = DateTime.Now;
        }

        private async void Stop()
        {
            if (_recorder?.IsRecording == true)
            {
                _recorder.Stop();
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
                get { return m_mediaCapture != null; }
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
                settings.MemoryPreference = MediaCaptureMemoryPreference.Auto;
                settings.MemoryPreference = MediaCaptureMemoryPreference.Auto;
                settings.MemoryPreference = MediaCaptureMemoryPreference.Auto;
                settings.SharingMode = MediaCaptureSharingMode.SharedReadOnly;
                settings.StreamingCaptureMode = StreamingCaptureMode.Audio;
            }

            public async Task StartAsync()
            {
                m_opusSink = await OpusCodec.CreateMediaSinkAsync(m_file);

                var wavEncodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                wavEncodingProfile.Audio.BitsPerSample = 16;
                wavEncodingProfile.Audio.SampleRate = 48000;
                wavEncodingProfile.Audio.ChannelCount = 1;
                await m_mediaCapture.StartRecordToCustomSinkAsync(wavEncodingProfile, m_opusSink);
            }

            public void Stop()
            {
                m_mediaCapture.Dispose();
                m_mediaCapture = null;
                if(m_opusSink != null)
                {
                    ((IDisposable)m_opusSink).Dispose();
                    m_opusSink = null;
                }
            }

            #endregion
        }
    }
}
