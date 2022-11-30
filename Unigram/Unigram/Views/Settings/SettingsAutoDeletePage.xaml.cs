using Unigram.ViewModels.Settings;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsAutoDeletePage : HostedPage
    {
        public SettingsAutoDeleteViewModel ViewModel => DataContext as SettingsAutoDeleteViewModel;

        public SettingsAutoDeletePage()
        {
            InitializeComponent();
            Title = Strings.Resources.AutoDeleteMessages;
        }

        private void OnChecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is RadioButton button && button.DataContext is SettingsOptionItem<int> option)
            {
                ViewModel.UpdateSelection(option.Value, true);
            }
        }
    }
}
