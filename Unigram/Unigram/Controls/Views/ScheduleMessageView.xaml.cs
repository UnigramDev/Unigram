using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class ScheduleMessageView : TLContentDialog
    {
        public ScheduleMessageView(User user, int until, bool reminder)
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

            Title = reminder ? Strings.Resources.SetReminder : Strings.Resources.ScheduleMessage;
            PrimaryButtonText = Strings.Resources.OK;
            //SecondaryButtonText = Strings.Resources.UserRestrictionsUntilForever;
            CloseButtonText = Strings.Resources.Cancel;

            if (user != null && !reminder)
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
