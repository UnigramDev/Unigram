using Unigram.ViewModels;
using Unigram.Views.Host;
using Unigram.Views.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views
{
    public sealed partial class LogOutPage : HostedPage
    {
        public LogOutViewModel ViewModel => DataContext as LogOutViewModel;

        public LogOutPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<LogOutViewModel>();
        }

        private void AddAnotherAccount_Click(object sender, RoutedEventArgs e)
        {
            if (Window.Current.Content is RootPage root)
            {
                root.Create();
            }
        }

        private void SetPasscode_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPasscodePage));
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStoragePage));
        }

        private void ChangePhoneNumber_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPhoneIntroPage));
        }
    }
}
