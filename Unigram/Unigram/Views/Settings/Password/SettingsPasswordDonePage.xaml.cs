using Unigram.Common;
using Unigram.ViewModels.Settings.Password;

namespace Unigram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordDonePage : HostedPage
    {
        public SettingsPasswordDoneViewModel ViewModel => DataContext as SettingsPasswordDoneViewModel;

        public SettingsPasswordDonePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasswordDoneViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }
    }
}
