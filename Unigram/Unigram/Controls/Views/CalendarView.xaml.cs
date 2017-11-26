using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class CalendarView : ContentDialog
    {
        public CalendarView()
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Android.OK;
            SecondaryButtonText = Strings.Android.Cancel;
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
