//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.System;
using Windows.System.Display;
using Windows.System.Profile;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Chats
{
    public enum ChatRecordMode
    {
        Voice,
        Video
    }

    public class ChatRecordButton : AnimatedIconToggleButton
    {
        public ComposeViewModel ViewModel => DataContext as ComposeViewModel;

        private AnimatedIcon Icon;
        private Visual _icon;

        private readonly DispatcherTimer _timer;
        private readonly Recorder _recorder;

        private DateTime _start;

        public TimeSpan Elapsed => DateTime.Now - _start;

        public bool IsRecording => _recordingAudioVideo;
        public bool IsLocked => _recordingLocked;

        private bool _isRestricted;
        public bool IsRestricted
        {
            get => _isRestricted;
            set
            {
                if (_isRestricted != value)
                {
                    _isRestricted = value;

                    if (_icon != null)
                    {
                        var opacity = _icon.Compositor.CreateScalarKeyFrameAnimation();
                        opacity.InsertKeyFrame(0, value ? 1 : 0.2f);
                        opacity.InsertKeyFrame(1, value ? 0.2f : 1);

                        _icon.StartAnimation("Opacity", opacity);
                    }
                }
            }
        }

        public ChatRecordMode Mode
        {
            get => IsChecked.HasValue && IsChecked.Value ? ChatRecordMode.Video : ChatRecordMode.Voice;
            set
            {
                IsChecked = value == ChatRecordMode.Video;

                AutomationProperties.SetName(this, value == ChatRecordMode.Video ? Strings.AccDescrVideoMessage : Strings.AccDescrVoiceMessage);
                ToolTipService.SetToolTip(this, value == ChatRecordMode.Video ? Strings.AccDescrVideoMessage : Strings.AccDescrVoiceMessage);
            }
        }

        public byte[] GetWaveform() => _recorder.GetWaveform();

        public ChatRecordButton()
        {
            Mode = ChatRecordMode.Voice;

            ClickMode = ClickMode.Press;
            Click += OnClick;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(300);
            _timer.Tick += (s, args) =>
            {
                Logger.Debug("Timer Tick, check for permissions");

                _timer.Stop();
                RecordAudioVideoRunnable();
            };

            _recorder = Recorder.Current;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override void OnApplyTemplate()
        {
            Icon = GetTemplateChild(nameof(Icon)) as AnimatedIcon;
            Icon.PointerReleased += OnPointerReleased;
            Icon.PointerCanceled += OnPointerCanceled;

            _icon = ElementCompositionPreview.GetElementVisual(Icon);
            _icon.Opacity = IsRestricted ? 0.2f : 1;

            base.OnApplyTemplate();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _recorder.RecordingStarted += Current_RecordingStarted;
            _recorder.RecordingStopped += Current_RecordingStopped;
            _recorder.RecordingFailed += Current_RecordingStopped;

            _recorder.QuantumProcessed = amplitude => QuantumProcessed?.Invoke(this, amplitude);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _recorder.RecordingStarted -= Current_RecordingStarted;
            _recorder.RecordingStopped -= Current_RecordingStopped;
            _recorder.RecordingFailed -= Current_RecordingStopped;

            _recorder.QuantumProcessed = null;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            Icon.PointerCaptureLost += OnPointerCaptureLost;
            Icon.CapturePointer(e.Pointer);
            base.OnPointerPressed(e);
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Logger.Debug("OnPointerReleased");

            Icon.PointerCaptureLost -= OnPointerCaptureLost;
            Icon.ReleasePointerCapture(e.Pointer);

            OnRelease();
        }

        private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            Logger.Debug("OnPointerCanceled");

            Icon.PointerCaptureLost -= OnPointerCaptureLost;
            Icon.ReleasePointerCapture(e.Pointer);

            OnRelease();
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            Logger.Debug("OnPointerCaptureLost");
            OnRelease();
        }

        private async void RecordAudioVideoRunnable()
        {
            _calledRecordRunnable = true;
            _recordAudioVideoRunnableStarted = false;

            var permissions = await CheckAccessAsync(Mode);
            if (permissions == false)
            {
                return;
            }

            //ViewModel.PlaybackService.Pause();

            Logger.Debug("Permissions granted, mode: " + Mode);

            _recorder.Start(Mode, ViewModel.Chat);
            UpdateRecordingInterface();
        }

        private void Current_RecordingStarted(object sender, EventArgs e)
        {
            if (!_recordingAudioVideo)
            {
                _recordingAudioVideo = true;
                UpdateRecordingInterface();

                if (_enqueuedLocking)
                {
                    LockRecording();
                }
            }
        }

        private void Current_RecordingStopped(object sender, EventArgs e)
        {
            if (_recordingAudioVideo)
            {
                // cancel typing
                _recordingAudioVideo = false;
                UpdateRecordingInterface();
            }
        }

        private int recordInterfaceState;

        private DisplayRequest _request;

        private void UpdateRecordingInterface()
        {
            Logger.Debug("Updating interface, state: " + recordInterfaceState);

            if (_recordingLocked && _recordingAudioVideo)
            {
                if (recordInterfaceState == 2)
                {
                    return;
                }
                recordInterfaceState = 2;

                this.BeginOnUIThread(() =>
                {
                    VisualStateManager.GoToState(this, "Locked", false);

                    ClickMode = ClickMode.Press;
                    RecordingLocked?.Invoke(this, EventArgs.Empty);
                });
            }
            else if (_recordingLocked && _recordingStopped)
            {
                if (recordInterfaceState == 3)
                {
                    return;
                }
                recordInterfaceState = 3;

                this.BeginOnUIThread(() =>
                {
                    VisualStateManager.GoToState(this, "Locked", false);

                    ClickMode = ClickMode.Press;
                    RecordingStopped?.Invoke(this, EventArgs.Empty);
                });
            }
            else if (_recordingAudioVideo)
            {
                if (recordInterfaceState == 1)
                {
                    return;
                }
                recordInterfaceState = 1;

                _recordingLocked = false;

                _start = DateTime.Now;

                this.BeginOnUIThread(() =>
                {
                    VisualStateManager.GoToState(this, "Started", false);

                    ClickMode = ClickMode.Release;
                    RecordingStarted?.Invoke(this, EventArgs.Empty);

                    try
                    {
                        if (_request == null)
                        {
                            _request = new DisplayRequest();
                            _request.RequestActive();
                        }
                    }
                    catch { }
                });
            }
            else
            {
                if (recordInterfaceState == 0)
                {
                    return;
                }
                recordInterfaceState = 0;

                _recordingStopped = false;
                _recordingLocked = false;

                this.BeginOnUIThread(() =>
                {
                    VisualStateManager.GoToState(this, "Stopped", false);

                    ClickMode = ClickMode.Press;
                    RecordingStopped?.Invoke(this, EventArgs.Empty);

                    if (_request != null)
                    {
                        try
                        {
                            _request.RequestRelease();
                            _request = null;
                        }
                        catch { }
                    }
                });
            }

            Logger.Debug("Updated interface, state: " + recordInterfaceState);
        }

        private async void OnClick(object sender, RoutedEventArgs e)
        {
            if (ClickMode == ClickMode.Press)
            {
                if (IsRestricted)
                {
                    var message = Mode == ChatRecordMode.Video
                        ? Strings.VideoMessagesRestrictedByPrivacy
                        : Strings.VoiceMessagesRestrictedByPrivacy;

                    var formatted = string.Format(message, ViewModel.Chat.Title);
                    var markdown = ClientEx.ParseMarkdown(formatted);
                    Window.Current.ShowTeachingTip(this, markdown, TeachingTipPlacementMode.TopLeft, autoDismiss: true);
                    return;
                }

                Logger.Debug("Click mode: Press");

                if (_recordingLocked)
                {
                    if (!_hasRecordVideo || _calledRecordRunnable)
                    {
                        _recorder.Stop(ViewModel, false);
                        _recordingAudioVideo = false;
                        UpdateRecordingInterface();
                    }

                    return;
                }

                ClickMode = ClickMode.Release;

                var restricted = await ViewModel.VerifyRightsAsync(x => Mode == ChatRecordMode.Video ? x.CanSendVideoNotes : x.CanSendVoiceNotes, Strings.GlobalAttachMediaRestricted, Strings.AttachMediaRestrictedForever, Strings.AttachMediaRestricted);
                if (restricted)
                {
                    return;
                }

                _timer.Stop();

                if (_hasRecordVideo)
                {
                    Logger.Debug("Can record videos, start timer to allow switch");

                    _calledRecordRunnable = false;
                    _recordAudioVideoRunnableStarted = true;
                    _timer.Start();
                }
                else
                {
                    RecordAudioVideoRunnable();
                }
            }
            else
            {
                Logger.Debug("Click mode: Release");
                OnRelease();
            }
        }

        public void Release()
        {
            if (_recordingLocked)
            {
                Logger.Debug("Click mode: Release - Programmatic");

                if (!_hasRecordVideo || _calledRecordRunnable)
                {
                    _recorder.Stop(ViewModel, false);
                    _recordingAudioVideo = false;
                    UpdateRecordingInterface();
                }
            }
        }

        private void OnRelease()
        {
            ClickMode = ClickMode.Press;

            if (_recordingLocked)
            {
                Logger.Debug("Recording is locked, abort");
                return;
            }
            if (_recordAudioVideoRunnableStarted && _timer.IsEnabled)
            {
                Logger.Debug("Timer should still tick, change mode to: " + (Mode == ChatRecordMode.Video ? ChatRecordMode.Voice : ChatRecordMode.Video));

                _timer.Stop();
                Mode = Mode == ChatRecordMode.Video ? ChatRecordMode.Voice : ChatRecordMode.Video;

                var message = Mode == ChatRecordMode.Video
                    ? Strings.HoldToVideo
                    : Strings.HoldToAudio;

                Window.Current.ShowTeachingTip(this, message, TeachingTipPlacementMode.TopLeft, autoDismiss: true);
            }
            else if (!_hasRecordVideo || _calledRecordRunnable)
            {
                Logger.Debug("Timer has tick, stopping recording");

                _recorder.Stop(ViewModel, false);
                _recordingAudioVideo = false;
                UpdateRecordingInterface();
            }
        }

        private async Task<bool> CheckAccessAsync(ChatRecordMode mode)
        {
            try
            {
                var audioPermission = await CheckDeviceAccessAsync(true, mode);
                if (audioPermission == false)
                {
                    return false;
                }

                if (mode == ChatRecordMode.Video)
                {
                    var videoPermission = await CheckDeviceAccessAsync(false, ChatRecordMode.Video);
                    if (videoPermission == false)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                // TODO: notify user
                return false;
            }
        }

        private async Task<bool> CheckDeviceAccessAsync(bool audio, ChatRecordMode mode)
        {
            // For some reason, as far as I understood, CurrentStatus is always Unspecified on Xbox
            if (string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Xbox"))
            {
                return true;
            }

            var access = DeviceAccessInformation.CreateFromDeviceClass(audio ? DeviceClass.AudioCapture : DeviceClass.VideoCapture);
            if (access.CurrentStatus == DeviceAccessStatus.Unspecified)
            {
                MediaCapture capture = null;
                try
                {
                    capture = new MediaCapture();
                    var settings = new MediaCaptureInitializationSettings();
                    settings.StreamingCaptureMode = mode == ChatRecordMode.Video
                        ? StreamingCaptureMode.AudioAndVideo
                        : StreamingCaptureMode.Audio;
                    await capture.InitializeAsync(settings);
                }
                catch { }
                finally
                {
                    if (capture != null)
                    {
                        capture.Dispose();
                        capture = null;
                    }
                }

                return false;
            }
            else if (access.CurrentStatus != DeviceAccessStatus.Allowed)
            {
                var message = audio
                    ? mode == ChatRecordMode.Voice
                    ? Strings.PermissionNoAudio
                    : Strings.PermissionNoAudioVideo
                    : Strings.PermissionNoCamera;

                this.BeginOnUIThread(async () =>
                {
                    var confirm = await MessagePopup.ShowAsync(message, Strings.AppName, Strings.PermissionOpenSettings, Strings.OK);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                    }
                });

                return false;
            }

            return true;
        }

        private readonly bool _hasRecordVideo = true;

        private bool _calledRecordRunnable;
        private bool _recordAudioVideoRunnableStarted;

        private bool _recordingAudioVideo;

        private bool _recordingStopped;

        private bool _recordingLocked;
        private bool _enqueuedLocking;

        public void StopRecording(bool cancel)
        {
            _recorder.Stop(null, cancel ? true : new bool?());
            _recordingStopped = !cancel;
            _recordingAudioVideo = false;
            UpdateRecordingInterface();
        }

        public void LockRecording()
        {
            Logger.Debug("Locking recording");

            _enqueuedLocking = false;
            _recordingLocked = true;
            UpdateRecordingInterface();
        }

        public async void ToggleRecording()
        {
            if (_recordingLocked)
            {
                if (!_hasRecordVideo || _calledRecordRunnable)
                {
                    _recorder.Stop(ViewModel, false);
                    _recordingAudioVideo = false;
                    UpdateRecordingInterface();
                }
            }
            else
            {
                var restricted = await ViewModel.VerifyRightsAsync(x => Mode == ChatRecordMode.Video ? x.CanSendVideoNotes : x.CanSendVoiceNotes, Strings.GlobalAttachMediaRestricted, Strings.AttachMediaRestrictedForever, Strings.AttachMediaRestricted);
                if (restricted)
                {
                    return;
                }

                _enqueuedLocking = true;
                RecordAudioVideoRunnable();
            }
        }

        public event EventHandler RecordingStarted;
        public event EventHandler RecordingStopped;
        public event EventHandler RecordingLocked;

        public event EventHandler<float> QuantumProcessed;

        public class Recorder
        {
            public event EventHandler RecordingFailed;
            public event EventHandler RecordingStarted;
            public event EventHandler RecordingStopped;
            public event EventHandler RecordingTooShort;

            public Action<float> QuantumProcessed;

            [ThreadStatic]
            private static Recorder _current;
            public static Recorder Current => _current ??= new Recorder();

            private readonly ConcurrentQueueWorker _recordQueue;
            private readonly DispatcherQueue _dispatcherQueue;

            private OpusRecorder _recorder;
            private StorageFile _file;
            private ChatRecordMode _mode;
            private Chat _chat;
            private DateTime _start;

            private MediaFrameReader _reader;

            public Recorder()
            {
                _recordQueue = new ConcurrentQueueWorker(1);
                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            }

            public async void Start(ChatRecordMode mode, Chat chat)
            {
                Logger.Debug("Start invoked, mode: " + mode);

                await _recordQueue.Enqueue(async () =>
                {
                    Logger.Debug("Enqueued start invoked");

                    if (_recorder != null)
                    {
                        Logger.Debug("_recorder != null, abort");

                        RecordingFailed?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    RecordingStarted?.Invoke(this, EventArgs.Empty);

                    try
                    {
                        // Create a new temporary file for the recording
                        var fileName = string.Format(mode == ChatRecordMode.Video
                            ? "video_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.mp4"
                            : "voice_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.oga", DateTime.Now);
                        var file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName);

                        _mode = mode;
                        _file = file;
                        _chat = chat;
                        _recorder = new OpusRecorder(file, mode == ChatRecordMode.Video);

                        _recorder.m_mediaCapture = new MediaCapture();

                        if (mode == ChatRecordMode.Video)
                        {
                            var cameraDevice = await _recorder.FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Front);
                            if (cameraDevice == null)
                            {
                                // TODO: ...
                            }

                            // Figure out where the camera is located
                            if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
                            {
                                // No information on the location of the camera, assume it's an external camera, not integrated on the device
                                _recorder._externalCamera = true;
                            }
                            else
                            {
                                // Camera is fixed on the device
                                _recorder._externalCamera = false;

                                // Only mirror the preview if the camera is on the front panel
                                _recorder._mirroringPreview = cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front;
                            }

                            _recorder.settings.VideoDeviceId = cameraDevice.Id;
                        }

                        await _recorder.m_mediaCapture.InitializeAsync(_recorder.settings);

                        Logger.Debug("Devices initialized, starting");

                        await InitializeQuantumAsync();
                        await _recorder.StartAsync();

                        Logger.Debug("Recording started at " + DateTime.Now);

                        _start = DateTime.Now;
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug("Failed to initialize devices, abort: " + ex);

                        if (_reader != null)
                        {
                            _reader.FrameArrived -= OnAudioFrameArrived;

                            _reader.Dispose();
                            _reader = null;
                        }

                        _recorder?.Dispose();
                        _recorder = null;

                        _file = null;

                        RecordingFailed?.Invoke(this, EventArgs.Empty);
                    }
                });
            }

            public async Task InitializeQuantumAsync()
            {
                var frameSource = _recorder.m_mediaCapture.FrameSources.FirstOrDefault(x => x.Value.Info.MediaStreamType == MediaStreamType.Audio);
                if (frameSource.Value == null)
                {
                    Logger.Info("No audio frame source was found.");
                    return;
                }

                var format = frameSource.Value.CurrentFormat;
                if (format.Subtype != MediaEncodingSubtypes.Float)
                {
                    Logger.Info("No audio frame source was found.");
                    return;
                }

                var mediaFrameReader = await _recorder.m_mediaCapture.CreateFrameReaderAsync(frameSource.Value);

                // Optionally set acquisition mode. Buffered is the default mode for audio.
                mediaFrameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
                mediaFrameReader.FrameArrived += OnAudioFrameArrived;

                var status = await mediaFrameReader.StartAsync();
                if (status != MediaFrameReaderStartStatus.Success)
                {
                    Logger.Info("The MediaFrameReader couldn't start.");
                }

                _reader = mediaFrameReader;
            }

            private readonly float[] _compressedWaveformSamples = new float[200];
            private int _compressedWaveformPosition = 0;

            private float _currentPeak;
            private int _currentPeakCount;
            private int _peakCompressionFactor = 1;

            private float _micLevelPeak = 0;
            private int _micLevelPeakCount = 0;

            private int _lastUpdateTime;

            [ComImport]
            [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            private unsafe interface IMemoryBufferByteAccess
            {
                void GetBuffer(out byte* buffer, out uint capacity);
            }

            private unsafe void OnAudioFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
            {
                using var reference = sender.TryAcquireLatestFrame();
                if (reference == null)
                {
                    return;
                }

                if (Environment.TickCount - _lastUpdateTime < 64)
                {
                    return;
                }

                _lastUpdateTime = Environment.TickCount;

                using var frame = reference.AudioMediaFrame.GetAudioFrame();

                using var audioBuffer = frame.LockBuffer(AudioBufferAccessMode.Read);
                using var bufferReference = audioBuffer.CreateReference();

                // Get the buffer from the AudioFrame
                ((IMemoryBufferByteAccess)bufferReference).GetBuffer(out byte* buffer, out uint capacity);

                var samples = (float*)buffer;
                var count = capacity / 4;

                for (int i = 0; i < count; i++)
                {
                    var sample = samples[i];
                    if (sample < 0)
                    {
                        sample = Math.Abs(sample);
                    }

                    _currentPeak = Math.Max(_currentPeak, sample);
                    _currentPeakCount++;

                    if (_currentPeakCount == _peakCompressionFactor)
                    {
                        _compressedWaveformSamples[_compressedWaveformPosition++] = _currentPeak;

                        _currentPeakCount = 0;

                        if (_compressedWaveformPosition == _compressedWaveformSamples.Length)
                        {
                            for (int j = 0; j < _compressedWaveformSamples.Length / 2; j++)
                            {
                                _compressedWaveformSamples[j] = Math.Max(_compressedWaveformSamples[j * 2 + 0], _compressedWaveformSamples[j * 2 + 1]);
                            }

                            _compressedWaveformPosition = _compressedWaveformSamples.Length / 2;
                            _peakCompressionFactor *= 2;
                        }

                    }

                    if (_micLevelPeak < sample)
                    {
                        _micLevelPeak = sample;
                    }

                    _micLevelPeakCount += 1;

                    if (_micLevelPeakCount >= 1200)
                    {
                        QuantumProcessed?.Invoke(_micLevelPeak);

                        _micLevelPeak = 0;
                        _micLevelPeakCount = 0;
                    }
                }
            }

            public unsafe byte[] GetWaveform()
            {
                var count = _compressedWaveformPosition;
                var scaledSamples = new short[100];

                for (int i = 0; i < count; i++)
                {
                    var sample = _compressedWaveformSamples[i] * short.MaxValue;
                    var index = i * 100 / count;
                    if (scaledSamples[index] < sample)
                    {
                        scaledSamples[index] = (short)sample;
                    }
                }

                short peak = 0;
                long sumSamples = 0;
                for (int i = 0; i < 100; i++)
                {
                    var sample = scaledSamples[i];
                    if (peak < sample)
                    {
                        peak = sample;
                    }
                    sumSamples += sample;
                }

                var calculatedPeak = (ushort)(sumSamples * 1.8 / 100.0);
                if (calculatedPeak < 2500)
                {
                    calculatedPeak = 2500;
                }

                for (int i = 0; i < 100; i++)
                {
                    uint sample = (ushort)scaledSamples[i];
                    var minPeak = Math.Min(sample, calculatedPeak);
                    var resultPeak = minPeak * 31 / calculatedPeak;
                    scaledSamples[i] = (short)/*clamping:*/ Math.Min(31, resultPeak);
                }

                var bitstreamLength = scaledSamples.Length * 5 / 8 + 1;
                var result = new byte[bitstreamLength];

                fixed (byte* data = result)
                {
                    static void set_bits(byte* bytes, int bitOffset, int value)
                    {
                        bytes += bitOffset / 8;
                        bitOffset %= 8;
                        *(int*)bytes |= value << bitOffset;
                    }

                    for (int i = 0; i < scaledSamples.Length; i++)
                    {
                        set_bits(data, i * 5, scaledSamples[i] & 31);
                    }
                }

                return result;
            }

            public async void Stop(ComposeViewModel viewModel, bool? cancel)
            {
                Logger.Debug("Stop invoked, cancel: " + cancel);

                await _recordQueue.Enqueue(async () =>
                {
                    Logger.Debug("Enqueued stop invoked");

                    var recorder = _recorder;
                    var file = _file;
                    var mode = _mode;
                    var chat = _chat;

                    var reader = _reader;

                    if (recorder == null || file == null || chat == null)
                    {
                        Logger.Debug("recorder or file == null, abort");
                        return;
                    }

                    RecordingStopped?.Invoke(this, EventArgs.Empty);

                    var now = DateTime.Now;
                    var elapsed = now - _start;

                    Logger.Debug("stopping reader");

                    if (reader != null)
                    {
                        reader.FrameArrived -= OnAudioFrameArrived;
                        reader.Dispose();

                        QuantumProcessed?.Invoke(0);
                    }

                    Logger.Debug("stopping recorder, elapsed " + elapsed);

                    await recorder.StopAsync();

                    Logger.Debug("recorder stopped");

                    if (cancel == true || elapsed.TotalMilliseconds < 700)
                    {
                        try
                        {
                            await file.DeleteAsync();
                        }
                        catch { }

                        Logger.Debug("recording canceled or too short, abort");

                        if (elapsed.TotalMilliseconds < 700)
                        {
                            RecordingTooShort?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    else
                    {
                        Logger.Debug("sending recorded file");

                        if (cancel == false)
                        {
                            Send(viewModel, mode, chat, file, recorder._mirroringPreview, (int)elapsed.TotalSeconds);
                        }
                    }

                    _recorder = null;
                    _file = null;

                    _reader = null;
                });
            }

            private async void Send(ComposeViewModel viewModel, ChatRecordMode mode, Chat chat, StorageFile file, bool mirroring, int duration)
            {
                if (mode == ChatRecordMode.Video)
                {
                    var props = await file.Properties.GetVideoPropertiesAsync();
                    var width = props.GetWidth();
                    var height = props.GetHeight();
                    var x = 0d;
                    var y = 0d;

                    if (width > height)
                    {
                        x = (width - height) / 2;
                        width = height;
                    }
                    else if (height > width)
                    {
                        y = (height - width) / 2;
                        height = width;
                    }

                    var length = viewModel.ClientService.Options.SuggestedVideoNoteLength;
                    var videoBitrate = viewModel.ClientService.Options.SuggestedVideoNoteVideoBitrate;
                    var audioBitrate = viewModel.ClientService.Options.SuggestedVideoNoteAudioBitrate;

                    var transform = new VideoTransformEffectDefinition();
                    transform.CropRectangle = new Rect(x, y, width, height);
                    transform.OutputSize = new Size(length, length);
                    transform.Mirror = mirroring ? MediaMirroringOptions.Horizontal : MediaMirroringOptions.None;

                    var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
                    profile.Video.Width = (uint)length;
                    profile.Video.Height = (uint)length;
                    profile.Video.Bitrate = (uint)videoBitrate * 1000;
                    profile.Audio.Bitrate = (uint)audioBitrate * 1000;

                    try
                    {
                        _dispatcherQueue.TryEnqueue(() => _ = viewModel.SendVideoNoteAsync(file, profile, transform));
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        _dispatcherQueue.TryEnqueue(() => _ = viewModel.SendVoiceNoteAsync(file, duration, null));
                    }
                    catch { }
                }
            }

            internal sealed class OpusRecorder
            {
                private readonly bool m_isVideo;

                private readonly StorageFile m_file;
                private LowLagMediaRecording m_lowLag;
                public MediaCapture m_mediaCapture;
                public MediaCaptureInitializationSettings settings;

                // Information about the camera device
                public bool _mirroringPreview;
                public bool _externalCamera;

                //// Rotation Helper to simplify handling rotation compensation for the camera streams
                //public CameraRotationHelper _rotationHelper;

                public StorageFile File
                {
                    get { return m_file; }
                }

                public OpusRecorder(StorageFile file, bool video)
                {
                    m_file = file;
                    m_isVideo = video;
                    InitializeSettings();
                }

                private void InitializeSettings()
                {
                    settings = new MediaCaptureInitializationSettings();
                    settings.MediaCategory = MediaCategory.Media;
                    settings.AudioProcessing = AudioProcessing.Default;
                    settings.MemoryPreference = MediaCaptureMemoryPreference.Auto;
                    settings.SharingMode = MediaCaptureSharingMode.SharedReadOnly;
                    settings.StreamingCaptureMode = m_isVideo ? StreamingCaptureMode.AudioAndVideo : StreamingCaptureMode.Audio;
                }

                public async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
                {
                    // Get available devices for capturing pictures
                    var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

                    // Get the desired camera by panel
                    DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

                    // If there is no device mounted on the desired panel, return the first device found
                    return desiredDevice ?? allVideoDevices.FirstOrDefault();
                }

                public async Task StartAsync()
                {
                    if (m_isVideo)
                    {
                        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                        //m_lowLag = await m_mediaCapture.PrepareLowLagRecordToStorageFileAsync(profile, m_file);

                        //await m_lowLag.StartAsync();
                        await m_mediaCapture.StartRecordToStorageFileAsync(profile, m_file);
                    }
                    else
                    {
                        var wavEncodingProfile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                        wavEncodingProfile.Audio.BitsPerSample = 16;
                        wavEncodingProfile.Audio.SampleRate = 48000;
                        wavEncodingProfile.Audio.ChannelCount = 1;

                        await m_mediaCapture.StartRecordToStorageFileAsync(wavEncodingProfile, m_file);
                    }
                }

                public async Task StopAsync()
                {
                    try
                    {
                        if (m_lowLag != null)
                        {
                            await m_lowLag.StopAsync();
                            await m_lowLag.FinishAsync();
                        }
                        else
                        {
                            await m_mediaCapture.StopRecordAsync();
                        }

                        m_mediaCapture.Dispose();
                        m_mediaCapture = null;
                    }
                    catch { }
                }

                public void Dispose()
                {
                    try
                    {
                        m_lowLag = null;

                        m_mediaCapture.Dispose();
                        m_mediaCapture = null;
                    }
                    catch { }
                }
            }
        }
    }
}
