using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsVoIPPage : HostedPage
    {
        public SettingsVoIPViewModel ViewModel => DataContext as SettingsVoIPViewModel;

        public SettingsVoIPPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsVoIPViewModel>();
        }
    }
}
