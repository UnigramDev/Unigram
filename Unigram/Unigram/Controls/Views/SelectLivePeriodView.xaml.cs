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
    public sealed partial class SelectLivePeriodView : ContentDialog
    {
        public SelectLivePeriodView(bool single, string name)
        {
            this.InitializeComponent();

            Title = Strings.Resources.SendLiveLocation;
            PrimaryButtonText = Strings.Resources.ShareFile;
            SecondaryButtonText = Strings.Resources.Cancel;
            Footer.Text = single
                ? string.Format(Strings.Resources.LiveLocationAlertPrivate, name)
                : Strings.Resources.LiveLocationAlertGroup;

            FieldSeconds.ItemsSource = new int[] { 15 * 60, 1 * 60 * 60, 8 * 60 * 60 };
        }

        public int Period { get; private set; } = 15 * 60;

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
