//
// Copyright Fela Ameghino & Contributors 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Text;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.ViewModels.Settings;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsDataAndStoragePage : HostedPage
    {
        public SettingsDataAndStorageViewModel ViewModel => DataContext as SettingsDataAndStorageViewModel;

        public SettingsDataAndStoragePage()
        {
            InitializeComponent();
            Title = Strings.DataSettings;
        }

        #region Binding

        private bool ConvertResetAutoDownload(bool isDefault)
        {
            return !isDefault;
        }

        private bool ConvertResetDownloadFolder(DownloadFolder path)
        {
            return path != null && path.IsCustom;
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
                            builder.Append(Strings.AutodownloadContacts);
                            break;
                        case 1:
                            builder.Append(Strings.AutoDownloadPm);
                            break;
                        case 2:
                            builder.Append(Strings.AutoDownloadGroups);
                            break;
                        case 3:
                            builder.Append(Strings.AutodownloadChannels);
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
                    builder.Append(Strings.AutoDownloadOnAllChats);
                }
                else
                {
                    builder.AppendFormat(Strings.AutoDownloadUpToOnAllChats, FileSizeConverter.Convert(limit, true));
                }
            }
            else if (count == 0)
            {
                builder.Append(Strings.AutoDownloadOff);
            }
            else
            {
                if (type == AutoDownloadType.Photos)
                {
                    builder = new StringBuilder(string.Format(Strings.AutoDownloadOnFor, builder.ToString()));
                }
                else
                {
                    builder = new StringBuilder(string.Format(Strings.AutoDownloadOnUpToFor, FileSizeConverter.Convert(limit, true), builder.ToString()));
                }
            }

            return builder.ToString();
        }

        #endregion
    }
}
