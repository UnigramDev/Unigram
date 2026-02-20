//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class SharedFileCell : Grid
    {
        private MediaTabsViewModelBase _viewModel;
        private MessageWithOwner _message;

        private long _fileToken;
        private long _thumbnailToken;

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

        private bool _hidden;

        public void Hide()
        {
            if (_hidden)
            {
                return;
            }

            _hidden = true;
            ButtonRoot.Opacity = 0;
            TextRoot.Opacity = 0;
        }

        public void UpdateMessage(MediaTabsViewModelBase viewModel, MessageWithOwner message)
        {
            if (_hidden)
            {
                _hidden = false;
                ButtonRoot.Opacity = 1;
                TextRoot.Opacity = 1;
            }

            _viewModel = viewModel;
            _message = message;

            var data = message.GetFileAndThumbnailAndName();
            if (data.File == null)
            {
                return;
            }

            ButtonRoot.Background = UpdateEllipseBrush(data.FileName);

            if (string.IsNullOrEmpty(data.FileName))
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User user))
                {
                    Title.Text = user.FullName();
                    TitleTrim.Text = string.Empty;
                }
                else if (message.ClientService.TryGetChat(message.SenderId, out Chat chat))
                {
                    Title.Text = chat.Title;
                    TitleTrim.Text = string.Empty;
                }
                else
                {
                    Title.Text = string.Empty;
                    TitleTrim.Text = string.Empty;
                }
            }
            else
            {
                var index = data.FileName.LastIndexOf('.');
                if (index > 0)
                {
                    Title.Text = data.FileName.Substring(0, index + 1);
                    TitleTrim.Text = data.FileName.Substring(index + 1);
                }
                else
                {
                    Title.Text = data.FileName;
                    TitleTrim.Text = string.Empty;
                }
            }

            if (data.Thumbnail != null)
            {
                UpdateThumbnail(message, data.Thumbnail, data.Thumbnail.File, true);
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
                UpdateThumbnail(message, data.Thumbnail, file, false);
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

        private void UpdateThumbnail(MessageWithOwner message, Thumbnail thumbnail, File file, bool download)
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

                if (download)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        message.ClientService.DownloadFile(file.Id, 1);
                    }

                    UpdateManager.Subscribe(this, message, file, ref _thumbnailToken, UpdateFile, true);
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
                if (_viewModel != null)
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
                if (_viewModel != null)
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
            else if (_viewModel == null)
            {
                // TODO: I don't like retrieving services this way
                var service = _message.ClientService.Session.Resolve<IStorageService>();
                if (service != null)
                {
                    _ = service.OpenFileAsync(file);
                }
            }
            else if (_message.Content is MessageDocument document && document.IsPhoto())
            {
                var response = await _message.ClientService.SendAsync(new GetMessageProperties(_message.ChatId, _message.Id));
                if (response is not MessageProperties properties)
                {
                    return;
                }

                var storageService = _message.ClientService.Session.Resolve<IStorageService>();
                var viewModel = new ChatGalleryViewModel(_message.ClientService, storageService, _viewModel.Aggregator, _message.ChatId, _viewModel.Topic, _message, properties);
                _viewModel.NavigationService.ShowGallery(viewModel, Texture);
            }
            else
            {
                _viewModel.MessageDelegate.OpenFile(file);
            }
        }
    }
}
