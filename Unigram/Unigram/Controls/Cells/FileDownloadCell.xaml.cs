using Unigram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Cells
{
    public sealed partial class FileDownloadCell : UserControl
    {
        public FileDownloadCell()
        {
            InitializeComponent();
        }

        public bool IsFirst
        {
            set => UpdateFirst(value);
        }

        private void UpdateFirst(bool value)
        {
            if (value && Header == null)
            {
                FindName(nameof(Header));
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
