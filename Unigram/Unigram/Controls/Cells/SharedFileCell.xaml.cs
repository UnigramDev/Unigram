using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Messages.Content;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Cells
{
    public sealed partial class SharedFileCell : UserControl
    {
        private IProtoService _protoService;
        private IMessageDelegate _delegate;
        private Message _message;

        public SharedFileCell()
        {
            InitializeComponent();
        }

        public void UpdateMessage(IProtoService protoService, IMessageDelegate delegato, Message message)
        {
            _protoService = protoService;
            _delegate = delegato;

            _message = message;

            var data = message.GetFileAndThumbnailAndName(false);
            if (data.File == null)
            {
                return;
            }

            Ellipse.Background = UpdateEllipseBrush(data.FileName);

            if (string.IsNullOrEmpty(data.FileName))
            {
                if (_protoService.TryGetUser(message.SenderId, out User user))
                {
                    Title.Text = user.GetFullName();
                }
                else if (_protoService.TryGetChat(message.SenderId, out Chat chat))
                {
                    Title.Text = chat.Title;
                }
                else
                {
                    Title.Text = string.Empty;
                }
            }
            else
            {
                Title.Text = data.FileName;
            }

            if (data.Thumbnail != null)
            {
                UpdateThumbnail(message, data.Thumbnail, data.Thumbnail.File);
            }
            else
            {
                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }

            UpdateFile(message, data.File);
        }

        public void UpdateFile(Message message, File file)
        {
            var data = message.GetFileAndThumbnailAndName(false);
            if (data.File == null)
            {
                return;
            }

            if (data.Thumbnail != null && data.Thumbnail.File.Id == file.Id)
            {
                UpdateThumbnail(message, data.Thumbnail, file);
                return;
            }
            else if (data.File.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Remote.IsUploadingActive)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                //Button.Glyph = Icons.Download;
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                Subtitle.Text = FileSizeConverter.Convert(size) + " — " + UpdateTimeLabel(message);
            }
            else
            {
                //Button.Glyph = Icons.Document;
                Button.SetGlyph(file.Id, MessageContentState.Document);
                Button.Progress = 1;

                Subtitle.Text = FileSizeConverter.Convert(size) + " — " + UpdateTimeLabel(message);
            }
        }

        private void UpdateThumbnail(Message message, Thumbnail thumbnail, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                double ratioX = (double)48 / thumbnail.Width;
                double ratioY = (double)48 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                try
                {
                    Texture.Background = new ImageBrush { ImageSource = new BitmapImage(UriEx.ToLocal(file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height }, Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
                    Button.Style = BootStrapper.Current.Resources["ImmersiveFileButtonStyle"] as Style;
                }
                catch
                {
                    Texture.Background = null;
                    Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                _protoService.DownloadFile(file.Id, 1);

                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }
        }

        private Brush UpdateEllipseBrush(string name)
        {
            var brushes = new[]
            {
                BootStrapper.Current.Resources["Placeholder0Brush"],
                BootStrapper.Current.Resources["Placeholder1Brush"],
                BootStrapper.Current.Resources["Placeholder2Brush"],
                BootStrapper.Current.Resources["Placeholder3Brush"]
            };

            if (name == null)
            {
                return brushes[0] as SolidColorBrush;
            }

            if (name.Length > 0)
            {
                int color;
                if (name.EndsWith(".doc") || name.EndsWith(".txt") || name.EndsWith(".psd"))
                {
                    color = 0;
                }
                else if (name.EndsWith(".xls") || name.EndsWith(".csv"))
                {
                    color = 1;
                }
                else if (name.EndsWith(".pdf") || name.EndsWith(".ppt") || name.EndsWith(".key"))
                {
                    color = 2;
                }
                else if (name.EndsWith(".zip") || name.EndsWith(".rar") || name.EndsWith(".ai") || name.EndsWith(".mp3") || name.EndsWith(".mov") || name.EndsWith(".avi"))
                {
                    color = 3;
                }
                else
                {
                    int idx;
                    var extension = (idx = name.LastIndexOf(".", StringComparison.Ordinal)) == -1 ? string.Empty : name.Substring(idx + 1);
                    if (extension.Length != 0)
                    {
                        color = extension[0] % brushes.Length;
                    }
                    else
                    {
                        color = name[0] % brushes.Length;
                    }
                }

                return brushes[color] as SolidColorBrush;
            }

            return brushes[0] as SolidColorBrush;
        }

        private string UpdateTimeLabel(Message message)
        {
            return Converter.BannedUntil(message.Date);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var file = _message.GetFile();
            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingActive)
            {
                _protoService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _protoService.DownloadFile(file.Id, 32);
            }
            else
            {
                _delegate.OpenFile(file);
            }
        }
    }
}
