//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.System.UserProfile;

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

        public DateTime Value
        {
            get
            {
                if (Date.Date is DateTimeOffset date)
                {
                    return date.Add(Time.Time).UtcDateTime;
                }

                return DateTime.MinValue;
            }
        }

        public bool IsUntilOnline { get; private set; }

        private void OnActionButtonClick(TeachingTip sender, object args)
        {
            if (IsUntilOnline)
            {
                return;
            }

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

            _tsc.TrySetResult(ContentDialogResult.Primary);
            IsOpen = false;
        }

        private void Online_Click(object sender, RoutedEventArgs e)
        {
            IsUntilOnline = true;

            _tsc.TrySetResult(ContentDialogResult.Primary);
            IsOpen = false;
        }

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
