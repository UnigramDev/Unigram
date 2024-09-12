//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Td.Api;
using Windows.System.UserProfile;

namespace Telegram.Views.Popups
{
    public sealed partial class ScheduleMessagePopup : ContentPopup
    {
        public ScheduleMessagePopup(User user, bool reminder)
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
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            if (user != null && user.Type is UserTypeRegular && user.Status is not UserStatusRecently && !reminder)
            {
                Online.Content = string.Format(Strings.MessageScheduledUntilOnline, user.FirstName);
            }
            else
            {
                Online.Visibility = Visibility.Collapsed;
            }

            DefaultButton = ContentDialogButton.Primary;
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

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (IsUntilOnline)
            {
                return;
            }

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
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Online_Click(object sender, RoutedEventArgs e)
        {
            IsUntilOnline = true;
            Hide(ContentDialogResult.Primary);
        }
    }
}
