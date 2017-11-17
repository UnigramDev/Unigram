using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Controls;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPhoneIntroPage : Page
    {
        public SettingsPhoneIntroViewModel ViewModel => DataContext as SettingsPhoneIntroViewModel;

        public SettingsPhoneIntroPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsPhoneIntroViewModel>();
        }

        private async void Change_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Android.PhoneNumberAlert, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                Frame.Navigate(typeof(SettingsPhonePage));
                Frame.BackStack.Remove(Frame.BackStack.Last());
            }
        }
    }
}
