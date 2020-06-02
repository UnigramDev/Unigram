using System;
using System.Reactive.Linq;
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

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(Username, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                if (ViewModel.UpdateIsValid(Username.Text))
                {
                    ViewModel.CheckAvailability(Username.Text);
                }
            });
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
