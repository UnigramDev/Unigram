//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class FileDownloadCell : UserControl
    {
        public FileDownloadCell()
        {
            InitializeComponent();
        }

        private bool _first;
        public bool IsFirst
        {
            set => UpdateFirst(value, _completeDate);
        }

        private int _completeDate;
        public int CompleteDate
        {
            set => UpdateFirst(_first, value);
        }

        private void UpdateFirst(bool value, int completeDate)
        {
            _first = value;
            _completeDate = completeDate;

            if (value)
            {
                if (Header == null)
                {
                    FindName(nameof(Header));
                }

                Header.Text = completeDate != 0 ? Strings.RecentlyDownloaded : Strings.Downloading;
            }
            else if (value is false && Header != null)
            {
                UnloadObject(Header);
            }
        }

        public void UpdateFileDownload(DownloadsViewModel viewModel, FileDownloadViewModel fileDownload)
        {
            SharedFile.UpdateFileDownload(viewModel, fileDownload);
        }
    }
}
