//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class SharedFileCell : Grid
    {
        private IMessageDelegate _delegate;
        private MessageWithOwner _message;

        private long _fileToken;

        public SharedFileCell()
        {
            InitializeComponent();
        }

        public void UpdateFileDownload(DownloadsViewModel viewModel, FileDownloadViewModel fileDownload)
        {
            if (fileDownload == null)
            {
                return;
            }

            UpdateMessage(null, new MessageWithOwner(viewModel.ClientService, fileDownload.Message));
        }

        public void UpdateMessage(IMessageDelegate delegato, MessageWithOwner message)
        {
            _delegate = delegato;
            _message = message;

            var data = message.GetFileAndThumbnailAndName();
            if (data.File == null)
            {
                return;
            }

            Ellipse.Background = UpdateEllipseBrush(data.FileName);

            if (string.IsNullOrEmpty(data.FileName))
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User user))
                {
                    Title.Text = user.FullName();
                }
                else if (message.ClientService.TryGetChat(message.SenderId, out Chat chat))
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

            UpdateManager.Subscribe(this, message, data.File, ref _fileToken, UpdateFile);
            UpdateFile(message, data.File);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageWithOwner message, File file)
        {
            var data = message.GetFileAndThumbnailAndName();
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

        private void UpdateThumbnail(MessageWithOwner message, Thumbnail thumbnail, File file)
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
                    Texture.Background = new ImageBrush { ImageSource = UriEx.ToBitmap(file.Local.Path, width, height), Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
                    Button.Style = BootStrapper.Current.Resources["ImmersiveFileButtonStyle"] as Style;
                }
                catch
                {
                    Texture.Background = null;
                    Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
                }
            }
            else
            {
                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    message.ClientService.DownloadFile(file.Id, 1);
                }
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

        private string UpdateTimeLabel(MessageWithOwner message)
        {
            return Formatter.BannedUntil(message.Date);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var file = _message.GetFile();
            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingActive)
            {
                if (_delegate != null)
                {
                    _message.ClientService.CancelDownloadFile(file);
                }
                else
                {
                    _message.ClientService.Send(new ToggleDownloadIsPaused(file.Id, true));
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (_delegate != null)
                {
                    if (_message.CanBeAddedToDownloads)
                    {
                        _message.ClientService.AddFileToDownloads(file, _message.ChatId, _message.Id);
                    }
                    else
                    {
                        _message.ClientService.DownloadFile(file.Id, 30);
                    }
                }
                else
                {
                    _message.ClientService.Send(new ToggleDownloadIsPaused(file.Id, false));
                }
            }
            else if (_delegate == null)
            {
                // TODO: Replace with IStorageService.OpenFileAsync

                var permanent = await _message.ClientService.GetPermanentFileAsync(file);
                if (permanent != null)
                {
                    await Windows.System.Launcher.LaunchFileAsync(permanent);
                }
            }
            else
            {
                _delegate.OpenFile(file);
            }
        }
    }
}
