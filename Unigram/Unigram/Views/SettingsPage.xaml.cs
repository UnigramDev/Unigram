using GalaSoft.MvvmLight.Command;
using System;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
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
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            DataContext = UnigramContainer.Instance.ResolverType<SettingsViewModel>();
            Loaded += OnLoaded;
        }


        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            OnStateChanged(null, null);
        }

        private void OnStateChanged(object p1, object p2)
        {
            
        }
        RelayCommand NotifcationPageCommand => new RelayCommand(NotifcationPageNavigate);
        RelayCommand PrivacyPageCommand => new RelayCommand(PrivacyPageNavigate);
        RelayCommand ChatSettingsPageCommand => new RelayCommand(ChatSettingsPageNavigate);

        private void PrivacyPageNavigate()
        {
            ViewModel.NavigationService.Navigate(typeof(Settings.PrivacySettingsPage));
        }

        private void ChatSettingsPageNavigate()
        {
            ViewModel.NavigationService.Navigate(typeof(Settings.ChatSettingsPage));
        }

        private void NotifcationPageNavigate()
        {
            ViewModel.NavigationService.Navigate(typeof(Settings.NotificationsSettingsPage));
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame.BackStack.Clear();
            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Settings");
            }
            ViewModel.NavigationService = MasterDetail.NavigationService;
        }

        private void ClearNavigation()
        {
            while (ViewModel.NavigationService.Frame.BackStackDepth > 1)
            {
                ViewModel.NavigationService.Frame.BackStack.RemoveAt(1);
            }

            if (ViewModel.NavigationService.CanGoBack)
            {
                ViewModel.NavigationService.GoBack();
                ViewModel.NavigationService.Frame.ForwardStack.Clear();
            }
        }

    }
}
