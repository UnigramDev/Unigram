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

            DataContext = UnigramContainer.Current.ResolveType<SettingsViewModel>();

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

        private void Accounts_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsAccountsPage));
        }
    }

    // Experiment
    public class TableStackPanel : StackPanel
    {
        //protected override Size ArrangeOverride(Size finalSize)
        //{
        //    if (finalSize.Width >= 500)
        //    {
        //        //Margin = new Thickness(12, 0, 12, 0);
        //        //CornerRadius = new CornerRadius(8);
        //        //BorderThickness = new Thickness(0);

        //        HyperButton first = null;
        //        HyperButton last = null;

        //        foreach (var item in Children)
        //        {
        //            if (item.Visibility == Visibility.Visible)
        //            {
        //                if (first == null)
        //                {
        //                    first = item as HyperButton;
        //                }

        //                last = item as HyperButton;

        //                if (last != null)
        //                {
        //                    last.BorderBrush = Application.Current.Resources["SystemControlForegroundBaseLowBrush"] as SolidColorBrush;
        //                }
        //            }
        //        }

        //        var lastRadius = new CornerRadius(0, 0, 8, 8);

        //        if (first != null)
        //        {
        //            if (first == last)
        //            {
        //                last.CornerRadius = new CornerRadius(8, 8, 8, 8);
        //                last.BorderBrush = null;
        //            }
        //            else
        //            {
        //                first.CornerRadius = new CornerRadius(8, 8, 0, 0);

        //                if (last != null)
        //                {
        //                    last.CornerRadius = new CornerRadius(0, 0, 8, 8);
        //                    last.BorderBrush = null;
        //                }
        //            }
        //        }
        //    }

        //    return base.ArrangeOverride(finalSize);
        //}
    }
}
