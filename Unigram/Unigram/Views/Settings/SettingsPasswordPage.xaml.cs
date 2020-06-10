using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPasswordPage : HostedPage
    {
        public SettingsPasswordViewModel ViewModel => DataContext as SettingsPasswordViewModel;

        public SettingsPasswordPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasswordViewModel>();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            //PrimaryInput.Focus(FocusState.Keyboard);
        }

        //private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        //{
        //    if (e.Key == Windows.System.VirtualKey.Enter)
        //    {
        //        ViewModel.SendCommand.Execute(sender);
        //        e.Handled = true;
        //    }
        //}
    }
}
