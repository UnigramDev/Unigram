using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Transport;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsGeneralPage : Page
    {
        public SettingsGeneralViewModel ViewModel => DataContext as SettingsGeneralViewModel;

        public SettingsGeneralPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsGeneralViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Socks5.Content = "ToggleSocks5: " + SettingsHelper.IsProxyEnabled;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SettingsHelper.IsProxyEnabled = !SettingsHelper.IsProxyEnabled;
            UnigramContainer.Current.ResolveType<ITransportService>().Close();
            UnigramContainer.Current.ResolveType<IMTProtoService>().UpdateStatusAsync(false, null);
            Socks5.Content = "ToggleSocks5: " + SettingsHelper.IsProxyEnabled;
        }
    }
}
