//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Common.Recording;
using Telegram.Entities;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.System;
using Windows.System.Display;
using Windows.System.Profile;
using Windows.UI.Composition;
using Windows.UI.Xaml;
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

    public partial class ChatRecordResult
    {
        public ChatRecordResult(TimeSpan duration, IList<byte> waveform)
        {
            Duration = duration;
            Waveform = waveform;
        }

        public TimeSpan Duration { get; }

        public IList<byte> Waveform { get; }
    }

    public partial class ChatRecordStartedEventArgs : EventArgs
    {
        public ChatRecordStartedEventArgs(DateTime startedAt)
        {
            StartedAt = startedAt;
        }

        public DateTime StartedAt { get; }
    }

    public partial class ChatRecordButton : AnimatedIconToggleButton
    {
        public ComposeViewModel ViewModel => DataContext as ComposeViewModel;

        private AnimatedIcon Icon;
        private Visual _icon;

        private readonly DispatcherTimer _timer;
        private readonly Recorder _recorder;

        private DateTime _start;
        private TimeSpan _duration;

        public TimeSpan Elapsed => DateTime.Now - _start + _duration;

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
                Automation.SetToolTip(this, value == ChatRecordMode.Video ? Strings.AccDescrVideoMessage : Strings.AccDescrVoiceMessage);
            }
        }

        public byte[] GetWaveform() => _recorder.GetWaveform();

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
                Logger.Debug("Timer Tick, check for permissions");

                _timer.Stop();
                RecordAudioVideoRunnable();
            };

            _recorder = Recorder.Current;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override bool IsRuntimeCompatible()
        {
            return Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 11);
        }

        protected override void OnApplyTemplate()
        {
            Icon = GetTemplateChild(nameof(Icon)) as AnimatedIcon;
            Icon.PointerReleased += OnPointerReleased;
            Icon.PointerCanceled += OnPointerCanceled;
            Icon.PointerCaptureLost += OnPointerCaptureLost;

            _icon = ElementComposition.GetElementVisual(Icon);
            _icon.Opacity = IsRestricted ? 0.2f : 1;

            base.OnApplyTemplate();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _recorder.RecordingStarting += OnRecordingStarting;
            _recorder.RecordingStarted += OnRecordingStarted;
            _recorder.RecordingStopped += OnRecordingStopped;
            _recorder.RecordingFailed += OnRecordingStopped;

            _recorder.QuantumProcessed = amplitude => QuantumProcessed?.Invoke(this, amplitude);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _recorder.RecordingStarting -= OnRecordingStarting;
            _recorder.RecordingStarted -= OnRecordingStarted;
            _recorder.RecordingStopped -= OnRecordingStopped;
            _recorder.RecordingFailed -= OnRecordingStopped;

            _recorder.QuantumProcessed = null;
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            _pointerEntered = true;

            try
            {
                base.OnPointerEntered(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            _pointerEntered = false;

            try
            {
                base.OnPointerExited(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            Icon.CapturePointer(e.Pointer);

            try
            {
                base.OnPointerPressed(e);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _pointerReleased = true;
            Logger.Debug("OnPointerReleased");

            Icon.ReleasePointerCapture(e.Pointer);
            OnRelease();

            _pointerReleased = false;
        }

        private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            _pointerReleased = true;
            Logger.Debug("OnPointerCanceled");

            Icon.ReleasePointerCapture(e.Pointer);
            OnRelease();

            _pointerReleased = false;
        }

        private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (_pointerReleased)
            {
                return;
            }

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

            LifetimeService.Current.Playback.Pause();

            Logger.Debug("Permissions granted, mode: " + Mode);

            _recorder.Start(Mode, ViewModel.Chat);
            UpdateRecordingInterface();
        }

        private void OnRecordingStarting(object sender, EventArgs e)
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

        private void OnRecordingStarted(object sender, EventArgs e)
        {
            if (_recordingAudioVideo)
            {
                this.BeginOnUIThread(() => RecordingStarted?.Invoke(_recorder.MediaSource, EventArgs.Empty));
            }
        }

        private void OnRecordingStopped(object sender, EventArgs e)
        {
            if (_recordingAudioVideo)
            {
                // cancel typing
                _recordingAudioVideo = false;
                UpdateRecordingInterface();
            }
        }

        private void UpdateVisualState()
        {
            if (_pointerEntered)
            {
                VisualStateManager.GoToState(this, Mode == ChatRecordMode.Voice ? "PointerOver" : "CheckedPointerOver", false);
            }
            else
            {
                VisualStateManager.GoToState(this, Mode == ChatRecordMode.Voice ? "Normal" : "Checked", false);
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
                    UpdateVisualState();

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
                    UpdateVisualState();

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
                _duration = TimeSpan.Zero;

                this.BeginOnUIThread(() =>
                {
                    UpdateVisualState();

                    ClickMode = ClickMode.Release;
                    RecordingStarting?.Invoke(this, EventArgs.Empty);

                    Automation.SetToolTip(this, null);

                    try
                    {
                        if (_request == null)
                        {
                            _request = new DisplayRequest();
                            _request.TryRequestActive();
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
                    UpdateVisualState();

                    ClickMode = ClickMode.Press;
                    RecordingStopped?.Invoke(this, EventArgs.Empty);

                    Automation.SetToolTip(this, Mode == ChatRecordMode.Video ? Strings.AccDescrVideoMessage : Strings.AccDescrVoiceMessage);

                    _request?.TryRequestRelease();
                    _request = null;
                });
            }

            Logger.Debug("Updated interface, state: " + recordInterfaceState);
        }

        private async void OnClick(object sender, RoutedEventArgs e)
        {
            if (ClickMode == ClickMode.Press)
            {
                if (MediaDevicePermissions.IsUnsupported(XamlRoot))
                {
                    return;
                }
                else if (IsRestricted)
                {
                    var message = Mode == ChatRecordMode.Video
                        ? Strings.VideoMessagesRestrictedByPrivacy
                        : Strings.VoiceMessagesRestrictedByPrivacy;

                    var formatted = string.Format(message, ViewModel.Chat.Title);
                    var markdown = ClientEx.ParseMarkdown(formatted);
                    ToastPopup.Show(this, markdown, TeachingTipPlacementMode.TopLeft, dismissAfter: TimeSpan.FromSeconds(3));
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

                ToastPopup.Show(this, message, TeachingTipPlacementMode.TopLeft, dismissAfter: TimeSpan.FromSeconds(3));
            }
            else if (!_hasRecordVideo || _calledRecordRunnable)
            {
                Logger.Debug("Timer has tick, stopping recording");

                _recorder.Stop(ViewModel, false);
                _recordingAudioVideo = false;
                UpdateRecordingInterface();
            }

            UpdateVisualState();
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
                    capture?.Dispose();
                    capture = null;
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
                    var confirm = await MessagePopup.ShowAsync(XamlRoot, message, Strings.AppName, Strings.PermissionOpenSettings, Strings.OK);
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

        private bool _pointerEntered;
        private bool _pointerReleased;

        private bool _calledRecordRunnable;
        private bool _recordAudioVideoRunnableStarted;

        private bool _recordingAudioVideo;

        private bool _recordingPaused;
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

        public async Task<ChatRecordResult> PauseRecording()
        {
            Logger.Debug("Pause recording");

            if (_recordingPaused)
            {
                _start = DateTime.Now;
                _recordingPaused = false;
            }
            else
            {
                _duration = Elapsed;
                _recordingPaused = true;
            }

            UpdateRecordingInterface();

            var result = await _recorder.PauseAsync();
            if (result != null)
            {
                _start = DateTime.Now;
                _duration = result.Duration;
            }

            return result;
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

        public event EventHandler RecordingStarting;
        public event EventHandler RecordingStarted;
        public event EventHandler RecordingStopped;
        public event EventHandler RecordingLocked;

        public event EventHandler<float> QuantumProcessed;

        public bool IsViewOnce
        {
            get => _recorder.IsViewOnce;
            set => _recorder.IsViewOnce = value;
        }

        public partial class Recorder
        {
            public event EventHandler RecordingFailed;
            public event EventHandler RecordingStarting;
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

            private MediaFrameReader _reader;

            private readonly AudioWaveform _waveform = new();

            // Set only when the capture format can be encoded as it arrives. Otherwise the
            // recording goes through MediaCapture's own encoder and is transcoded when sent.
            private VoiceSink _sink;

            private float[] _samples;
            private uint _channels;
            private uint _bitsPerSample;

            private bool _paused;

            public Recorder()
            {
                _recordQueue = new ConcurrentQueueWorker(1);
                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            }

            public static void Release()
            {
                _current = null;
            }

            public bool IsViewOnce { get; set; }

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

                    RecordingStarting?.Invoke(this, EventArgs.Empty);

                    try
                    {
                        _mode = mode;
                        _chat = chat;
                        _paused = false;
                        _channels = 0;
                        _bitsPerSample = 0;
                        _waveform.Reset();

                        _recorder = new OpusRecorder(mode == ChatRecordMode.Video);

                        _recorder.m_mediaCapture = new MediaCapture();
                        _recorder.m_mediaCapture.Failed += OnFailed;

                        if (mode == ChatRecordMode.Video)
                        {
                            var cameraDevice = await _recorder.FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Front);
                            if (cameraDevice != null)
                            {
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
                        }

                        await _recorder.m_mediaCapture.InitializeAsync(_recorder.settings);
                        RecordingStarted?.Invoke(this, EventArgs.Empty);

                        Logger.Debug("Devices initialized, starting");

                        // For a voice message the reader is the recording, so it always runs. A
                        // video message animates the same blob and keeps the gate it had.
                        var reader = mode == ChatRecordMode.Voice
                            || (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths);

                        var streaming = reader && await PrepareReaderAsync(mode);

                        _file = await CreateFileAsync(mode, streaming);

                        if (streaming)
                        {
                            _sink = new VoiceSink(_file.Path);

                            if (!_sink.IsValid)
                            {
                                throw new InvalidOperationException("Opus encoder couldn't open " + _file.Path);
                            }
                        }
                        else
                        {
                            Logger.Info("Recording through MediaCapture, the file will be transcoded when sent");
                            await _recorder.StartAsync(_file);
                        }

                        // Started last, so that no sample arrives before there is something to
                        // write it to.
                        await StartReaderAsync();

                        Logger.Debug("Recording started at " + DateTime.Now);
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

                        _sink?.Dispose();
                        _sink = null;

                        _recorder?.Dispose();
                        _recorder = null;

                        _file = null;

                        RecordingFailed?.Invoke(this, EventArgs.Empty);
                    }
                });
            }

            private void OnFailed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
            {
                Logger.Debug(errorEventArgs.Message);
            }

            private static Task<StorageFile> CreateFileAsync(ChatRecordMode mode, bool streaming)
            {
                var fileName = string.Format(mode == ChatRecordMode.Video
                    ? "video_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.mp4"
                    : streaming
                    ? "voice_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.oga"
                    : "voice_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.wav", DateTime.Now);

                // Unique rather than fail: the name is only second-accurate, and two recordings
                // within the same second is a tap away.
                return ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName).AsTask();
            }

            /// <summary>
            /// Creates the audio frame reader, and reports whether its samples can be handed to
            /// the encoder as they are.
            /// </summary>
            private async Task<bool> PrepareReaderAsync(ChatRecordMode mode)
            {
                try
                {
                    var frameSource = _recorder.m_mediaCapture.FrameSources.FirstOrDefault(x => x.Value.Info.MediaStreamType == MediaStreamType.Audio);
                    if (frameSource.Value == null)
                    {
                        Logger.Info("No audio frame source was found.");
                        return false;
                    }

                    var source = frameSource.Value;
                    var format = source.CurrentFormat;

                    // The encoder is 48kHz: at any other rate the samples would play back at the
                    // wrong speed, so ask for one and give up on streaming if there isn't one.
                    //
                    // Only ever for a voice message. A video message records through MediaCapture,
                    // and renegotiating the format of the source it is about to record from would
                    // change what lands in the mp4 for the sake of a blob.
                    if (mode == ChatRecordMode.Voice && format.AudioEncodingProperties?.SampleRate != VoiceSink.SampleRate)
                    {
                        var match = source.SupportedFormats.FirstOrDefault(x => x.AudioEncodingProperties?.SampleRate == VoiceSink.SampleRate && IsSupportedSubtype(x.Subtype));
                        if (match != null)
                        {
                            await source.SetFormatAsync(match);
                            format = source.CurrentFormat;
                        }
                    }

                    var reader = await _recorder.m_mediaCapture.CreateFrameReaderAsync(source);

                    // Buffered rather than Realtime: a dropped frame used to cost a blob update,
                    // it now costs a gap in the recording.
                    reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Buffered;
                    reader.FrameArrived += OnAudioFrameArrived;

                    _reader = reader;

                    var properties = format.AudioEncodingProperties;
                    if (properties == null)
                    {
                        return false;
                    }

                    _channels = properties.ChannelCount;
                    _bitsPerSample = properties.BitsPerSample;

                    return mode == ChatRecordMode.Voice
                        && properties.SampleRate == VoiceSink.SampleRate
                        && IsSupportedSubtype(format.Subtype)
                        && (_bitsPerSample == 32 || _bitsPerSample == 16)
                        && _channels > 0;
                }
                catch (Exception ex)
                {
                    Logger.Info("The audio frame reader couldn't be created: " + ex);
                    return false;
                }
            }

            private async Task StartReaderAsync()
            {
                if (_reader == null)
                {
                    return;
                }

                try
                {
                    var status = await _reader.StartAsync();
                    if (status != MediaFrameReaderStartStatus.Success)
                    {
                        Logger.Info("The MediaFrameReader couldn't start.");
                    }
                }
                catch
                {
                    // A task was canceled.
                }
            }

            private static bool IsSupportedSubtype(string subtype)
            {
                // 32-bit float and 16-bit PCM are the two the sink knows how to read.
                return string.Equals(subtype, MediaEncodingSubtypes.Float, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(subtype, MediaEncodingSubtypes.Pcm, StringComparison.OrdinalIgnoreCase);
            }

            private unsafe void OnAudioFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
            {
                using var reference = sender.TryAcquireLatestFrame();
                if (reference?.SourceKind != MediaFrameSourceKind.Audio || _paused)
                {
                    return;
                }

                // The reader can be running with a format we never managed to read, in which case
                // there is nothing to say about how to interpret its bytes.
                if (_channels == 0 || _bitsPerSample == 0)
                {
                    return;
                }

                using var frame = reference.AudioMediaFrame.GetAudioFrame();

                using var audioBuffer = frame.LockBuffer(AudioBufferAccessMode.Read);
                using var bufferReference = audioBuffer.CreateReference();

                // Get the buffer from the AudioFrame
                bufferReference.Buffer(out byte* buffer, out uint capacity);

                var samples = ReadSamples(buffer, Math.Min(audioBuffer.Length, capacity));

                // Every frame is folded in, and only the notification is rate-limited: this
                // callback is the recording now, so it can't afford to skip one.
                if (_waveform.Add(samples))
                {
                    QuantumProcessed?.Invoke(_waveform.Level);
                }

                _sink?.Write(samples);
            }

            /// <summary>
            /// Presents the captured buffer as mono 32-bit float, which is what both the waveform
            /// and the encoder read.
            /// </summary>
            private unsafe ReadOnlySpan<float> ReadSamples(byte* buffer, uint length)
            {
                var channels = (int)_channels;
                var count = (int)(length / (_bitsPerSample / 8 * _channels));

                // Mono float is the common case and needs no conversion at all.
                if (_bitsPerSample == 32 && channels == 1)
                {
                    return new ReadOnlySpan<float>(buffer, count);
                }

                if (_samples == null || _samples.Length < count)
                {
                    _samples = new float[count];
                }

                var target = _samples.AsSpan(0, count);

                if (_bitsPerSample == 32)
                {
                    var source = (float*)buffer;

                    for (int i = 0; i < count; i++)
                    {
                        var sum = 0f;
                        for (int j = 0; j < channels; j++)
                        {
                            sum += source[i * channels + j];
                        }

                        target[i] = sum / channels;
                    }
                }
                else
                {
                    var source = (short*)buffer;

                    for (int i = 0; i < count; i++)
                    {
                        var sum = 0f;
                        for (int j = 0; j < channels; j++)
                        {
                            sum += source[i * channels + j] / 32768f;
                        }

                        target[i] = sum / channels;
                    }
                }

                return target;
            }

            public byte[] GetWaveform()
            {
                return _waveform.GetWaveform();
            }

            public async Task<ChatRecordResult> PauseAsync()
            {
                Logger.Debug("Pause invoked");

                var tsc = new TaskCompletionSource<ChatRecordResult>();

                _ = _recordQueue.Enqueue(async () =>
                {
                    Logger.Debug("Enqueued pause invoked");

                    var recorder = _recorder;
                    if (recorder == null)
                    {
                        Logger.Debug("recorder or file == null, abort");

                        // Setting it is what releases the caller: it awaits this task.
                        tsc.SetResult(null);
                        return;
                    }

                    if (_sink != null)
                    {
                        // Nothing to pause: the frames keep arriving and are dropped, which keeps
                        // the device warm and resuming instant.
                        _paused = !_paused;

                        tsc.SetResult(_paused
                            ? new ChatRecordResult(_sink.Duration, GetWaveform())
                            : null);
                        return;
                    }

                    var paused = await recorder.PauseAsync();
                    if (paused != null)
                    {
                        tsc.SetResult(new ChatRecordResult(paused.RecordDuration, GetWaveform()));

                        if (_reader != null)
                        {
                            try
                            {
                                await _reader.StopAsync();
                            }
                            catch
                            {
                                // A task was canceled.
                            }
                        }
                    }
                    else
                    {
                        tsc.SetResult(null);

                        if (_reader != null)
                        {
                            try
                            {
                                await _reader.StartAsync();
                            }
                            catch
                            {
                                // A task was canceled.
                            }
                        }
                    }
                });

                return await tsc.Task;
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

                    if (recorder.m_mediaCapture != null)
                    {
                        recorder.m_mediaCapture.Failed -= OnFailed;
                    }

                    RecordingStopped?.Invoke(this, EventArgs.Empty);

                    Logger.Debug("stopping reader");

                    if (reader != null)
                    {
                        try
                        {
                            await reader.StopAsync();
                        }
                        catch
                        {
                            // A task was canceled.
                        }

                        reader.FrameArrived -= OnAudioFrameArrived;
                        reader.Dispose();

                        QuantumProcessed?.Invoke(0);
                    }

                    var sink = _sink;
                    var waveform = Array.Empty<byte>();

                    TimeSpan duration;

                    if (sink != null)
                    {
                        sink.Complete();

                        // Counted from the samples that were actually encoded, so it matches the
                        // file rather than the moment the user pressed the button.
                        duration = sink.Duration;
                        waveform = GetWaveform();

                        sink.Dispose();

                        await recorder.StopAsync();
                    }
                    else
                    {
                        var result = await recorder.StopAsync();
                        duration = result?.RecordDuration ?? TimeSpan.Zero;

                        if (mode == ChatRecordMode.Voice)
                        {
                            waveform = GetWaveform();
                        }
                    }

                    Logger.Debug("recorder stopped, duration: " + duration);

                    if (cancel == true || duration.TotalMilliseconds < 700)
                    {
                        try
                        {
                            await file.DeleteAsync();
                        }
                        catch { }

                        Logger.Debug("recording canceled or too short, abort");

                        if (duration.TotalMilliseconds < 700)
                        {
                            RecordingTooShort?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    else
                    {
                        Logger.Debug("sending recorded file");

                        if (cancel == false)
                        {
                            Send(viewModel, mode, chat, file, recorder._mirroringPreview, duration, waveform, sink != null);
                        }
                    }

                    _recorder = null;
                    _file = null;

                    _reader = null;
                    _sink = null;
                });
            }

            private async void Send(ComposeViewModel viewModel, ChatRecordMode mode, Chat chat, StorageFile file, bool mirroring, TimeSpan duration, byte[] waveform, bool encoded)
            {
                var selfDestructType = IsViewOnce
                        ? new MessageSelfDestructTypeImmediately()
                        : null;

                IsViewOnce = false;

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

                    var video = await StorageMedia.CreateAsync(file);
                    var generation = new VideoGeneration
                    {
                        Transcode = true,
                        Transform = true,
                        CropRectangle = new Rect(x, y, width, height),
                        OutputSize = new Size(length, length),
                        Flip = mirroring ? ImageFlip.Horizontal : ImageFlip.None,
                        Width = (uint)length,
                        Height = (uint)length,
                        VideoBitrate = (uint)videoBitrate * 1000,
                        AudioBitrate = (uint)audioBitrate * 1000
                    };

                    try
                    {
                        _dispatcherQueue.TryEnqueue(() => _ = viewModel.SendVideoNoteAsync(video as StorageVideo, generation, selfDestructType));
                    }
                    catch { }
                }
                else
                {
                    // Already Ogg/Opus when the sink wrote it, so there is nothing to convert.
                    var conversion = encoded
                        ? ConversionType.Copy
                        : ConversionType.Opus;

                    try
                    {
                        _dispatcherQueue.TryEnqueue(() => _ = viewModel.SendVoiceNoteAsync(file, conversion, duration, waveform, null, selfDestructType));
                    }
                    catch { }
                }
            }

            public MediaCapture MediaSource => _recorder.m_mediaCapture;

            internal sealed class OpusRecorder
            {
                private readonly bool m_isVideo;

                private LowLagMediaRecording m_lowLag;
                private bool m_paused;

                public MediaCapture m_mediaCapture;
                public MediaCaptureInitializationSettings settings;

                // Information about the camera device
                public bool _mirroringPreview;
                public bool _externalCamera;

                //// Rotation Helper to simplify handling rotation compensation for the camera streams
                //public CameraRotationHelper _rotationHelper;

                public OpusRecorder(bool video)
                {
                    m_isVideo = video;
                    InitializeSettings();
                }

                private void InitializeSettings()
                {
                    // We're forcing CPU because "Auto" seems to be failing on some devices.
                    settings = new MediaCaptureInitializationSettings();
                    settings.MediaCategory = MediaCategory.Media;
                    settings.AudioProcessing = m_isVideo ? AudioProcessing.Default : SettingsService.Current.Diagnostics.ForceRawAudio ? AudioProcessing.Raw : AudioProcessing.Default;
                    settings.MemoryPreference = MediaCaptureMemoryPreference.Cpu;
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

                public async Task StartAsync(StorageFile file)
                {
                    MediaEncodingProfile profile;
                    if (m_isVideo)
                    {
                        profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                    }
                    else
                    {
                        profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
                        profile.Audio.BitsPerSample = 16;
                        profile.Audio.SampleRate = 48000;
                        profile.Audio.ChannelCount = 1;
                    }

                    m_lowLag = await m_mediaCapture.PrepareLowLagRecordToStorageFileAsync(profile, file);
                    await m_lowLag.StartAsync();
                }

                public async Task<MediaCapturePauseResult> PauseAsync()
                {
                    try
                    {
                        if (m_paused)
                        {
                            m_paused = false;
                            await m_lowLag.ResumeAsync();
                            return null;
                        }
                        else
                        {
                            m_paused = true;
                            return await m_lowLag.PauseWithResultAsync(MediaCapturePauseBehavior.RetainHardwareResources);
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }

                public async Task<MediaCaptureStopResult> StopAsync()
                {
                    MediaCaptureStopResult result = null;
                    try
                    {
                        // Null when the sink did the recording: there is only the device to close.
                        if (m_lowLag != null)
                        {
                            result = await m_lowLag.StopWithResultAsync();
                            await m_lowLag.FinishAsync();
                        }
                    }
                    catch { }
                    finally
                    {
                        m_mediaCapture?.Dispose();
                        m_mediaCapture = null;
                    }
                    return result;
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
