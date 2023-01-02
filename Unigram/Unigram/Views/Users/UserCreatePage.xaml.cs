//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Unigram.Common;
using Unigram.ViewModels.Users;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Users
{
    public sealed partial class UserCreatePage : HostedPage
    {
        public UserCreateViewModel ViewModel => DataContext as UserCreateViewModel;

        public UserCreatePage()
        {
            InitializeComponent();
            Title = Strings.Resources.AddContactTitle;
        }

        private void FirstName_Loaded(object sender, RoutedEventArgs e)
        {
            FirstName.Focus(FocusState.Keyboard);
        }

        #region Binding

        private ImageSource ConvertPhoto(string firstName, string lastName)
        {
            return PlaceholderHelper.GetNameForUser(firstName, lastName, 64);
        }

        #endregion
    }
}
