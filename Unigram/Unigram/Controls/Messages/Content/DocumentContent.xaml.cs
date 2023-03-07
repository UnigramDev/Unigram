//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.Storage;

namespace Unigram.Controls.Messages.Content
{
    public sealed class DocumentContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private string _fileToken;
        private string _thumbnailToken;

        public DocumentContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(DocumentContent);
        }

        #region InitializeComponent

        private Border Texture;
        private FileButton Button;
        private TextBlock Title;
        private TextBlock Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Texture = GetTemplateChild(nameof(Texture)) as Border;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            Title = GetTemplateChild(nameof(Title)) as TextBlock;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;

            Button.Click += Button_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var document = GetContent(message.Content);
            if (document == null || !_templateApplied)
            {
                return;
            }

            Title.Text = document.FileName;

            if (document.Thumbnail?.File.Id != null)
            {
                UpdateManager.Subscribe(this, message, document.Thumbnail.File, ref _thumbnailToken, UpdateThumbnail, true);
                UpdateThumbnail(message, document.Thumbnail, document.Thumbnail.File);
            }
            else
            {
                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }

            UpdateManager.Subscribe(this, message, document.DocumentValue, ref _fileToken, UpdateFile);
            UpdateFile(message, document.DocumentValue);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var document = GetContent(message.Content);
            if (document == null || !_templateApplied)
            {
                return;
            }

            if (document.DocumentValue.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                Subtitle.Text = FileSizeConverter.Convert(size);

                if (message.Delegate.CanBeDownloaded(document, file))
                {
                    _message.ClientService.DownloadFile(file.Id, 32);
                }
            }
            else
            {
                var theme = document.FileName.EndsWith(".unigram-theme");
                if (theme)
                {
                    Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Theme);
                }
                else
                {
                    Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Document);
                }
                Button.Progress = 1;

                Subtitle.Text = FileSizeConverter.Convert(size);
            }
        }

        private void UpdateThumbnail(object target, File file)
        {
            var document = GetContent(_message.Content);
            if (document == null || !_templateApplied)
            {
                return;
            }

            UpdateThumbnail(_message, document.Thumbnail, file);
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail, File file)
        {
            if (thumbnail == null || thumbnail.File.Id != file.Id)
            {
                return;
            }

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
                message.ClientService.DownloadFile(file.Id, 1);

                Texture.Background = null;
                Button.Style = BootStrapper.Current.Resources["InlineFileButtonStyle"] as Style;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageDocument)
            {
                return true;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Document != null && !string.Equals(text.WebPage.Type, "telegram_background", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private Document GetContent(MessageContent content)
        {
            if (content is MessageDocument document)
            {
                return document.Document;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Document;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var document = GetContent(_message.Content);
            if (document == null)
            {
                return;
            }

            var file = document.DocumentValue;
            if (file.Local.IsDownloadingActive)
            {
                _message.ClientService.CancelDownloadFile(file.Id);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (_message.Content is not MessageDocument)
                {
                    _message.ClientService.DownloadFile(file.Id, 30);
                }
                else
                {
                    _message.ClientService.AddFileToDownloads(file.Id, _message.ChatId, _message.Id);
                }
            }
            else
            {
                _message.Delegate.OpenFile(file);
            }
        }

        private async void Button_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            var document = GetContent(_message.Content);
            if (document == null)
            {
                return;
            }

            var file = document.DocumentValue;
            if (file.Local.IsDownloadingCompleted)
            {
                var item = await StorageFile.GetFileFromPathAsync(file.Local.Path);

                args.AllowedOperations = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                args.Data.SetStorageItems(new[] { item });
                args.DragUI.SetContentFromDataPackage();
            }
        }
    }
}
