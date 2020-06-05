using System;
using System.Linq;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPhoneIntroPage : Page
    {
        public SettingsPhoneIntroViewModel ViewModel => DataContext as SettingsPhoneIntroViewModel;

        public SettingsPhoneIntroPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPhoneIntroViewModel>();
        }

        private async void Change_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.PhoneNumberAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                Frame.Navigate(typeof(SettingsPhonePage));
                Frame.BackStack.Remove(Frame.BackStack.Last());
            }
        }

        #region Binding

        private string ConvertPhoneNumber(string number)
        {
            return PhoneNumber.Format(number);
        }

        #endregion
    }
}
