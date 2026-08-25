//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.ViewModels;
using Windows.Media.Capture;

namespace Telegram.Common.Recording
{
    public enum ChatRecordState
    {
        Idle,
        Recording,

        /// <summary>
        /// Recording, and no longer held by the pointer.
        /// </summary>
        Locked
    }

    /// <summary>
    /// Owns a recording from the moment it is asked for to the moment it is sent or thrown away:
    /// the state, the clock, and the engine underneath. One per chat.
    /// </summary>
    /// <remarks>
    /// This is deliberately not a control. What is left in <c>ChatRecordButton</c> is input, and
    /// what is left in <c>ChatRecordBar</c> is animation.
    /// </remarks>
    public partial class ChatRecordSession
    {
        /// <summary>
        /// How long a video message is allowed to be.
        /// </summary>
        public static readonly TimeSpan MaximumVideoDuration = TimeSpan.FromSeconds(60);

        private readonly ChatRecordEngine _engine = new();
        private readonly DispatcherQueue _dispatcherQueue;

        private Windows.System.DispatcherQueueTimer _limitTimer;

        // The engine reports from its own queue, so these are written off the UI thread and read
        // on it, exactly as they were when they lived in the button.
        private bool _recording;
        private bool _locked;
        private bool _paused;

        // Set when a recording is asked to lock before it has actually begun: Ctrl+R does not wait
        // for the microphone.
        private bool _lockRequested;

        private ComposeViewModel _viewModel;

        // .NET Native has no Environment.TickCount64.
        private ulong _resumedAt;
        private TimeSpan _accumulated;

        public ChatRecordSession()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _engine.RecordingStarting += OnEngineStarting;
            _engine.RecordingStarted += OnEngineStarted;
            _engine.RecordingStopped += OnEngineStopped;
            _engine.RecordingFailed += OnEngineStopped;
            _engine.RecordingTooShort += OnEngineTooShort;

            _engine.QuantumProcessed = OnQuantumProcessed;
        }

        public ChatRecordState State { get; private set; }

        public ChatRecordMode Mode { get; private set; }

        public bool IsRecording => _recording;

        public bool IsLocked => _locked;

        public bool IsPaused => _paused;

        public bool IsViewOnce
        {
            get => _engine.IsViewOnce;
            set => _engine.IsViewOnce = value;
        }

        /// <summary>
        /// How long the recording has been running, from a monotonic clock and frozen while
        /// paused. Not the same number as the duration that gets sent, which is counted from the
        /// samples: this one includes the time the microphone took to open.
        /// </summary>
        public TimeSpan Elapsed => _paused
            ? _accumulated
            : _accumulated + TimeSpan.FromMilliseconds(Logger.TickCount - _resumedAt);

        public byte[] GetWaveform()
        {
            return _engine.GetWaveform();
        }

        public event EventHandler RecordingStarting;
        public event EventHandler<MediaCapture> RecordingStarted;
        public event EventHandler RecordingStopped;
        public event EventHandler RecordingLocked;
        public event EventHandler RecordingTooShort;

        /// <summary>
        /// A video message has run for as long as one is allowed to, and wants sending.
        /// </summary>
        public event EventHandler DurationLimitReached;

        public event EventHandler<float> QuantumProcessed;

        /// <summary>
        /// Begins capturing. The caller is expected to have settled permissions and chat rights
        /// already: by the time this runs the answer is yes.
        /// </summary>
        public void Start(ChatRecordMode mode, ComposeViewModel viewModel)
        {
            Mode = mode;
            _viewModel = viewModel;

            LifetimeService.Current.Playback.Pause();

            _engine.Start(mode, viewModel.Chat, (int)viewModel.ClientService.Options.SuggestedVideoNoteLength);
            Update();
        }

        /// <summary>
        /// Locks as soon as the recording begins, for the paths that don't hold a pointer down.
        /// </summary>
        public void RequestLock()
        {
            _lockRequested = true;
        }

        public void Lock()
        {
            _lockRequested = false;
            _locked = true;

            Update();
        }

        public void Complete()
        {
            _engine.Complete(_viewModel);

            _recording = false;
            Update();
        }

        public void Cancel()
        {
            _engine.Cancel();

            _recording = false;
            Update();
        }

