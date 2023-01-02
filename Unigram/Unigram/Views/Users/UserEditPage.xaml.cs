using Telegram.Td.Api;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Users;
using Windows.UI.Xaml;

namespace Unigram.Views.Users
{
    public sealed partial class UserEditPage : HostedPage, IUserDelegate
    {
        public UserEditViewModel ViewModel => DataContext as UserEditViewModel;

        public UserEditPage()
        {
            InitializeComponent();
            Title = Strings.Resources.EditContact;
        }

        #region Delegate

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Photo.SetUser(ViewModel.ClientService, user, 140);

            SuggestPhoto.Content = string.Format(Strings.Resources.SuggestPhotoFor, user.FirstName);
            PersonalPhoto.Content = string.Format(Strings.Resources.SetPhotoFor, user.FirstName);

            SharePhoneCheck.Content = string.Format(Strings.Resources.SharePhoneNumberWith, user.FirstName);
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            ResetPhoto.Visibility = fullInfo.PersonalPhoto == null ? Visibility.Collapsed : Visibility.Visible;

            SharePhone.Visibility = fullInfo.NeedPhoneNumberPrivacyException ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user) { }

        #endregion
    }
}
