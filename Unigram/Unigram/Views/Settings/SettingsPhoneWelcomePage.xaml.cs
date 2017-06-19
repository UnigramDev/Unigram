using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Controls;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPhoneWelcomePage : Page
    {
        public SettingsPhoneWelcomePage()
        {
            this.InitializeComponent();
        }

        private async void Change_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await TLMessageDialog.ShowAsync("All your Telegram contacts will get your new number added to their address book, provided they had your old number and you haven't blocked them in Telegram.", "Telegram", "OK", "Cancel");
            if (confirm == ContentDialogResult.Primary)
            {
                Frame.Navigate(typeof(SettingsPhonePage));
                Frame.BackStack.RemoveAt(1);
            }
        }
    }
}
