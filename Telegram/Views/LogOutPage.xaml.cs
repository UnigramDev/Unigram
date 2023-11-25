//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.ViewModels;
using Telegram.Views.Host;
using Windows.UI.Xaml;

namespace Telegram.Views
{
    public sealed partial class LogOutPage : HostedPage
    {
        public LogOutViewModel ViewModel => DataContext as LogOutViewModel;

        public LogOutPage()
        {
            InitializeComponent();
            Title = Strings.LogOutTitle;

            if (TypeResolver.Current.Count < 3)
            {
                FindName(nameof(AddAccount));
            }
        }

        private void AddAnotherAccount_Click(object sender, RoutedEventArgs e)
        {
            if (Window.Current.Content is RootPage root)
            {
                root.Create();
            }
        }
    }
}
