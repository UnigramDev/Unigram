//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Streams;
using Telegram.ViewModels.Settings.Password;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordCreatePage : HostedPage
    {
        public SettingsPasswordCreateViewModel ViewModel => DataContext as SettingsPasswordCreateViewModel;

        public SettingsPasswordCreatePage()
        {
            InitializeComponent();

            Walkthrough.HeaderSource = new LocalFileSource("ms-appx:///Assets/Animations/AuthorizationStateWaitPassword.tgs")
            {
                Markers = new Dictionary<string, int>
                {
                    { "Close", 40 },
                    { "CloseToPeek", 40 + 16 },
                }
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        private void Walkthrough_Loaded(object sender, RoutedEventArgs e)
        {
            Walkthrough.Header.Play();
        }

        private void Reveal_Click(object sender, RoutedEventArgs e)
        {
            Field1.PasswordRevealMode = Reveal.IsChecked == true ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
            Field2.PasswordRevealMode = Reveal.IsChecked == true ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
            Walkthrough.Header.Seek(Reveal.IsChecked == true ? "Close" : "CloseToPeek");
        }
    }
}