        /// <summary>
        /// Pauses or resumes. Returns the recording so far when it pauses, and null when it
        /// resumes.
        /// </summary>
        public async Task<ChatRecordResult> TogglePauseAsync()
        {
            // Flipped before awaiting so the clock stops at the moment of the click rather than
            // when the engine gets around to it.
            _paused = !_paused;

            if (_paused)
            {
                _accumulated = Elapsed;
            }
            else
            {
                _resumedAt = Logger.TickCount;
            }

            var result = await _engine.PauseAsync();
            if (result != null)
            {
                // The engine knows how much audio it actually holds, which is the number the
                // waveform preview is labelled with.
                _accumulated = result.Duration;
            }

            return result;
        }

        private void OnEngineStarting(object sender, EventArgs e)
        {
            if (!_recording)
            {
                _recording = true;
                Update();

                if (_lockRequested)
                {
                    Lock();
                }
            }
        }

        private void OnEngineStarted(object sender, EventArgs e)
        {
            // The device is open, which is the expensive part of starting. Restarting the clock
            // here keeps the label from counting time no microphone was listening for — the bar
            // is already on screen by now, it just wasn't ticking against anything real.
            _accumulated = TimeSpan.Zero;
            _resumedAt = Logger.TickCount;

            // Read here rather than in the callback: by the time that runs the recording may
            // already have been torn down.
            var mediaCapture = _engine.MediaSource;

            if (_recording && mediaCapture != null)
            {
                _dispatcherQueue.TryEnqueue(() => RecordingStarted?.Invoke(this, mediaCapture));
            }
        }

        private void OnEngineStopped(object sender, EventArgs e)
        {
            if (_recording)
            {
                _recording = false;
                Update();
            }
        }

        private void OnEngineTooShort(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => RecordingTooShort?.Invoke(this, EventArgs.Empty));
        }

        private void OnQuantumProcessed(float amplitude)
        {
            // Raised on the capture thread, ~40 times a second. Marshalling would allocate a
            // closure for each one, and there is nothing to marshal for: the only listener hands
            // the level to CompositionBlobVisual, which stores it and lets its own vsync tick pick
            // it up on the UI thread.
            QuantumProcessed?.Invoke(this, amplitude);
        }

        /// <summary>
        /// The one transition. Everything that used to be spread across a chain of booleans and an
        /// integer interface state resolves here.
        /// </summary>
        private void Update()
        {
            var state = (_recording, _locked) switch
            {
                (true, true) => ChatRecordState.Locked,
                (true, false) => ChatRecordState.Recording,
                _ => ChatRecordState.Idle
            };

            if (state == State)
            {
                return;
            }

            State = state;

            switch (state)
            {
                case ChatRecordState.Recording:
                    _paused = false;

                    _accumulated = TimeSpan.Zero;
                    _resumedAt = Logger.TickCount;

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        StartLimitTimer();
                        RecordingStarting?.Invoke(this, EventArgs.Empty);
                    });
                    break;

                case ChatRecordState.Locked:
                    _dispatcherQueue.TryEnqueue(() => RecordingLocked?.Invoke(this, EventArgs.Empty));
                    break;

                case ChatRecordState.Idle:
                    _locked = false;
                    _paused = false;
                    _lockRequested = false;

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        _limitTimer?.Stop();
                        RecordingStopped?.Invoke(this, EventArgs.Empty);
                    });
                    break;
            }
        }

        private void StartLimitTimer()
        {
            if (Mode != ChatRecordMode.Video)
            {
                return;
            }

            if (_limitTimer == null)
            {
                _limitTimer = _dispatcherQueue.CreateTimer();
                _limitTimer.Interval = TimeSpan.FromMilliseconds(250);
                _limitTimer.Tick += OnLimitTick;
            }

            _limitTimer.Start();
        }

        private void OnLimitTick(Windows.System.DispatcherQueueTimer sender, object args)
        {
            // Elapsed rather than a deadline, so that pausing pauses the limit too.
            if (Elapsed < MaximumVideoDuration)
            {
                return;
            }

            sender.Stop();
            DurationLimitReached?.Invoke(this, EventArgs.Empty);
        }
    }
}
