using System;
using System.Collections.Generic;
using System.Linq;
using Unigram.Controls;
using Windows.System.UserProfile;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class CalendarPopup : ContentPopup
    {
        public CalendarPopup()
        {
            InitializeComponent();

            View.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            View.Language = Native.NativeUtils.GetCurrentCulture();

            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public DateTimeOffset MinDate
        {
            get
            {
                return View.MinDate;
            }
            set
            {
                View.MinDate = value;
            }
        }

        public DateTimeOffset MaxDate
        {
            get
            {
                return View.MaxDate;
            }
            set
            {
                View.MaxDate = value;
            }
        }

        public IList<DateTimeOffset> SelectedDates
        {
            get
            {
                return View.SelectedDates;
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
