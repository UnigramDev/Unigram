using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsNotificationsPage : HostedPage
    {
        public SettingsNotificationsViewModel ViewModel => DataContext as SettingsNotificationsViewModel;

        public SettingsNotificationsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsNotificationsViewModel>();
        }

        #region Binding

        private string ConvertCountInfo(bool count)
        {
            return count ? "Switch off to show the number of unread chats instead of messages" : "Switch on to show the number of unread messages instead of chats";
        }

        #endregion

    }
}
