//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Telegram.Controls;
using Telegram.ViewModels.Users;

namespace Telegram.Views.Users
{
    public sealed partial class UserCreatePage : HostedPage
    {
        public UserCreateViewModel ViewModel => DataContext as UserCreateViewModel;

        public UserCreatePage()
        {
            InitializeComponent();
            Title = Strings.AddContactTitle;
        }

        private void FirstName_Loaded(object sender, RoutedEventArgs e)
        {
            FirstName.Focus(FocusState.Keyboard);
        }

        #region Binding

        private PlaceholderImage ConvertPhoto(string firstName, string lastName)
        {
            return PlaceholderImage.GetNameForUser(firstName, lastName);
        }

        #endregion
    }
}
