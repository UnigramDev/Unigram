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
using Telegram.Controls;
using Telegram.Native;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ScheduleMessageToast : TeachingTipEx
    {
        private readonly TaskCompletionSource<ContentDialogResult> _tsc = new();

        public ScheduleMessageToast(User user, bool reminder)
        {
            InitializeComponent();

            var date = DateTime.Now.AddMinutes(10);
            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = NativeUtils.GetCurrentCulture();
            Time.Language = NativeUtils.GetCurrentCulture();

            Date.MinDate = DateTime.Today;
            Date.MaxDate = DateTime.Today.AddYears(1);

            Title = reminder ? Strings.SetReminder : Strings.ScheduleMessage;
            ActionButtonContent = Strings.OK;
            CloseButtonContent = Strings.Cancel;

            if (user != null && user.Type is UserTypeRegular && user.Status is not UserStatusRecently && !reminder)
            {
                Online.Content = string.Format(Strings.MessageScheduledUntilOnline, user.FirstName);
            }
            else
            {
                Online.Visibility = Visibility.Collapsed;
            }

            ActionButtonClick += OnActionButtonClick;
            Closed += OnClosed;
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

        private void OnActionButtonClick(TeachingTip sender, object args)
        {
            if (Date.Date == null || Date.Date < DateTime.Today)
            {
                VisualUtilities.ShakeView(Date);
                return;
            }
            else if (Date.Date == DateTime.Today && Time.Time <= DateTime.Now.TimeOfDay)
            {
                VisualUtilities.ShakeView(Time);
                return;
            }

            SchedulingState = new MessageSchedulingStateSendAtDate(GetDateTime(true).ToTimestamp(), _repeat);

            _tsc.TrySetResult(ContentDialogResult.Primary);
            IsOpen = false;
        }

        private void Online_Click(object sender, RoutedEventArgs e)
        {
            SchedulingState = new MessageSchedulingStateSendWhenOnline();

            _tsc.TrySetResult(ContentDialogResult.Primary);
            IsOpen = false;
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

        private void OnClosed(TeachingTip sender, TeachingTipClosedEventArgs args)
        {
            _tsc.TrySetResult(ContentDialogResult.Secondary);
        }

        public Task<ContentDialogResult> ShowAsync(XamlRoot xamlRoot)
        {
            if (xamlRoot.Content is not IToastHost host)
            {
                return Task.FromResult(ContentDialogResult.None);
            }

            XamlRoot = xamlRoot;
            Closed += (s, args) =>
            {
                host.ToastClosed(s);
            };

            host.ToastOpened(this);

            IsOpen = true;
            return _tsc.Task;
        }
    }
}
