//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Gallery;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Business;
using Telegram.Views.Folders;
using Telegram.Views.Settings;
using Telegram.Views.Stars.Popups;

namespace Telegram.Views
{
    public sealed partial class SettingsPage : Page, ISettingsDelegate, IDisposable
    {
        public SettingsViewModel ViewModel => DataContext as SettingsViewModel;

        public SettingsPage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Required;

            _settings = new Dictionary<Type, object>
            {
                { typeof(SettingsProfilePage), Profile },
                { typeof(SettingsAppearancePage), Appearance },
                { typeof(SettingsPrivacyAndSecurityPage), Privacy },
                { typeof(SettingsNotificationsPage), Notifications },
                { typeof(SettingsDataAndStoragePage), Data },
                { typeof(SettingsPowerSavingPage), PowerSaving },
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

        private void Navigate(Type type)
        {
            if (MasterDetail.NavigationService.Navigate(type))
            {
                UpdateSelection();
            }
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(SettingsProfilePage));
        }

        private void Privacy_Click(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(SettingsPrivacyAndSecurityPage));
        }

        private void PowerSaving_Click(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(SettingsPowerSavingPage));
        }

        private void Data_Click(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(SettingsDataAndStoragePage));
        }

        private void Folders_Click(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(FoldersPage));
        }

        private void Notifications_Click(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(SettingsNotificationsPage));
        }

        private void Appearance_Click(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(SettingsAppearancePage));
        }

        private void Sessions_Click(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(SettingsSessionsPage));
        }

        private void Language_Click(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(SettingsLanguagePage));
        }

        private void Advanced_Click(object sender, RoutedEventArgs e)
        {
            Navigate(typeof(SettingsAdvancedPage));
        }

        private void Questions_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.NavigateToInstant(Strings.TelegramFaqUrl);
        }

        private void PrivacyPolicy_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.NavigateToInstant(Strings.PrivacyPolicyUrl);
        }

        private void Premium_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.ShowPromo(new PremiumSourceSettings());
        }

        private async void Stars_Click(object sender, RoutedEventArgs e)
        {
            await MasterDetail.NavigationService.ShowPopupAsync(typeof(StarsPopup));
        }

        private void Business_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(BusinessPage));
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            await GalleryWindow.ShowAsync(ViewModel, ViewModel.StorageService, chat, () => Photo);
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

        private void VersionLabel_Navigate(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(DiagnosticsPage));
        }
    }
}
