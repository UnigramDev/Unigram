//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Windows.System.UserProfile;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Calls.Popups
{
    public sealed partial class ScheduleVideoChatPopup : ContentPopup
    {
        private readonly bool _channel;

        public ScheduleVideoChatPopup(bool channel)
        {
            InitializeComponent();

            _channel = channel;

            var date = DateTime.Now.AddMinutes(10);
            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = NativeUtils.GetCurrentCulture();
            Time.Language = NativeUtils.GetCurrentCulture();

            Date.MinDate = DateTime.Today;
            Date.MaxDate = DateTime.Today.AddDays(7);

            Title = channel ? Strings.VoipChannelScheduleVoiceChat : Strings.VoipGroupScheduleVoiceChat;
            PrimaryButtonText = Strings.Schedule;
            SecondaryButtonText = Strings.Cancel;

            Message.Text = string.Format(channel ? Strings.VoipChannelScheduleInfo : Strings.VoipGroupScheduleInfo, Locale.FormatTtl((int)(Value - DateTime.UtcNow).TotalSeconds));

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

        private void Date_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            Message.Text = string.Format(_channel ? Strings.VoipChannelScheduleVoiceChat : Strings.VoipGroupScheduleVoiceChat, Locale.FormatTtl((int)(Value - DateTime.UtcNow).TotalSeconds));
        }

        private void Time_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            Message.Text = string.Format(_channel ? Strings.VoipChannelScheduleVoiceChat : Strings.VoipGroupScheduleVoiceChat, Locale.FormatTtl((int)(Value - DateTime.UtcNow).TotalSeconds));
        }
    }
}
