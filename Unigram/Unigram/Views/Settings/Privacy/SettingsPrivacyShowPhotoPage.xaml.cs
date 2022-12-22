using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Unigram.Views.Popups;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyShowPhotoPage : HostedPage, IUserDelegate
    {
        public SettingsPrivacyShowPhotoViewModel ViewModel => DataContext as SettingsPrivacyShowPhotoViewModel;

        public SettingsPrivacyShowPhotoPage()
        {
            InitializeComponent();
            Title = Strings.Resources.PrivacyProfilePhoto;
        }

        private async void EditPhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.MediaTypes);

                var media = await picker.PickSingleMediaAsync();
                if (media != null)
                {
                    var dialog = new EditMediaPopup(media, ImageCropperMask.Ellipse);

                    var confirm = await dialog.ShowAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await ViewModel.EditPhotoAsync(media);
                    }
                }
            }
            catch { }
        }

        private async void CreatePhoto_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.NavigationService.ShowAsync(typeof(CreateChatPhotoPopup), new CreateChatPhotoParameters(null, true));
        }

        #region Delegate

        public void UpdateUser(Chat chat, User user, bool secret) { }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (fullInfo != null)
            {
                UpdatePhoto.Content = fullInfo.PublicPhoto == null
                    ? Strings.Resources.SetPhotoForRest
                    : Strings.Resources.UpdatePhotoForRest;
                
                RemovePhoto.Visibility = fullInfo.PublicPhoto == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        public void UpdateUserStatus(Chat chat, User user) { }

        #endregion

        #region Binding

        private Visibility ConvertNever(PrivacyValue value)
        {
            return value is PrivacyValue.AllowAll or PrivacyValue.AllowContacts ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertAlways(PrivacyValue value)
        {
            return value is PrivacyValue.AllowContacts or PrivacyValue.DisallowAll ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

    }
}
