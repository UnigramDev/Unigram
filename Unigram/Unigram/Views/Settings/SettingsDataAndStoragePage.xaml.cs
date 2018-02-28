using libtgvoip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
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

        #region Binding

        private string ConvertUseLessData(DataSavingMode value)
        {
            switch (value)
            {
                default:
                case DataSavingMode.Never:
                    return Strings.Resources.UseLessDataNever;
                case DataSavingMode.MobileOnly:
                    return Strings.Resources.UseLessDataOnMobile;
                case DataSavingMode.Always:
                    return Strings.Resources.UseLessDataAlways;
            }
        }

        #endregion

    }
}
