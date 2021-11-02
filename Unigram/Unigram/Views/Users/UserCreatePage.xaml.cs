using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Unigram.Common;
using Unigram.ViewModels.Users;

namespace Unigram.Views.Users
{
    public sealed partial class UserCreatePage : HostedPage
    {
        public UserCreateViewModel ViewModel => DataContext as UserCreateViewModel;

        public UserCreatePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<UserCreateViewModel>();
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
