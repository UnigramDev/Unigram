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
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Business;
using Telegram.Views.Folders;
using Telegram.Views.Grams;
using Telegram.Views.Host;
using Telegram.Views.Settings;
using Telegram.Views.Stars;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

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
                else if (ViewModel.NavigationService.CurrentPageType == typeof(ProfilePage) && ViewModel.NavigationService.CurrentPageParam is long chatId && chatId == ViewModel.ClientService.Options.MyId)
                {
                    return Profile;
                }

                for (int i = ViewModel.NavigationService.Frame.BackStack.Count - 1; i >= 0; i--)
                {
                    if (_settings.TryGetValue(ViewModel.NavigationService.Frame.BackStack[i].SourcePageType, out item))
                    {
                        return item;
                    }
                    else if (ViewModel.NavigationService.Frame.BackStack[i].SourcePageType == typeof(ProfilePage) && ViewModel.NavigationService.Frame.BackStack[i].Parameter is long chatId && chatId == ViewModel.ClientService.Options.MyId)
                    {
                        return Profile;
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

        private long _sessionsHash;

        public void UpdateAccounts()
        {
            var items = LifetimeService.Current.GetItems(out long sessionsHash);

            if (_sessionsHash != sessionsHash)
            {
                Accounts.Items.Clear();

                foreach (var item in items)
                {
                    if (item != ViewModel.Session)
                    {
                        Accounts.Items.Add(item);
                    }
                }

                if (items.Count < Constants.MaxAccountCount)
                {
                    AddAccount.Visibility = Visibility.Visible;
                }
                else
                {
                    AddAccount.Visibility = Visibility.Collapsed;
                }

                _sessionsHash = sessionsHash;
            }
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
            if (ViewModel.NavigationService.Navigate(typeof(ProfilePage), ViewModel.ClientService.Options.MyId))
            {
                UpdateSelection();
            }
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

        private void Grams_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(GramsPage));
        }

        private void Wallet_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.NavigateToWallet();
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

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.Item is ISession session)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;
                if (content == null)
                {
                    return;
                }

                var user = session.ClientService.GetUser(session.UserId);
                if (user == null)
                {
                    session.ClientService.Send(new GetUser(session.UserId));
                    return;
                }

                var title = content.FindName("TitleLabel") as TextBlock;
                title.Text = user.FullName();

                var photo = content.Children[0] as ProfilePicture;
                photo.Source = ProfilePictureSource.User(session.ClientService, user);

                var identity = content.FindName("Identity") as IdentityIcon;
                var botVerified = content.FindName("BotVerified") as CustomEmojiIcon;

                identity.SetStatus(session.ClientService, user, botVerified);

                AutomationProperties.SetName(args.ItemContainer, user.FullName());
                args.Handled = true;
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            var root = this.GetParent<RootWindow>();
            if (root != null && e.ClickedItem is ISession session)
            {
                root.Switch(session);
            }
        }

        private void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            var premium = 0;
            var count = 0;

            foreach (var session in LifetimeService.Current.Items)
            {
                if (session.Settings.UseTestDC)
                {
                    continue;
                }

                if (session.ClientService.Options.IsPremium)
                {
                    premium++;
                }

                count++;
            }

            var limit = 3;

            if (count >= limit + premium)
            {
                ViewModel.NavigationService.ShowLimitReached(new PremiumLimitTypeConnectedAccounts());
                return;
            }

            var root = this.GetParent<RootWindow>();
            root.Switch(LifetimeService.Current.Create());
        }

        private void AddAccount_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var root = this.GetParent<RootWindow>();
            if (root != null)
            {
                var modifiers = WindowContext.KeyModifiers();
                if (modifiers == (VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift))
                {
                    var flyout = new MenuFlyout();
                    flyout.CreateFlyoutItem(() => root.Switch(LifetimeService.Current.Create(test: false)), "Production Server", Icons.Globe);
                    flyout.CreateFlyoutItem(() => root.Switch(LifetimeService.Current.Create(test: true)), "Test Server", Icons.Bug);
                    flyout.ShowAt(sender, args);
                }
            }
        }
    }
}
