using System;
using System.Diagnostics;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
using Unigram.Views.Settings;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel => DataContext as SettingsViewModel;

        public SettingsPage()
        {
            InitializeComponent();

            NavigationCacheMode = NavigationCacheMode.Required;

            DataContext = UnigramContainer.Instance.ResolveType<SettingsViewModel>();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

            OnStateChanged(null, null);
        }

        private void OnStateChanged(object p1, object p2)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Narrow)
            {
                Separator.BorderThickness = new Thickness(0);
            }
            else
            {
                Separator.BorderThickness = new Thickness(0, 0, 1, 0);
            }
        }

        RelayCommand NotifcationPageCommand => new RelayCommand(() => MasterDetail.NavigationService.Navigate(typeof(SettingsNotificationsPage)));
        RelayCommand PrivacyPageCommand => new RelayCommand(() => MasterDetail.NavigationService.Navigate(typeof(SettingsPrivacyPage)));
        RelayCommand StickersPageCommand => new RelayCommand(() => MasterDetail.NavigationService.Navigate(typeof(SettingsStickersPage)));
        RelayCommand WallpaperPageCommand => new RelayCommand(() => MasterDetail.NavigationService.Navigate(typeof(SettingsWallpaperPage)));

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //if (Frame.CanGoBack)
            //{
            //    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
            //        AppViewBackButtonVisibility.Visible;
            //}
            //else
            //{
            //    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
            //        AppViewBackButtonVisibility.Collapsed;
            //}

            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Settings");
            }

            ViewModel.NavigationService = MasterDetail.NavigationService;
        }

        private void Username_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsUsernamePage));
        }

        private async void EditName_Click(object sender, RoutedEventArgs e)
        {
            await MasterDetail.NavigationService.NavigateModalAsync(typeof(EditYourNameView));
        }

        private void Privacy_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsPrivacyPage));
        }

        private void Stickers_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsStickersPage));
        }

        private void Data_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsDataPage));
        }

        private void Notifications_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsNotificationsPage));
        }
    }
}
