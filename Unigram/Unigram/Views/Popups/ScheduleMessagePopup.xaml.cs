using System;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class ScheduleMessagePopup : ContentPopup
    {
        public ScheduleMessagePopup(User user, int until, bool reminder)
        {
            InitializeComponent();

            var date = until == 0 ? DateTime.Now.AddMinutes(1) : BindConvert.Current.DateTime(until);
            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = Native.NativeUtils.GetCurrentCulture();
            Time.Language = Native.NativeUtils.GetCurrentCulture();

            Date.MinDate = DateTime.Today;
            Date.MaxDate = DateTime.Today.AddYears(1);

            Time.Time = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(10));

            Title = reminder ? Strings.Resources.SetReminder : Strings.Resources.ScheduleMessage;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            if (user != null && !(user.Status is UserStatusRecently) && !reminder)
            {
                Online.Content = string.Format(Strings.Resources.MessageScheduledUntilOnline, user.FirstName);
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
            if (Date.Date == null)
            {
                VisualUtilities.ShakeView(Date);
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
