//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.ComponentModel;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Users;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Users
{
    public sealed partial class UserAffiliatePage : HostedPage
    {
        public UserAffiliateViewModel ViewModel => DataContext as UserAffiliateViewModel;

        public UserAffiliatePage()
        {
            InitializeComponent();
            Title = Strings.BotAffiliateProgramTitle;

            SliderHelper.InitializeTicks(Commission, CommissionTicks, 2, ConvertCommissionTicks);
            SliderHelper.InitializeTicks(Duration, DurationTicks, 7, ConvertDurationTicks);
        }

        private DispatcherTimer _countdownTimer;

        private string ConvertCommissionValue(double value)
        {
            return string.Format("{0:P1}", value / 1000);
        }

        private string ConvertCommissionTicks(int arg)
        {
            return arg == 0 ? "0.1%" : "90%";
        }

        private string ConvertDurationTicks(int arg)
        {
            return arg switch
            {
                0 => "1m",
                1 => "3m",
                2 => "6m",
                3 => "1y",
                4 => "2y",
                5 => "3y",
                _ => "∞"
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateInfo(ViewModel.Info);
            UpdateMinimumCommission(ViewModel.MinimumCommission);
            UpdateMinimumDuration(ViewModel.MinimumDuration);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Info))
            {
                UpdateInfo(ViewModel.Info);
            }
            else if (e.PropertyName == nameof(ViewModel.MinimumCommission))
            {
                UpdateMinimumCommission(ViewModel.MinimumCommission);
            }
            else if (e.PropertyName == nameof(ViewModel.MinimumDuration))
            {
                UpdateMinimumDuration(ViewModel.MinimumDuration);
            }
        }

        private void UpdateInfo(AffiliateProgramInfo info)
        {
            UpdateCountdown();

            if (info == null)
            {
                ActionButton.Content = Strings.AffiliateProgramStart;
                ActionButtonInfo.Text = Strings.AffiliateProgramStartInfo;

                Stop.Visibility = Visibility.Collapsed;
            }
            else if (info.EndDate > DateTime.Now.ToTimestamp())
            {
                ActionButtonInfo.Text = Strings.AffiliateProgramStartInfo;

                Stop.Visibility = Visibility.Collapsed;
            }
            else
            {
                ActionButton.Content = Strings.AffiliateProgramUpdate;
                ActionButtonInfo.Text = Strings.AffiliateProgramUpdateInfo;

                Stop.Visibility = Visibility.Visible;
            }
        }

        private void UpdateMinimumCommission(int value)
        {
            MinimumCommission.Width = new GridLength(value, GridUnitType.Star);
            MaximumCommission.Width = new GridLength(900 - value, GridUnitType.Star);
            Commission.Minimum = value;
        }

        private void UpdateMinimumDuration(int value)
        {
            MinimumDuration.Width = new GridLength(value, GridUnitType.Star);
            MaximumDuration.Width = new GridLength(6 - value, GridUnitType.Star);
            Duration.Minimum = value;
        }

        private void UpdateCountdown()
        {
            _countdownTimer?.Stop();

            if (ViewModel.Info?.EndDate > DateTime.Now.ToTimestamp())
            {
                if (_countdownTimer == null)
                {
                    _countdownTimer = new DispatcherTimer();
                    _countdownTimer.Interval = TimeSpan.FromMilliseconds(500);
                    _countdownTimer.Tick += Countdown_Tick;
                }

                _countdownTimer.Start();
                Countdown_Tick(null, null);
            }
        }

        private void Countdown_Tick(object sender, object e)
        {
            var date = Formatter.ToLocalTime(ViewModel.Info?.EndDate ?? 0);
            var diff = date - DateTime.Now;

            if (diff > TimeSpan.Zero)
            {
                ActionButton.Content = string.Format(Strings.AffiliateProgramStartAvailableIn, diff.ToDuration());
            }
            else
            {
                _countdownTimer.Stop();
                ViewModel.Reset();
            }
        }
    }
}
