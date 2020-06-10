using Unigram.ViewModels.Settings.Password;

namespace Unigram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordIntroPage : HostedPage
    {
        public SettingsPasswordIntroViewModel ViewModel => DataContext as SettingsPasswordIntroViewModel;

        public SettingsPasswordIntroPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasswordIntroViewModel>();
        }
    }
}
