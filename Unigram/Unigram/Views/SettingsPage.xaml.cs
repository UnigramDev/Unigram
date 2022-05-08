using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Gallery;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Users;
using Unigram.Views.Folders;
using Unigram.Views.Settings;
using Windows.ApplicationModel;
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

            _settings = new Dictionary<Type, object>
            {
                { typeof(SettingsProfilePage), Profile },
                { typeof(SettingsNotificationsPage), Notifications },
                { typeof(SettingsDataAndStoragePage), Data },
                { typeof(SettingsPrivacyAndSecurityPage), Privacy },
                { typeof(SettingsStickersPage), Stickers },
                { typeof(SettingsAppearancePage), Appearance },
                { typeof(FoldersPage), Folders },
                { typeof(SettingsSessionsPage), Sessions },
                { typeof(SettingsLanguagePage), Language },
                { typeof(SettingsAdvancedPage), Advanced }
            };
        }

        private readonly Dictionary<Type, object> _settings;

        public void Dispose()
        {
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
                _ => " Direct"
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
            get => _masterDetail;
            set
            {
                _masterDetail = value;
                _masterDetail.NavigationService.Frame.Navigated += OnNavigated;
                ViewModel.NavigationService = value.NavigationService;
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            object FindRoot()
            {
                if (_settings.TryGetValue(e.SourcePageType, out object item))
                {
                    return item;
                }

                for (int i = _masterDetail.NavigationService.Frame.BackStack.Count - 1; i >= 0; i--)
                {
                    if (_settings.TryGetValue(_masterDetail.NavigationService.Frame.BackStack[i].SourcePageType, out item))
                    {
                        return item;
                    }
                }

                return null;
            }

            Navigation.SelectedItem = FindRoot();
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsProfilePage));
            MasterDetail.NavigationService.GoBackAt(0, false);
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

        private void Sessions_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsSessionsPage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Language_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsLanguagePage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Advanced_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsAdvancedPage));
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void Questions_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.NavigateToInstant(Strings.Resources.TelegramFaqUrl);
            MasterDetail.NavigationService.GoBackAt(0, false);
        }

        private void PrivacyPolicy_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.NavigateToInstant(Strings.Resources.PrivacyPolicyUrl);
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

                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, ViewModel.StorageService, ViewModel.Aggregator, user, userFull);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
        }

        #region Binding

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Photo.SetUser(ViewModel.ProtoService, user, 48);
            Title.Text = user.GetFullName();

            Premium.Visibility = user.IsPremium
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
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
