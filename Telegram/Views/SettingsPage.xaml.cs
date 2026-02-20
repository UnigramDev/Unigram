//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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
using Telegram.Views.Stars;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views
{
    public sealed partial class SettingsPage : Page, ISettingsDelegate, IDisposable
    {
        public SettingsViewModel ViewModel => DataContext as SettingsViewModel;

        public SettingsPage()
        {
            InitializeComponent();

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

        public void UpdateSelection(bool clearBackStack = true)
        {
            object FindRoot()
            {
                if (_settings.TryGetValue(ViewModel.NavigationService.CurrentPageType, out object item))
                {
                    return item;
                }

                for (int i = ViewModel.NavigationService.Frame.BackStack.Count - 1; i >= 0; i--)
                {
                    if (_settings.TryGetValue(ViewModel.NavigationService.Frame.BackStack[i].SourcePageType, out item))
                    {
                        return item;
                    }
                }

                return null;
            }

            if (clearBackStack)
            {
                ViewModel.NavigationService.GoBackAt(0, false);
            }

            Navigation.SelectedItem = FindRoot();
        }

        private void Navigate(Type type)
        {
            if (ViewModel.NavigationService.Navigate(type))
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
            ViewModel.NavigationService.NavigateToInstant(Strings.TelegramFaqUrl);
        }

        private void PrivacyPolicy_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.NavigateToInstant(Strings.PrivacyPolicyUrl);
        }

        private void Features_Click(object sender, RoutedEventArgs e)
        {
            if (Uri.TryCreate(Strings.TelegramFeaturesUrl, UriKind.Absolute, out Uri tipsUri))
            {
                MessageHelper.OpenTelegramUrl(ViewModel.ClientService, ViewModel.NavigationService, tipsUri);
            }
        }

        private void Premium_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.ShowPromo(new PremiumSourceSettings());
        }

        private void Stars_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(StarsPage));
        }

        private void Business_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(BusinessPage));
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
            {
                await GalleryWindow.ShowAsync(ViewModel, ViewModel.StorageService, user, Photo);
            }
        }

        #region Binding

        public void UpdateUser(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            Title.Text = user.FullName();
            Photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);
            Identity.SetStatus(ViewModel.ClientService, user, BotVerified);
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
        }

        #endregion

        private void VersionLabel_Navigate(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(DiagnosticsPage));
        }
    }
}
