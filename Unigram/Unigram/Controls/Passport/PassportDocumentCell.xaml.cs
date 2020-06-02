using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Passport
{
    public sealed partial class PassportDocumentCell : UserControl
    {
        public PassportDocumentCell()
        {
            InitializeComponent();
        }

        public void UpdateFile(IProtoService protoService, DatedFile file)
        {
            var date = Utils.UnixTimestampToDateTime(file.Date);
            var format = string.Format(Strings.Resources.formatDateAtTime, BindConvert.Current.ShortDate.Format(date), BindConvert.Current.ShortTime.Format(date));
            Date.Text = format;
            Texture.Source = null;

            UpdateFile(protoService, file.File);
        }

        public void UpdateFile(IProtoService protoService, File file)
        {
            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Transfer.Value = (double)file.Local.DownloadedSize / size;
                Transfer.Opacity = 1;
            }
            else if (file.Remote.IsUploadingActive)
            {
                Transfer.Value = (double)file.Remote.UploadedSize / size;
                Transfer.Opacity = 1;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Transfer.Value = 0;
                Transfer.Opacity = 0;

                protoService.DownloadFile(file.Id, 32);
            }
            else
            {
                Transfer.Value = 1;
                Transfer.Opacity = 0;

                Texture.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
            }
        }
    }
}
