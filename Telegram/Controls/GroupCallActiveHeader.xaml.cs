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
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public sealed partial class GroupCallActiveHeader : UserControl
    {
        private readonly CompositionCurveVisual _curveVisual;

        private IVoipService _service;
        private IVoipGroupService _groupService;

        public GroupCallActiveHeader()
        {
            InitializeComponent();

            // This should be used as a mask to apply a gradient to another visual
            _curveVisual = new CompositionCurveVisual(Curve, 0, 0, 1.5f);
        }

        public void Update(IVoipService value)
        {
#if ENABLE_CALLS
            if (_service != null)
            {
                _service.MutedChanged -= OnMutedChanged;
                _service.AudioLevelUpdated -= OnAudioLevelUpdated;
            }

            _service = value;

            if (_service != null && _service?.Call != null)
            {
                _service.MutedChanged += OnMutedChanged;
                _service.AudioLevelUpdated += OnAudioLevelUpdated;

                if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
                {
                    _curveVisual.StartAnimating();
                }
                else
                {
                    _curveVisual.StopAnimating();
                    _curveVisual.Clear();
                }

                UpdateCurveColors(_service.IsMuted);

                Audio.Visibility = Visibility.Collapsed;
                Dismiss.Visibility = Visibility.Collapsed;

                try
                {
                    if (value.ClientService.TryGetUser(value.Call.UserId, out User user))
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
            else
            {
                _curveVisual.StopAnimating();
                _curveVisual.Clear();
            }
#endif
        }

        public void Update(IVoipGroupService value)
        {
#if ENABLE_CALLS
            if (_groupService != null)
            {
                _groupService.MutedChanged -= OnMutedChanged;
                _groupService.AudioLevelsUpdated -= OnAudioLevelsUpdated;
            }

            _groupService = value;

            if (_groupService?.Chat != null && _groupService?.Call != null)
            {
                _groupService.MutedChanged += OnMutedChanged;
                _groupService.AudioLevelsUpdated += OnAudioLevelsUpdated;

                if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
                {
                    _curveVisual.StartAnimating();
                }
                else
                {
                    _curveVisual.StopAnimating();
                    _curveVisual.Clear();
                }

                UpdateCurveColors(_groupService.IsMuted);

                Audio.Visibility = Visibility.Visible;
                Dismiss.Visibility = Visibility.Visible;

                TitleInfo.Text = _groupService.Call.Title.Length > 0 ? _groupService.Call.Title : _groupService.ClientService.GetTitle(_groupService.Chat);
                Audio.IsChecked = !_groupService.IsMuted;
                Automation.SetToolTip(Audio, _groupService.IsMuted ? Strings.VoipGroupUnmute : Strings.VoipGroupMute);
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
            _curveVisual.FillColor = muted
                ? Color.FromArgb(0xFF, 0x00, 0x78, 0xff)
                : Color.FromArgb(0xFF, 0x33, 0xc6, 0x59);
        }

        private void OnMutedChanged(object sender, EventArgs e)
        {
            if (sender is VoipGroupManager groupService && groupService.IsMuted is bool groupMuted)
            {
                this.BeginOnUIThread(() =>
                {
                    UpdateCurveColors(groupMuted);

                    Audio.IsChecked = !groupMuted;
                    Automation.SetToolTip(Audio, groupMuted ? Strings.VoipGroupUnmute : Strings.VoipGroupMute);
                });
            }
            else if (sender is VoipManager service && service.IsMuted is bool muted)
            {
                this.BeginOnUIThread(() => UpdateCurveColors(muted));
            }
        }

        private void OnAudioLevelsUpdated(object sender, IList<VoipGroupParticipant> args)
        {
            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                var average = 0f;

                foreach (var level in args)
                {
                    if (level.AudioSource == 0)
                    {
                        if (_groupService.IsMuted)
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

        private void OnAudioLevelUpdated(object sender, float average)
        {
            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _curveVisual.UpdateLevel(average);
            }
#endif
        }


        private void Audio_Click(object sender, RoutedEventArgs e)
        {
#if ENABLE_CALLS
            var service = _groupService;
            if (service != null)
            {
                service.IsMuted = !service.IsMuted;
            }
#endif
        }

        private async void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            var service = _groupService;
            if (service != null)
            {
                await service.ConsolidateAsync();
                await service.LeaveAsync();
            }
        }

        private async void Title_Click(object sender, RoutedEventArgs e)
        {
            if (_groupService != null)
            {
                await _groupService.ShowAsync();
            }
            else if (_service != null)
            {
                _service.Show();
            }
        }

        private void Curve_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _curveVisual.ActualSize = e.NewSize.ToVector2();
        }
    }
}
