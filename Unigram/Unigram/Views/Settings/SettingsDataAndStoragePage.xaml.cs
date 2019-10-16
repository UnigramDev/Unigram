using libtgvoip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Services.Settings;
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
            DataContext = TLContainer.Current.Resolve<SettingsDataAndStorageViewModel>();
        }

        private void Storage_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStoragePage));
        }

        private void Stats_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsNetworkPage));
        }

        private void Proxy_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsProxiesPage));
        }

        #region Binding

        private string ConvertFilesDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "Default folder";
            }

            return path;
        }

        private string ConvertAutoDownload(AutoDownloadType type, AutoDownloadMode mode, int limit)
        {
            int count = 0;
            var builder = new StringBuilder();

            var mask = new int[4]
            {
                mode.HasFlag(AutoDownloadMode.WifiContacts) ? 0 : -1,
                mode.HasFlag(AutoDownloadMode.WifiPrivateChats) ? 1 : -1,
                mode.HasFlag(AutoDownloadMode.WifiGroups) ? 2 : -1,
                mode.HasFlag(AutoDownloadMode.WifiChannels) ? 3 : -1
            };

            for (int a = 0; a < mask.Length; a++)
            {
                if (mask[a] != -1)
                {
                    if (builder.Length != 0)
                    {
                        builder.Append(", ");
                    }
                    switch (a)
                    {
                        case 0:
                            builder.Append(Strings.Resources.AutoDownloadContacts);
                            break;
                        case 1:
                            builder.Append(Strings.Resources.AutoDownloadPm);
                            break;
                        case 2:
                            builder.Append(Strings.Resources.AutoDownloadGroups);
                            break;
                        case 3:
                            builder.Append(Strings.Resources.AutoDownloadChannels);
                            break;
                    }
                    count++;
                }
            }

            if (count == 4)
            {
                builder.Length = 0;

                if (type == AutoDownloadType.Photos)
                {
                    builder.Append(Strings.Resources.AutoDownloadOnAllChats);
                }
                else
                {
                    builder.AppendFormat(Strings.Resources.AutoDownloadUpToOnAllChats, FileSizeConverter.Convert(limit, true));
                }
            }
            else if (count == 0)
            {
                builder.Append(Strings.Resources.AutoDownloadOff);
            }
            else
            {
                if (type == AutoDownloadType.Photos)
                {
                    builder = new StringBuilder(string.Format(Strings.Resources.AutoDownloadOnFor, builder.ToString()));
                }
                else
                {
                    builder = new StringBuilder(string.Format(Strings.Resources.AutoDownloadOnUpToFor, FileSizeConverter.Convert(limit, true), builder.ToString()));
                }
            }

            return builder.ToString();
        }

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
