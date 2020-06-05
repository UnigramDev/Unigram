using System;
using System.Linq;
using Unigram.Controls;
using Unigram.Converters;
using Windows.System.UserProfile;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class SupergroupEditRestrictedUntilPopup : ContentPopup
    {
        public SupergroupEditRestrictedUntilPopup(int until)
        {
            InitializeComponent();

            var date = until == 0 ? DateTime.Now.AddDays(1) : BindConvert.Current.DateTime(until);
            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = Native.NativeUtils.GetCurrentCulture();
            Time.Language = Native.NativeUtils.GetCurrentCulture();

            Date.MinYear = DateTime.Today;

            Title = Strings.Resources.UserRestrictionsUntil;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.UserRestrictionsUntilForever;
            CloseButtonText = Strings.Resources.Cancel;

            DefaultButton = ContentDialogButton.Primary;
        }

        public DateTime Value
        {
            get
            {
                return Date.Date.Add(Time.Time).UtcDateTime;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
