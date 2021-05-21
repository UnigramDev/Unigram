using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Gallery;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Users;
using Unigram.Views.Folders;
using Unigram.Views.Popups;
using Unigram.Views.Settings;
using Windows.ApplicationModel;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class SettingsPage : Page, ISettingsDelegate, IDisposable
    {
        public SettingsViewModel ViewModel => DataContext as SettingsViewModel;

        public SettingsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsViewModel, ISettingsDelegate>(this);

            NavigationCacheMode = NavigationCacheMode.Required;

            Diagnostics.Text = $"Unigram " + GetVersion();
        }

        public void Dispose()
        {
            //DataContext = null;
            //Bindings?.Update();
            Bindings?.StopTracking();
        }

        public static string GetVersion()
        {
            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;

            var type = Package.Current.SignatureKind switch
            {
                PackageSignatureKind.Store => "",
                PackageSignatureKind.Enterprise => " Direct",
                _ => " Debug"
            };

            if (version.Revision > 0)
            {
                return string.Format("{0}.{1}.{3} ({2}) {4}{5}", version.Major, version.Minor, version.Build, version.Revision, packageId.Architecture, type);
            }

            return string.Format("{0}.{1} ({2}) {3}{4}", version.Major, version.Minor, version.Build, packageId.Architecture, type);
        }

        private MasterDetailView _masterDetail;
        public MasterDetailView MasterDetail
        {
            get
            {
                return _masterDetail;
            }
            set
            {
                _masterDetail = value;
                ViewModel.NavigationService = value.NavigationService;
            }
        }

        private void Advanced_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsAdvancedPage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Phone_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsPhoneIntroPage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Username_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsUsernamePage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        public async void EditName_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                var user = ViewModel.ProtoService.GetUser(privata.UserId);
                if (user == null)
                {
                    return;
                }

                var dialog = new EditUserNamePopup(user.FirstName, user.LastName);

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    ViewModel.ProtoService.Send(new SetName(dialog.FirstName, dialog.LastName));
                }
            }
        }

        private async void About_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                var user = ViewModel.ProtoService.GetUserFull(privata.UserId);
                if (user == null)
                {
                    return;
                }

                var dialog = new EditYourAboutPopup(user.Bio);

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    ViewModel.ProtoService.Send(new SetBio(dialog.About));
                }
            }
        }

        private void Privacy_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsPrivacyAndSecurityPage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Stickers_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsStickersPage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Data_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsDataAndStoragePage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Folders_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(FoldersPage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Notifications_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsNotificationsPage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Appearance_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsAppearancePage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Language_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsLanguagePage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Questions_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.NavigateToInstant(Strings.Resources.TelegramFaqUrl);
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate)
            {
                var user = ViewModel.ProtoService.GetUser(chat);
                if (user == null || user.ProfilePhoto == null)
                {
                    return;
                }

                var userFull = ViewModel.ProtoService.GetUserFull(user.Id);
                if (userFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, ViewModel.Aggregator, user, userFull);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
        }

        public async void EditPhoto_Click(object sender, RoutedEventArgs e)
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
                        ViewModel.EditPhotoCommand.Execute(media);
                    }
                }
            }
            catch { }
        }

        #region Binding

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 64);
            Title.Text = user.GetFullName();

            Verified.Visibility = user.IsVerified ? Visibility.Visible : Visibility.Collapsed;

#if DEBUG
            PhoneNumber.Badge = "+42 --- --- ----";
#else
            if (ViewModel.Settings.UseTestDC)
            {
                PhoneNumber.Badge = "+42 --- --- ----";
            }
            else
            {
                PhoneNumber.Badge = Common.PhoneNumber.Format(user.PhoneNumber);
            }
#endif

            Username.Badge = string.IsNullOrEmpty(user.Username) ? Strings.Resources.UsernameEmpty : $"{user.Username}";
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            Bio.Badge = string.IsNullOrEmpty(fullInfo.Bio) ? Strings.Resources.UserBioDetail : fullInfo.Bio;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
        }

        public void UpdateChat(Chat chat)
        {
        }

        public void UpdateChatTitle(Chat chat)
        {
        }

        public void UpdateChatPhoto(Chat chat)
        {
        }



        public void UpdateFile(File file)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var user = ViewModel.CacheService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            if (user.UpdateFile(file))
            {
                Photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 64);
            }
        }

        #endregion

        private int _advanced;

        private void Diagnostics_Click(object sender, RoutedEventArgs e)
        {
            _advanced++;

            if (_advanced >= 10)
            {
                _advanced = 0;

                MasterDetail.NavigationService.Navigate(typeof(DiagnosticsPage));
            }
        }
    }
}
