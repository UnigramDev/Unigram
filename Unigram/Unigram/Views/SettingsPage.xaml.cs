using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Gallery;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
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
            UpdateSelection(false);
        }

        private void UpdateSelection(bool clearBackStack = true)
        {
            object FindRoot()
            {
                if (_settings.TryGetValue(_masterDetail.NavigationService.CurrentPageType, out object item))
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

            if (clearBackStack)
            {
                MasterDetail.NavigationService.GoBackAt(0, false);
            }

            Navigation.SelectedItem = FindRoot();
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsProfilePage));
            UpdateSelection();
        }

        private void Privacy_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsPrivacyAndSecurityPage));
            UpdateSelection();
        }

        private void Stickers_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsStickersPage));
            UpdateSelection();
        }

        private void Data_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsDataAndStoragePage));
            UpdateSelection();
        }

        private void Folders_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(FoldersPage));
            UpdateSelection();
        }

        private void Notifications_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsNotificationsPage));
            UpdateSelection();
        }

        private void Appearance_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsAppearancePage));
            UpdateSelection();
        }

        private void Sessions_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsSessionsPage));
            UpdateSelection();
        }

        private void Language_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsLanguagePage));
            UpdateSelection();
        }

        private void Advanced_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsAdvancedPage));
            UpdateSelection();
        }

        private void Questions_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.NavigateToInstant(Strings.Resources.TelegramFaqUrl);
            UpdateSelection();
        }

        private void PrivacyPolicy_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.NavigateToInstant(Strings.Resources.PrivacyPolicyUrl);
            UpdateSelection();
        }

        private void Premium_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.ShowPromo(new PremiumSourceSettings());
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            await GalleryView.ShowAsync(ViewModel.ClientService, ViewModel.StorageService, ViewModel.Aggregator, chat, () => Photo);
        }

        #region Binding

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Title.Text = user.FullName();
            Photo.SetUser(ViewModel.ClientService, user, 48);
            Identity.SetStatus(ViewModel.ClientService, user);
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
