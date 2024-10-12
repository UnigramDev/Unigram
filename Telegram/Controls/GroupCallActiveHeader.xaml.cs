//
// Copyright Fela Ameghino 2015-2024
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
using Telegram.Services.Calls;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public sealed partial class GroupCallActiveHeader : UserControl
    {
        private readonly CompositionCurveVisual _curveVisual;

        private VoipCallBase _call;

        public GroupCallActiveHeader()
        {
            InitializeComponent();

            // This should be used as a mask to apply a gradient to another visual
            _curveVisual = new CompositionCurveVisual(Curve, 0, 0, 1.5f);
            _curveVisual.SetColorStops(0xFF59c7f8, 0xFF0078ff);
        }

        public void Update(VoipCallBase value)
        {
            if (_call is VoipCall oldPrivateCall)
            {
                oldPrivateCall.MediaStateChanged -= OnMediaStateChanged;
                oldPrivateCall.AudioLevelUpdated -= OnAudioLevelUpdated;
            }
            else if (_call is VoipGroupCall oldGroupCall)
            {
                oldGroupCall.MutedChanged -= OnMutedChanged;
                oldGroupCall.AudioLevelsUpdated -= OnAudioLevelsUpdated;
            }

            _call = value;

            if (_call is VoipCall newPrivateCall)
            {
                newPrivateCall.MediaStateChanged += OnMediaStateChanged;
                newPrivateCall.AudioLevelUpdated += OnAudioLevelUpdated;

                StartAnimating();
                UpdateCurveColors(newPrivateCall.AudioState == VoipAudioState.Muted);

                Audio.Visibility = Visibility.Visible;
                Dismiss.Visibility = Visibility.Visible;

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
                newGroupCall.MutedChanged += OnMutedChanged;
                newGroupCall.AudioLevelsUpdated += OnAudioLevelsUpdated;

                StartAnimating();
                UpdateCurveColors(newGroupCall.IsMuted);

                Audio.Visibility = Visibility.Visible;
                Dismiss.Visibility = Visibility.Visible;

                TitleInfo.Text = newGroupCall.GetTitle();
                Audio.IsChecked = !newGroupCall.IsMuted;
                Automation.SetToolTip(Audio, newGroupCall.IsMuted ? Strings.VoipGroupUnmute : Strings.VoipGroupMute);
            }
            else
            {
                _curveVisual.StopAnimating();
                _curveVisual.Clear();
            }
        }

        private void UpdateCurveColors(bool muted)
        {
            // TODO: there are multiple states to be supported: connecting, active, speaking, can't speak, late
            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                if (muted)
                {
                    _curveVisual.SetColorStops(0xFF59c7f8, 0xFF0078ff);
                }
                else
                {
                    _curveVisual.SetColorStops(0xFF0078ff, 0xFF33c659);
                }
            }
            else
            {
                if (muted)
                {
                    RootGrid.Background = ColorsHelper.LinearGradient(0xFF59c7f8, 0xFF0078ff);
                }
                else
                {
                    RootGrid.Background = ColorsHelper.LinearGradient(0xFF0078ff, 0xFF33c659);
                }
            }
        }

        private void OnMutedChanged(object sender, EventArgs e)
        {
            if (sender is VoipGroupCall groupCall && groupCall.IsMuted is bool groupMuted)
            {
                this.BeginOnUIThread(() =>
                {
                    UpdateCurveColors(groupMuted);

                    Audio.IsChecked = !groupMuted;
                    Automation.SetToolTip(Audio, groupMuted ? Strings.VoipGroupUnmute : Strings.VoipGroupMute);
                });
            }
        }

        private void OnMediaStateChanged(VoipCall sender, VoipCallMediaStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateCurveColors(args.Audio == VoipAudioState.Muted));
        }

        private void OnAudioLevelsUpdated(VoipGroupCall sender, IList<VoipGroupParticipant> args)
        {
            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                var average = 0f;

                foreach (var level in args)
                {
                    if (level.AudioSource == 0)
                    {
                        if (sender.IsMuted)
                        {
                            average = MathF.Max(average, 0);
                        }
                        else
                        {
                            average = MathF.Max(average, level.Level); // MathF.Max(average, Math.Min(8500, level.Level * 4000) / 8500);
                        }
                    }
                    else
                    {
                        average = MathF.Max(average, Math.Clamp(level.Level * 15f / 80f, 0, 1));
                    }
                }

                _curveVisual.UpdateLevel(average);
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
            _call?.Show();
        }

        private void Curve_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _curveVisual.ActualSize = e.NewSize.ToVector2();
            StartAnimating();
        }

        private void StartAnimating()
        {
            if (_curveVisual.ActualSize.X == 0 || _curveVisual.ActualSize.Y == 0)
            {
                return;
            }

            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _curveVisual.StartAnimating();
            }
            else
            {
                _curveVisual.StopAnimating();
                _curveVisual.Clear();
            }
        }
    }
}
