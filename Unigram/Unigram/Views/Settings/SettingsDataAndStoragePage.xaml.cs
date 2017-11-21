using libtgvoip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
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
            dialog.IsProxyEnabled = SettingsHelper.IsProxyEnabled;
            dialog.IsCallsProxyEnabled = SettingsHelper.IsCallsProxyEnabled;

            var enabled = SettingsHelper.IsProxyEnabled == true;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                SettingsHelper.ProxyServer = dialog.Server;
                SettingsHelper.ProxyPort = Extensions.TryParseOrDefault(dialog.Port, 1080);
                SettingsHelper.ProxyUsername = dialog.Username;
                SettingsHelper.ProxyPassword = dialog.Password;
                SettingsHelper.IsProxyEnabled = dialog.IsProxyEnabled;
                SettingsHelper.IsCallsProxyEnabled = dialog.IsCallsProxyEnabled;

                if (SettingsHelper.IsProxyEnabled || SettingsHelper.IsProxyEnabled != enabled)
                {
                    UnigramContainer.Current.ResolveType<IMTProtoService>().ToggleProxy();
                }
            }
        }

        #region Binding

        private string ConvertUseLessData(DataSavingMode value)
        {
            switch (value)
            {
                default:
                case DataSavingMode.Never:
                    return Strings.Android.UseLessDataNever;
                case DataSavingMode.MobileOnly:
                    return Strings.Android.UseLessDataOnMobile;
                case DataSavingMode.Always:
                    return Strings.Android.UseLessDataAlways;
            }
        }

        #endregion

    }
}
