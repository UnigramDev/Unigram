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
    public sealed partial class SelectTTLSecondsView : ContentDialog
    {
        public SelectTTLSecondsView(bool photo)
        {
            this.InitializeComponent();

            Title = Strings.Android.MessageLifetime;
            PrimaryButtonText = Strings.Android.OK;
            SecondaryButtonText = Strings.Android.Cancel;
            Footer.Text = photo 
                ? Strings.Android.MessageLifetimePhoto
                : Strings.Android.MessageLifetimeVideo;

            var seconds = new int[29];
            for (int i = 0; i < seconds.Length; i++)
            {
                seconds[i] = i;
            }

            FieldSeconds.ItemsSource = seconds;
        }

        public int? TTLSeconds { get; set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
