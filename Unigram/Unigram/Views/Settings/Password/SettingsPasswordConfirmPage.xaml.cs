using Unigram.Common;
using Unigram.ViewModels.Settings.Password;

namespace Unigram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordConfirmPage : HostedPage
    {
        public SettingsPasswordConfirmViewModel ViewModel => DataContext as SettingsPasswordConfirmViewModel;

        public SettingsPasswordConfirmPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasswordConfirmViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }

        #region Binding

        private string ConvertPattern(string pattern)
        {
            return string.Format("[Please enter code we've just emailed at **{0}**]", pattern);
        }

        #endregion

    }
}
