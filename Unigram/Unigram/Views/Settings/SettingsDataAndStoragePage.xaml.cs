//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Text;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services.Settings;
using Unigram.ViewModels.Settings;
using Unigram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsDataAndStoragePage : HostedPage
    {
        public SettingsDataAndStorageViewModel ViewModel => DataContext as SettingsDataAndStorageViewModel;

        public SettingsDataAndStoragePage()
        {
            InitializeComponent();
            Title = Strings.Resources.DataSettings;
        }

        private void Storage_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStoragePage));
        }

        private void Stats_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsNetworkPage));
        }

        private async void Downloads_Click(object sender, RoutedEventArgs e)
        {
            await new DownloadsPopup(ViewModel.SessionId, ViewModel.NavigationService).ShowQueuedAsync();
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
                return Strings.Resources.Default;
            }

            return path;
        }

        private Visibility ConvertFilesReset(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        private string ConvertAutoDownload(AutoDownloadType type, AutoDownloadMode mode, long limit)
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
                            builder.Append(Strings.Resources.AutodownloadContacts);
                            break;
                        case 1:
                            builder.Append(Strings.Resources.AutoDownloadPm);
                            break;
                        case 2:
                            builder.Append(Strings.Resources.AutoDownloadGroups);
                            break;
                        case 3:
                            builder.Append(Strings.Resources.AutodownloadChannels);
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

        #endregion
    }
}
