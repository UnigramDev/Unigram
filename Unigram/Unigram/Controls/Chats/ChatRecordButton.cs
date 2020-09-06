using System;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Logs;
using Unigram.Native.Media;
using Unigram.ViewModels;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.System;
using Windows.System.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls.Chats
{
    public enum ChatRecordMode
    {
        Voice,
        Video
    }

    public class ChatRecordButton : GlyphToggleButton
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private DispatcherTimer _timer;
        private Recorder _recorder;

        private DateTime _start;

        public TimeSpan Elapsed => DateTime.Now - _start;

        public bool IsRecording => _recordingAudioVideo;
        public bool IsLocked => _recordingLocked;

        public ChatRecordMode Mode
        {
            get
            {
                return IsChecked.HasValue && IsChecked.Value ? ChatRecordMode.Video : ChatRecordMode.Voice;
            }
            set
            {
                IsChecked = value == ChatRecordMode.Video;

                AutomationProperties.SetName(this, value == ChatRecordMode.Video ? Strings.Resources.AccDescrVideoMessage : Strings.Resources.AccDescrVoiceMessage);
                ToolTipService.SetToolTip(this, value == ChatRecordMode.Video ? Strings.Resources.AccDescrVideoMessage : Strings.Resources.AccDescrVoiceMessage);
            }
        }

        public ChatRecordButton()
        {
            DefaultStyleKey = typeof(ChatRecordButton);

            Mode = ChatRecordMode.Voice;

            ClickMode = ClickMode.Press;
            Click += OnClick;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(300);
            _timer.Tick += (s, args) =>
            {
                Logger.Debug(Target.Recording, "Timer Tick, check for permissions");

                _timer.Stop();
                RecordAudioVideoRunnable();
            };

            _recorder = Recorder.Current;
            _recorder.RecordingStarted += Current_RecordingStarted;
            _recorder.RecordingStopped += Current_RecordingStopped;
            _recorder.RecordingFailed += Current_RecordingStopped;
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

            Logger.Debug(Target.Recording, "Permissions granted, mode: " + Mode);

            _recorder.Start(Mode);
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
            Logger.Debug(Target.Recording, "Updating interface, state: " + recordInterfaceState);

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
            else if (_recordingAudioVideo)
            {
                if (recordInterfaceState == 1)
                {
                    return;
                }
                recordInterfaceState = 1;
                try
                {
                    if (_request == null)
                    {
                        _request = new DisplayRequest();
                        _request.GetType();
                    }
                }
                catch { }

                _recordingLocked = false;

                _start = DateTime.Now;

                this.BeginOnUIThread(() =>
                {
                    VisualStateManager.GoToState(this, "Started", false);

                    ClickMode = ClickMode.Release;
                    RecordingStarted?.Invoke(this, EventArgs.Empty);
                });
            }
            else
            {
                if (_request != null)
                {
                    try
                    {
                        _request.RequestRelease();
                        _request = null;
                    }
                    catch { }
                }
                if (recordInterfaceState == 0)
                {
                    return;
                }
                recordInterfaceState = 0;

                _recordingLocked = false;

                this.BeginOnUIThread(() =>
                {
                    VisualStateManager.GoToState(this, "Stopped", false);

                    ClickMode = ClickMode.Press;
                    RecordingStopped?.Invoke(this, EventArgs.Empty);
                });
            }

            Logger.Debug(Target.Recording, "Updated interface, state: " + recordInterfaceState);
        }

        private async void OnClick(object sender, RoutedEventArgs e)
        {
            if (ClickMode == ClickMode.Press)
            {
                Logger.Debug(Target.Recording, "Click mode: Press");

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

                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var restricted = await ViewModel.VerifyRightsAsync(chat, x => x.CanSendMediaMessages, Strings.Resources.GlobalAttachMediaRestricted, Strings.Resources.AttachMediaRestrictedForever, Strings.Resources.AttachMediaRestricted);
                if (restricted)
                {
                    return;
                }

                _timer.Stop();

                if (_hasRecordVideo)
                {
                    Logger.Debug(Target.Recording, "Can record videos, start timer to allow switch");

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
                Logger.Debug(Target.Recording, "Click mode: Release");

                ClickMode = ClickMode.Press;

                OnRelease();
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            CapturePointer(e.Pointer);
            base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            ReleasePointerCapture(e.Pointer);

            Logger.Debug(Target.Recording, "OnPointerReleased");

            OnRelease();
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            base.OnPointerCanceled(e);
            ReleasePointerCapture(e.Pointer);

            Logger.Debug(Target.Recording, "OnPointerCanceled");

            OnRelease();
        }

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            base.OnPointerCaptureLost(e);

            Logger.Debug(Target.Recording, "OnPointerCaptureLost");

            OnRelease();
        }

        private void OnRelease()
        {
            if (_recordingLocked)
            {
                Logger.Debug(Target.Recording, "Recording is locked, abort");
                return;
            }
            if (_recordAudioVideoRunnableStarted)
            {
                Logger.Debug(Target.Recording, "Timer should still tick, change mode to: " + (Mode == ChatRecordMode.Video ? ChatRecordMode.Voice : ChatRecordMode.Video));

                _timer.Stop();
                Mode = Mode == ChatRecordMode.Video ? ChatRecordMode.Voice : ChatRecordMode.Video;
            }
            else if (!_hasRecordVideo || _calledRecordRunnable)
            {
                Logger.Debug(Target.Recording, "Timer has tick, stopping recording");

                _recorder.Stop(ViewModel, false);
                _recordingAudioVideo = false;
                UpdateRecordingInterface();
            }
        }

        private async Task<bool> CheckAccessAsync(ChatRecordMode mode)
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

        private async Task<bool> CheckDeviceAccessAsync(bool audio, ChatRecordMode mode)
        {
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
                    ? Strings.Resources.PermissionNoAudio
                    : Strings.Resources.PermissionNoAudioVideo
                    : Strings.Resources.PermissionNoCamera;

                this.BeginOnUIThread(async () =>
                {
                    var confirm = await MessagePopup.ShowAsync(message, Strings.Resources.AppName, Strings.Resources.PermissionOpenSettings, Strings.Resources.OK);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                    }
                });

                return false;
            }

            return true;
        }

        private bool _hasRecordVideo = false;

        private bool _calledRecordRunnable;
        private bool _recordAudioVideoRunnableStarted;

        private bool _recordingAudioVideo;

        private bool _recordingLocked;
        private bool _enqueuedLocking;

        public void CancelRecording()
        {
            _recorder.Stop(null, true);
            _recordingAudioVideo = false;
            UpdateRecordingInterface();
        }

        public void LockRecording()
        {
            Logger.Debug(Target.Recording, "Locking recording");

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
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var restricted = await ViewModel.VerifyRightsAsync(chat, x => x.CanSendMediaMessages, Strings.Resources.GlobalAttachMediaRestricted, Strings.Resources.AttachMediaRestrictedForever, Strings.Resources.AttachMediaRestricted);
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

        class Recorder
        {
            public event EventHandler RecordingFailed;
            public event EventHandler RecordingStarted;
            public event EventHandler RecordingStopped;
            public event EventHandler RecordingTooShort;

            [ThreadStatic]
            private static Recorder _current;
            public static Recorder Current => _current = _current ?? new Recorder();

            private ConcurrentQueueWorker _recordQueue;

            private OpusRecorder _recorder;
            private StorageFile _file;
            private ChatRecordMode _mode;
            private DateTime _start;

            public Recorder()
            {
                _recordQueue = new ConcurrentQueueWorker(1);
            }

            public async void Start(ChatRecordMode mode)
            {
                Logger.Debug(Target.Recording, "Start invoked, mode: " + mode);

                await _recordQueue.Enqueue(async () =>
                {
                    Logger.Debug(Target.Recording, "Enqueued start invoked");

                    if (_recorder != null)
                    {
                        Logger.Debug(Target.Recording, "_recorder != null, abort");

                        RecordingFailed?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    // Create a new temporary file for the recording
                    var fileName = string.Format(mode == ChatRecordMode.Video
                        ? "video_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.mp4"
                        : "voice_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.ogg", DateTime.Now);
                    var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                    try
                    {
                        _mode = mode;
                        _file = cache;
                        _recorder = new OpusRecorder(cache, mode == ChatRecordMode.Video);

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

                        Logger.Debug(Target.Recording, "Devices initialized, starting");

                        await _recorder.StartAsync();

                        Logger.Debug(Target.Recording, "Recording started at " + DateTime.Now);

                        _start = DateTime.Now;
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(Target.Recording, "Failed to initialize devices, abort: " + ex);

                        _recorder.Dispose();
                        _recorder = null;

                        _file = null;

                        RecordingFailed?.Invoke(this, EventArgs.Empty);
                        return;
                    }

                    RecordingStarted?.Invoke(this, EventArgs.Empty);
                });
            }

            private async void RotationHelper_OrientationChanged(object sender, bool updatePreview)
            {
                if (updatePreview)
                {
                    await _recorder.SetPreviewRotationAsync();
                }
            }

            public async void Stop(DialogViewModel viewModel, bool cancel)
            {
                Logger.Debug(Target.Recording, "Stop invoked, cancel: " + cancel);

                await _recordQueue.Enqueue(async () =>
                {
                    Logger.Debug(Target.Recording, "Enqueued stop invoked");

                    var recorder = _recorder;
                    var file = _file;
                    var mode = _mode;

                    if (recorder == null || file == null)
                    {
                        Logger.Debug(Target.Recording, "recorder or file == null, abort");
                        return;
                    }

                    RecordingStopped?.Invoke(this, EventArgs.Empty);

                    var now = DateTime.Now;
                    var elapsed = now - _start;

                    Logger.Debug(Target.Recording, "stopping recorder, elapsed " + elapsed);

                    await recorder.StopAsync();

                    Logger.Debug(Target.Recording, "recorder stopped");

                    if (cancel || elapsed.TotalMilliseconds < 700)
                    {
                        try
                        {
                            await file.DeleteAsync();
                        }
                        catch { }

                        Logger.Debug(Target.Recording, "recording canceled or too short, abort");

                        if (elapsed.TotalMilliseconds < 700)
                        {
                            RecordingTooShort?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    else
                    {
                        Logger.Debug(Target.Recording, "sending recorded file");

                        Send(viewModel, mode, file, recorder._mirroringPreview, (int)elapsed.TotalSeconds);
                    }

                    _recorder = null;
                    _file = null;
                });
            }

            private async void Send(DialogViewModel viewModel, ChatRecordMode mode, StorageFile file, bool mirroring, int duration)
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

                    var transform = new VideoTransformEffectDefinition();
                    transform.CropRectangle = new Rect(x, y, width, height);
                    transform.OutputSize = new Size(240, 240);
                    transform.Mirror = mirroring ? MediaMirroringOptions.Horizontal : MediaMirroringOptions.None;

                    var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga);
                    profile.Video.Width = 240;
                    profile.Video.Height = 240;
                    profile.Video.Bitrate = 300000;

                    try
                    {
                        viewModel.Dispatcher.Dispatch(async () =>
                        {
                            await viewModel.SendVideoNoteAsync(file, profile, transform);
                        });
                    }
                    catch { }
                }
                else
                {
                    try
                    {
                        viewModel.Dispatcher.Dispatch(async () =>
                        {
                            await viewModel.SendVoiceNoteAsync(file, duration, null);
                        });
                    }
                    catch { }
                }
            }

            internal sealed class OpusRecorder
            {
                #region fields

                private bool m_isVideo;

                private StorageFile m_file;
                private IMediaExtension m_opusSink;
                private LowLagMediaRecording m_lowLag;
                public MediaCapture m_mediaCapture;
                public MediaCaptureInitializationSettings settings;

                // Information about the camera device
                public bool _mirroringPreview;
                public bool _externalCamera;

                // Rotation Helper to simplify handling rotation compensation for the camera streams
                public CameraRotationHelper _rotationHelper;

                #endregion

                #region properties

                public StorageFile File
                {
                    get { return m_file; }
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
                    settings.AudioProcessing = m_isVideo ? AudioProcessing.Default : AudioProcessing.Raw;
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
                        var rotationAngle = CameraRotationHelper.ConvertSimpleOrientationToClockwiseDegrees(Windows.Devices.Sensors.SimpleOrientation.NotRotated); // _rotationHelper.GetCameraCaptureOrientation());
                        profile.Video.Properties.Add(new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1"), PropertyValue.CreateInt32(rotationAngle));

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

                        if (m_opusSink is IDisposable disposable)
                        {
                            disposable.Dispose();
                            m_opusSink = null;
                        }
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

                        if (m_opusSink is IDisposable disposable)
                        {
                            disposable.Dispose();
                            m_opusSink = null;
                        }
                    }
                    catch { }
                }

                #endregion

                public async Task SetPreviewRotationAsync()
                {
                    // Only need to update the orientation if the camera is mounted on the device
                    if (_externalCamera || _rotationHelper == null || m_mediaCapture == null) return;

                    // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
                    var rotation = _rotationHelper.GetCameraPreviewOrientation();
                    var props = m_mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                    props.Properties.Add(new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1"), CameraRotationHelper.ConvertSimpleOrientationToClockwiseDegrees(rotation));
                    await m_mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
                }
            }
        }
    }
}
