using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
    public sealed partial class CalendarView : ContentDialog
    {
        public CalendarView()
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
