using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Unigram.Common;
using Unigram.Controls.Views;
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
    public sealed partial class SettingsDataAndStoragePage : Page
    {
        public SettingsDataAndStorageViewModel ViewModel => DataContext as SettingsDataAndStorageViewModel;

        public SettingsDataAndStoragePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsDataAndStorageViewModel>();
        }

        private void Storage_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStoragePage));
        }

        private void Stats_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStatsPage));
        }

        private async void Proxy_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProxyView();
            dialog.Server = SettingsHelper.ProxyServer;
            dialog.Port = SettingsHelper.ProxyPort.ToString();
            dialog.Username = SettingsHelper.ProxyUsername;
            dialog.Password = SettingsHelper.ProxyPassword;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                SettingsHelper.ProxyServer = dialog.Server;
                SettingsHelper.ProxyPort = int.Parse(dialog.Port);
                SettingsHelper.ProxyUsername = dialog.Username;
                SettingsHelper.ProxyPassword = dialog.Password;
            }
        }
    }
}
