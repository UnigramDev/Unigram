//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ScheduleMessagePopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly bool _reminder;

        public ScheduleMessagePopup(IClientService clientService, INavigationService navigationService, User user, bool reminder)
            : this(clientService, navigationService, user, reminder, DateTime.Now.AddMinutes(10), 0)
        {

        }

        public ScheduleMessagePopup(IClientService clientService, INavigationService navigationService, User user, bool reminder, DateTime date, int repeat)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _reminder = reminder;

            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = NativeUtils.GetCurrentCulture();
            Time.Language = NativeUtils.GetCurrentCulture();

            Date.MinDate = DateTime.Today;
            Date.MaxDate = DateTime.Today.AddYears(1);

            int? period = null;

            var max = 2147483647;
            foreach (var days in _repeatIndexer)
            {
                int abs = Math.Abs(repeat - days);
                if (abs < max)
                {
                    max = abs;
                    period = days;
                }
            }

            _repeat = period ?? _repeatIndexer[2];
            RepeatSelector.IsEnabled = _clientService.IsPremium;
            RepeatButton.Visibility = _clientService.IsPremium
                ? Visibility.Collapsed
                : Visibility.Visible;

            Title = reminder ? Strings.SetReminder : Strings.ScheduleMessage;
            PrimaryButtonText = Strings.OK;

            if (user != null && user.Type is UserTypeRegular && user.Status is not UserStatusRecently && !reminder)
            {
                CloseButtonText = string.Format(Strings.MessageScheduledUntilOnline, user.FirstName);
            }

            DefaultButton = ContentDialogButton.Primary;

            UpdatePrimaryButtonText();
        }

        public MessageSchedulingState SchedulingState { get; private set; }

        private DateTime GetDateTime(bool utc)
        {
            if (utc)
            {
                if (Date.Date is DateTimeOffset dateUtc)
                {
                    return dateUtc.Add(Time.Time).UtcDateTime;
                }
            }

            if (Date.Date is DateTimeOffset date)
            {
                return date.Add(Time.Time).DateTime;
            }

            return DateTime.MinValue;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (Date.Date == null || Date.Date < DateTime.Today)
            {
                VisualUtilities.ShakeView(Date);
                args.Cancel = true;
            }
            else if (Date.Date == DateTime.Today && Time.Time <= DateTime.Now.TimeOfDay)
            {
                VisualUtilities.ShakeView(Time);
                args.Cancel = true;
            }
            else
            {
                SchedulingState = new MessageSchedulingStateSendAtDate(GetDateTime(true).ToTimestamp(), _repeat);
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SchedulingState = new MessageSchedulingStateSendWhenOnline();
        }

        private void Date_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            UpdatePrimaryButtonText();
        }

        private void Time_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            UpdatePrimaryButtonText();
        }

        private void UpdatePrimaryButtonText()
        {
            var date = GetDateTime(false);
            try
            {
                if (date.Date == DateTime.Today)
                {
                    PrimaryButtonText = date.ToString(_reminder ? Strings.RemindTodayAt : Strings.SendTodayAt);
                }
                else if (date.Year == DateTime.Today.Year)
                {
                    PrimaryButtonText = date.ToString(_reminder ? Strings.RemindDayAt : Strings.SendDayAt);
                }
                else
                {
                    PrimaryButtonText = date.ToString(_reminder ? Strings.RemindDayYearAt : Strings.SendDayYearAt);
                }
            }
            catch
            {
                WatchDog.TrackError("Failed to format string for " + LocaleService.Current.Id);

                PrimaryButtonText = date.ToString();
            }
        }

        private int _repeat;
        public int Repeat
        {
            get => Array.IndexOf(_repeatIndexer, _repeat);
            set
            {
                if (value >= 0 && value < _repeatIndexer.Length && _repeat != _repeatIndexer[value])
                {
                    _repeat = _repeatIndexer[value];
                }
            }
        }

        private readonly int[] _repeatIndexer = new[]
        {
            0,
            86400,
            7 * 86400,
            14 * 86400,
            30 * 86400,
            91 * 86400,
            182 * 86400,
            365 * 86400
        };

        public List<SettingsOptionItem<int>> RepeatOptions { get; } = new()
        {
            new SettingsOptionItem<int>(0, Strings.MessageScheduledRepeatOptionNever),
            new SettingsOptionItem<int>(86400, Strings.MessageScheduledRepeatOptionDaily),
            new SettingsOptionItem<int>(7 * 86400, Strings.MessageScheduledRepeatOptionWeekly),
            new SettingsOptionItem<int>(14 * 86400, Strings.MessageScheduledRepeatOptionBiweekly),
            new SettingsOptionItem<int>(30 * 86400, Strings.MessageScheduledRepeatOptionMonthly),
            new SettingsOptionItem<int>(91 * 86400, Strings.MessageScheduledRepeatOption3Monthly),
            new SettingsOptionItem<int>(182 * 86400, Strings.MessageScheduledRepeatOption6Monthly),
            new SettingsOptionItem<int>(365 * 86400, Strings.MessageScheduledRepeatOptionYearly),
        };

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            ToastPopup.ShowFeaturePromo(_navigationService, Strings.MessageScheduledRepeatPremium, null);
        }
    }
}
