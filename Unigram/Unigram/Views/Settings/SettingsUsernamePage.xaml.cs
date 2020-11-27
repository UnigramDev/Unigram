using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsUsernamePage : HostedPage
    {
        public SettingsUsernameViewModel ViewModel => DataContext as SettingsUsernameViewModel;

        public SettingsUsernamePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsUsernameViewModel>();

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => Username.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += (s, args) =>
            {
                if (ViewModel.UpdateIsValid(Username.Text))
                {
                    ViewModel.CheckAvailability(Username.Text);
                }
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Username.Focus(FocusState.Keyboard);
        }

        private void Copy_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            ViewModel.CopyCommand.Execute();
        }

        #region Binding

        private string ConvertAvailable(string username)
        {
            return string.Format(Strings.Resources.UsernameAvailable, username);
        }

        private string ConvertUsername(string username)
        {
            return MeUrlPrefixConverter.Convert(ViewModel.CacheService, username);
        }

        public string UsernameHelpLink
        {
            get
            {
                return string.Format(Strings.Resources.UsernameHelpLink, string.Empty).TrimEnd();
            }
        }

        #endregion
    }
}
