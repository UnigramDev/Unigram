using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings.Popups
{
    public sealed partial class SettingsUsernamePopup : ContentPopup
    {
        public SettingsUsernameViewModel ViewModel => DataContext as SettingsUsernameViewModel;

        public SettingsUsernamePopup()
        {
            InitializeComponent();

            Title = Strings.Resources.Username;
            PrimaryButtonText = Strings.Resources.Done;
            SecondaryButtonText = Strings.Resources.Cancel;

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
            Username.SelectionStart = Username.Text.Length;
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

        private async void ContentPopup_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();
            var confirm = await ViewModel.SendAsync();

            args.Cancel = !confirm;
            deferral.Complete();
        }
    }
}
