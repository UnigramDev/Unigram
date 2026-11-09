//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Native.Calls;
using Telegram.Navigation.Services;
using Telegram.Services.Calls;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public sealed partial class GroupCallActiveHeader : UserControlEx
    {
        private readonly CompositionCurveVisual _curveVisual;

        private INavigationService _navigationService;
        private VoipCallBase _call;

        private DispatcherTimer _lateTimer;

        private SolidColorBrush _connectingBrush;
        private long _connectingToken;

        public GroupCallActiveHeader()
        {
            InitializeComponent();

            // This should be used as a mask to apply a gradient to another visual
            _curveVisual = new CompositionCurveVisual(Curve, 0, 0, 1.5f);

            RegisterPropertyChangedCallback(BackgroundProperty, OnBackgroundChanged);
        }

        protected override void OnLoaded()
        {
            WatchConnectingBrush();
            StartAnimating();
            UpdateCurveColors(false);
        }

        protected override void OnUnloaded()
        {
            _connectingBrush?.UnregisterColorChangedCallback(ref _connectingToken);
            _connectingBrush = null;

            StopLateTimer();
            _curveVisual.StopAnimating();
        }

        public void Update(INavigationService navigationService, VoipCallBase value)
        {
            _navigationService = navigationService;

            if (_call is VoipCall oldPrivateCall)
            {
                oldPrivateCall.StateChanged -= OnStateChanged;
                oldPrivateCall.MediaStateChanged -= OnMediaStateChanged;
                oldPrivateCall.AudioLevelUpdated -= OnAudioLevelUpdated;
            }
            else if (_call is VoipGroupCall oldGroupCall)
            {
                oldGroupCall.Changed -= OnGroupCallChanged;
                oldGroupCall.NetworkStateChanged -= OnNetworkStateChanged;
                oldGroupCall.MutedChanged -= OnGroupCallChanged;
                oldGroupCall.AudioLevelsUpdated -= OnAudioLevelsUpdated;
            }

            StopLateTimer();

            // The colors fade between calls, but the first ones have nothing to fade from.
            var animated = _call != null;

            if (_call != value)
            {
                _curveVisual.UpdateLevel(0, immediately: true);
            }

            _call = value;

            if (_call is VoipCall newPrivateCall)
            {
                newPrivateCall.StateChanged += OnStateChanged;
                newPrivateCall.MediaStateChanged += OnMediaStateChanged;
                newPrivateCall.AudioLevelUpdated += OnAudioLevelUpdated;

                StartAnimating();
                UpdateCurveColors(animated);

                Audio.IsEnabled = true;
                Audio.Visibility = Visibility.Visible;
                //Dismiss.Visibility = Visibility.Visible;

                try
                {
                    if (newPrivateCall.ClientService.TryGetUser(newPrivateCall.UserId, out User user))
                    {
                        TitleInfo.Text = user.FullName();
                    }
                }
                catch
                {
                    // TODO: there's a race condition happening here for obvious reasons.
                    // Try-catching until the code is actually properly refactored.
                }
            }
            else if (_call is VoipGroupCall newGroupCall)
            {
                newGroupCall.Changed += OnGroupCallChanged;
                newGroupCall.NetworkStateChanged += OnNetworkStateChanged;
                newGroupCall.MutedChanged += OnGroupCallChanged;
                newGroupCall.AudioLevelsUpdated += OnAudioLevelsUpdated;

                StartAnimating();
                UpdateCurveColors(animated);

                Audio.Visibility = newGroupCall.IsRtmpStream ? Visibility.Collapsed : Visibility.Visible;
                //Dismiss.Visibility = Visibility.Visible;

                TitleInfo.Text = newGroupCall.GetTitle();
                UpdateAudio(newGroupCall);
            }
            else
            {
                _curveVisual.StopAnimating();
                _curveVisual.Clear();
            }
        }

        private void UpdateCurveColors(bool animated)
        {
            var connected = false;
            var muted = true;
            var cantSpeak = false;
            var late = false;

            if (_call is VoipCall privateCall)
            {
                connected = privateCall.IsConnected;
                muted = privateCall.AudioState == VoipAudioState.Muted;
            }
            else if (_call is VoipGroupCall groupCall)
            {
                var scheduled = groupCall.ScheduledStartDate > 0;
                var remaining = groupCall.ScheduledStartDate - groupCall.ClientService.UnixTime;

                connected = scheduled || groupCall.IsConnected;
                muted = groupCall.IsMuted;
                cantSpeak = CantSpeak(groupCall);
                late = scheduled && remaining < 0;

                if (scheduled && !late)
                {
                    StartLateTimer(remaining);
                }
                else
                {
                    StopLateTimer();
                }
            }
            else
            {
                return;
            }

            if (!connected)
            {
                if (Background is SolidColorBrush connecting)
                {
                    // A translucent badge color would read too thin across the whole bar, so its
                    // alpha a is raised to a(2-a).
                    var color = connecting.Color;
                    color.A = (byte)(color.A * (2 - color.A / 255f));

                    _curveVisual.SetColorStops(color, color, animated);
                }
            }
            else if (late)
            {
                _curveVisual.SetColorStops(ColorEx.FromHex(0xFFAA56A6), ColorEx.FromHex(0xFFEF476F), animated);
            }
            else if (cantSpeak)
            {
                _curveVisual.SetColorStops(ColorEx.FromHex(0xFF3252EF), ColorEx.FromHex(0xFFEF436C), animated);
            }
            else if (muted)
            {
                _curveVisual.SetColorStops(ColorEx.FromHex(0xFF007FFF), ColorEx.FromHex(0xFF00AFFE), animated);
            }
            else
            {
                _curveVisual.SetColorStops(ColorEx.FromHex(0xFF33C659), ColorEx.FromHex(0xFF00A0B9), animated);
            }
        }

        private static bool CantSpeak(VoipGroupCall groupCall)
        {
            return groupCall.ScheduledStartDate > 0 || groupCall.CurrentUser is { IsMutedForAllUsers: true, CanUnmuteSelf: false };
        }

        private void UpdateAudio(VoipGroupCall groupCall)
        {
            Audio.IsChecked = !groupCall.IsMuted;
            Audio.IsEnabled = !CantSpeak(groupCall);
            Automation.SetToolTip(Audio, groupCall.IsMuted ? Strings.VoipGroupUnmute : Strings.VoipGroupMute);
        }

        private void StartLateTimer(double remaining)
        {
            if (_lateTimer == null)
            {
                _lateTimer = new DispatcherTimer();
                _lateTimer.Tick += OnLateTimerTick;
            }

            // The tick arms it again, so a date far off needs no interval that long.
            _lateTimer.Interval = TimeSpan.FromSeconds(Math.Min(remaining + 1, 3600));
            _lateTimer.Start();
        }

        private void StopLateTimer()
        {
            if (_lateTimer != null)
            {
                _lateTimer.Stop();
                _lateTimer.Tick -= OnLateTimerTick;
                _lateTimer = null;
            }
        }

        private void OnLateTimerTick(object sender, object e)
        {
            UpdateCurveColors(true);
        }

        // A UserControl does not paint its Background: it is only how the theme's connecting color
        // gets here, and how a light/dark switch is noticed.
        private void OnBackgroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (IsConnected)
            {
                WatchConnectingBrush();
            }

            UpdateCurveColors(true);
        }

        // Applying a theme recolors the brush in place, which Background does not see change.
        private void WatchConnectingBrush()
        {
            _connectingBrush?.UnregisterColorChangedCallback(ref _connectingToken);
            _connectingBrush = Background as SolidColorBrush;
            _connectingBrush?.RegisterColorChangedCallback(OnConnectingColorChanged, ref _connectingToken);
        }

        private void OnConnectingColorChanged(DependencyObject sender, DependencyProperty dp)
        {
            UpdateCurveColors(true);
        }

        private void OnStateChanged(VoipCall sender, VoipCallStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateCurveColors(true));
        }

        private void OnNetworkStateChanged(VoipGroupCall sender, VoipGroupCallNetworkStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateCurveColors(true));
        }

        // Also raised when an admin mutes the current user, which sets the manager directly and
        // has no MutedChanged of its own.
        private void OnGroupCallChanged(object sender, EventArgs e)
        {
            this.BeginOnUIThread(() =>
            {
                if (_call is VoipGroupCall groupCall)
                {
                    UpdateCurveColors(true);
                    UpdateAudio(groupCall);
                }
            });
        }

        private void OnMediaStateChanged(VoipCall sender, VoipCallMediaStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateCurveColors(true));
        }

        private void OnAudioLevelsUpdated(VoipGroupCall sender, IList<VoipGroupParticipant> args)
        {
            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                var loudest = 0f;

                foreach (var level in args)
                {
                    if (level.IsMuted)
                    {
                        continue;
                    }

                    // The 1.5 cancels the visual's maxLevel, so your own voice can use the waves' full range.
                    loudest = MathF.Max(loudest, level.AudioSource == 0 ? level.Level * 1.5f : level.Level);
                }

                _curveVisual.UpdateLevel(loudest);
            }
        }

        private void OnAudioLevelUpdated(VoipCall sender, VoipCallAudioLevelUpdatedEventArgs args)
        {
            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _curveVisual.UpdateLevel(args.AudioLevel);
            }
        }


        private void Audio_Click(object sender, RoutedEventArgs e)
        {
            if (_call is VoipCall privateCall)
            {
                privateCall.AudioState = privateCall.AudioState == VoipAudioState.Muted
                    ? VoipAudioState.Active
                    : VoipAudioState.Muted;
            }
            else if (_call is VoipGroupCall groupCall)
            {
                groupCall.IsMuted = !groupCall.IsMuted;
            }
        }

        private void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            _call?.Discard();
        }

        private void Title_Click(object sender, RoutedEventArgs e)
        {
            _call?.Show(_navigationService);
        }

        private void Curve_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _curveVisual.ActualSize = e.NewSize.ToVector2();
            StartAnimating();
        }

        private void StartAnimating()
        {
            if (_call == null || IsDisconnected || _curveVisual.ActualSize.X == 0 || _curveVisual.ActualSize.Y == 0)
            {
                return;
            }

            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _curveVisual.StartAnimating();
            }
            else
            {
                _curveVisual.ShowSolid();
            }
        }
    }
}
