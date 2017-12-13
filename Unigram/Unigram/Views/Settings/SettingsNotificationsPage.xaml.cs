using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsNotificationsPage : Page
    {
        public SettingsNotificationsViewModel ViewModel => DataContext as SettingsNotificationsViewModel;

        public SettingsNotificationsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsNotificationsViewModel>();
        }

        private async void Private_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PrivateAlert = PrivateAlert.IsChecked == true;
            ViewModel.PrivatePreview = PrivatePreview.IsChecked == true;

            await ViewModel.UpdatePrivateAsync();
        }

        private async void Group_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GroupAlert = GroupPreview.IsChecked == true;
            ViewModel.GroupPreview = GroupPreview.IsChecked == true;

            await ViewModel.UpdateGroupAsync();
        }
    }
}
