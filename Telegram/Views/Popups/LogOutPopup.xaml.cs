//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.ViewModels;
using Telegram.Views.Host;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views
{
    public sealed partial class LogOutPopup : ContentPopup
    {
        public LogOutViewModel ViewModel => DataContext as LogOutViewModel;

        public LogOutPopup()
        {
            InitializeComponent();
            Title = Strings.LogOutTitle;

            PrimaryButtonText = Strings.LogOutTitle;
            SecondaryButtonText = Strings.Cancel;

            if (TypeResolver.Current.Count < 3)
            {
                FindName(nameof(AddAccount));
            }

            Closed += OnClosed;
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (args.Result == ContentDialogResult.Primary)
            {
                ViewModel.Logout();
            }
        }

        private void AddAnotherAccount_Click(object sender, RoutedEventArgs e)
        {
            Hide();

            if (XamlRoot.Content is RootPage root)
            {
                root.Create();
            }
        }

        private void Passcode_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            ViewModel.OpenPasscode();
        }

        private void Storage_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            ViewModel.OpenStorage();
        }

        private void PhoneNumber_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            ViewModel.ChangePhoneNumber();
        }

        private void Question_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            ViewModel.Ask();
        }
    }
}
