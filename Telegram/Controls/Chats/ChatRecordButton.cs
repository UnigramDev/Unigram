//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Common.Recording;
using Telegram.Td;
using Telegram.ViewModels;
using Windows.Media.Capture;
using Windows.System.Display;
using Windows.System.Profile;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Chats
{
    /// <summary>
    /// The press-and-hold gesture, and nothing else. What is being recorded, and how far along it
    /// is, belongs to <see cref="ChatRecordSession"/>.
    /// </summary>
    public partial class ChatRecordButton : AnimatedIconToggleButton
    {
        public ComposeViewModel ViewModel => DataContext as ComposeViewModel;

        private AnimatedIcon Icon;
        private Visual _icon;

        private readonly DispatcherTimer _timer;

        // One session per button, and one button per chat. The engine underneath used to be a
        // thread-static singleton, so a recording started in one chat drove the state of every
        // other chat and story composer loaded on the same thread, and whichever loaded last owned
        // the level meter.
        private readonly ChatRecordSession _session = new();

        public ChatRecordSession Session => _session;

        public TimeSpan Elapsed => _session.Elapsed;

        public bool IsRecording => _session.IsRecording;

        public bool IsLocked => _session.IsLocked;

        public bool IsViewOnce
        {
            get => _session.IsViewOnce;
            set => _session.IsViewOnce = value;
        }

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

        public byte[] GetWaveform() => _session.GetWaveform();

        public ChatRecordButton()
        {
            DefaultStyleKey = typeof(ChatRecordButton);

            Mode = ChatRecordMode.Voice;

            ClickMode = ClickMode.Press;
            Click += OnClick;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(300);
            _timer.Tick += OnTimerTick;

            // Nothing to detach: the session is owned by this button and cannot outlive it.
            _session.RecordingStarting += OnRecordingStarting;
            _session.RecordingStarted += OnRecordingStarted;
            _session.RecordingStopped += OnRecordingStopped;
            _session.RecordingLocked += OnRecordingLocked;
            _session.RecordingTooShort += OnRecordingTooShort;
            _session.DurationLimitReached += OnDurationLimitReached;
            _session.QuantumProcessed += OnQuantumProcessed;
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

        private void OnTimerTick(object sender, object e)
        {
            Logger.Debug("Timer Tick, check for permissions");

            _timer.Stop();
            StartRecording();
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

        private async void StartRecording()
        {
            _calledRecordRunnable = true;
            _recordAudioVideoRunnableStarted = false;

            // Inherited from the permission check this replaced, which found consent status
            // always Unspecified on Xbox and gave up asking. Unverified against AppCapability,
            // and cheaper to keep than to be wrong about.
            if (!string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Xbox"))
            {
                var requested = Mode == ChatRecordMode.Video
                    ? MediaDeviceAccess.AudioAndVideo
                    : MediaDeviceAccess.Audio;

                var permissions = await MediaDevicePermissions.CheckAccessAsync(XamlRoot, requested, ElementTheme.Default, MediaDevicePurpose.Record);
                if (permissions == false)
                {
                    return;
                }
            }

            Logger.Debug("Permissions granted, mode: " + Mode);

            _session.Start(Mode, ViewModel.Chat);
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

        private DisplayRequest _request;

        private void OnRecordingStarting(object sender, EventArgs e)
        {
            UpdateVisualState();

            // Release, so that letting go of the pointer raises Click and ends the recording.
            ClickMode = ClickMode.Release;

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

            RecordingStarting?.Invoke(this, e);
        }

        private void OnRecordingLocked(object sender, EventArgs e)
        {
            UpdateVisualState();

            ClickMode = ClickMode.Press;
            RecordingLocked?.Invoke(this, e);
        }

        private void OnRecordingStopped(object sender, EventArgs e)
        {
            UpdateVisualState();

            ClickMode = ClickMode.Press;
            Automation.SetToolTip(this, Mode == ChatRecordMode.Video ? Strings.AccDescrVideoMessage : Strings.AccDescrVoiceMessage);

            _request?.TryRequestRelease();
            _request = null;

            RecordingStopped?.Invoke(this, e);
        }

        private void OnRecordingStarted(object sender, MediaCapture e)
        {
            RecordingStarted?.Invoke(this, e);
        }

        private void OnQuantumProcessed(object sender, float e)
        {
            QuantumProcessed?.Invoke(this, e);
        }

        private void OnDurationLimitReached(object sender, EventArgs e)
        {
            // Sends whether or not the pointer is still down. A second Complete from the release
            // that follows finds nothing left to stop.
            _session.Complete(ViewModel);
        }

        private void OnRecordingTooShort(object sender, EventArgs e)
        {
            // Same hint the mode-switch tap shows: releasing before there is anything to send is a
            // tap that happened to last a little longer.
            ShowHoldHint();
        }

        private void ShowHoldHint()
        {
            var message = Mode == ChatRecordMode.Video
                ? Strings.HoldToVideo
                : Strings.HoldToAudio;

            ToastPopup.Show(this, message, TeachingTipPlacementMode.TopLeft, dismissAfter: TimeSpan.FromSeconds(3));
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

                if (_session.IsLocked)
                {
                    Complete();
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
                    StartRecording();
                }
            }
            else
            {
                Logger.Debug("Click mode: Release");
                OnRelease();
            }
        }

        /// <summary>
        /// Stops a locked recording and sends it.
        /// </summary>
        public void Complete()
        {
            if (_session.IsLocked && (!_hasRecordVideo || _calledRecordRunnable))
            {
                Logger.Debug("Completing a locked recording");
                _session.Complete(ViewModel);
            }
        }

        /// <summary>
        /// Stops a recording and throws it away.
        /// </summary>
        public void Cancel()
        {
            _session.Cancel();
        }

        public void Lock()
        {
            Logger.Debug("Locking recording");
            _session.Lock();
        }

        public Task<ChatRecordResult> TogglePauseAsync()
        {
            Logger.Debug("Pause recording");
            return _session.TogglePauseAsync();
        }

        private void OnRelease()
        {
            ClickMode = ClickMode.Press;

            if (_session.IsLocked)
            {
                Logger.Debug("Recording is locked, abort");
                return;
            }
            if (_recordAudioVideoRunnableStarted && _timer.IsEnabled)
            {
                Logger.Debug("Timer should still tick, change mode to: " + (Mode == ChatRecordMode.Video ? ChatRecordMode.Voice : ChatRecordMode.Video));

                _timer.Stop();
                Mode = Mode == ChatRecordMode.Video ? ChatRecordMode.Voice : ChatRecordMode.Video;

                ShowHoldHint();
            }
            else if (!_hasRecordVideo || _calledRecordRunnable)
            {
                Logger.Debug("Timer has tick, stopping recording");
                _session.Complete(ViewModel);
            }

            UpdateVisualState();
        }

        public async void ToggleRecording()
        {
            if (_session.IsLocked)
            {
                Complete();
            }
            else
            {
                var restricted = await ViewModel.VerifyRightsAsync(x => Mode == ChatRecordMode.Video ? x.CanSendVideoNotes : x.CanSendVoiceNotes, Strings.GlobalAttachMediaRestricted, Strings.AttachMediaRestrictedForever, Strings.AttachMediaRestricted);
                if (restricted)
                {
                    return;
                }

                // Nothing is holding a pointer down, so it locks the moment it begins.
                _session.RequestLock();
                StartRecording();
            }
        }

        // Whether tapping switches between voice and video, which is what the 300ms timer is for.
        private readonly bool _hasRecordVideo = true;

        private bool _pointerEntered;
        private bool _pointerReleased;

        private bool _calledRecordRunnable;
        private bool _recordAudioVideoRunnableStarted;

        public event EventHandler RecordingStarting;
        public event EventHandler<MediaCapture> RecordingStarted;
        public event EventHandler RecordingStopped;
        public event EventHandler RecordingLocked;

        public event EventHandler<float> QuantumProcessed;
    }
}
