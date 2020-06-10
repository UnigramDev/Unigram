using Unigram.Common;
using Unigram.ViewModels.Settings.Password;

namespace Unigram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordHintPage : HostedPage
    {
        public SettingsPasswordHintViewModel ViewModel => DataContext as SettingsPasswordHintViewModel;

        public SettingsPasswordHintPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasswordHintViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }
    }
}
