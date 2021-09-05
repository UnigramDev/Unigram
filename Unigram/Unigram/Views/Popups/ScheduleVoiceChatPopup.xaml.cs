using System;
using System.Linq;
using Unigram.Common;
using Unigram.Controls;
using Windows.System.UserProfile;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class ScheduleVoiceChatPopup : ContentPopup
    {
        public ScheduleVoiceChatPopup(bool channel)
        {
            InitializeComponent();

            var date = DateTime.Now.AddMinutes(10);
            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = Native.NativeUtils.GetCurrentCulture();
            Time.Language = Native.NativeUtils.GetCurrentCulture();

            Date.MinDate = DateTime.Today;
            Date.MaxDate = DateTime.Today.AddDays(7);

            Title = channel ? Strings.Resources.VoipChannelScheduleVoiceChat : Strings.Resources.VoipGroupScheduleVoiceChat;
            PrimaryButtonText = Strings.Resources.Schedule;
            SecondaryButtonText = Strings.Resources.Cancel;

            Message.Text = string.Format(channel ? Strings.Resources.VoipChannelScheduleInfo : Strings.Resources.VoipGroupScheduleInfo, "yolo");

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
    }
}
