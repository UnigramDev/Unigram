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
    public sealed partial class PhoneCallRatingView : ContentDialog
    {
        public PhoneCallRatingView()
        {
            this.InitializeComponent();

            Title = Strings.Android.AppName;
            PrimaryButtonText = Strings.Android.OK;
            SecondaryButtonText = Strings.Android.Cancel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private Visibility ConvertVisibility(int rating)
        {
            return rating >= 0 && rating <= 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool ConvertBoolean(int rating)
        {
            return rating >= 0;
        }

        public int Rating { get; set; } = -1;

        public string Comment { get; set; }
    }
}
