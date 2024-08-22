//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages.Content
{
    // TODO: turn the whole control into a Button
    public sealed class DocumentContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;
        private long _thumbnailToken;

        public DocumentContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(DocumentContent);
        }

        #region InitializeComponent

        private AutomaticDragHelper ButtonDrag;

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

            ButtonDrag = new AutomaticDragHelper(Button, true);
            ButtonDrag.StartDetectingDrag();

            Button.Click += Button_Click;
            Button.DragStarting += Button_DragStarting;

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

            var document = GetContent(message);
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
                UpdateThumbnail(null);
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
            var document = GetContent(message);
            if (document == null || !_templateApplied)
            {
                return;
            }

            if (document.DocumentValue.Id != file.Id)
            {
                return;
            }

            var canBeDownloaded = file.Local.CanBeDownloaded
                && !file.Local.IsDownloadingCompleted
                && !file.Local.IsDownloadingActive;

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive || (canBeDownloaded && message.Delegate.CanBeDownloaded(document, file)))
            {
                if (canBeDownloaded)
                {
                    _message.ClientService.DownloadFile(file.Id, 32);
                }

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
            else if (canBeDownloaded)
            {
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                Subtitle.Text = FileSizeConverter.Convert(size);
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
            var document = GetContent(_message);
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

                UpdateThumbnail(UriEx.ToBitmap(file.Local.Path, width, height));
            }
            else
            {
                UpdateThumbnail(null);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    message.ClientService.DownloadFile(file.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(BitmapImage imageSource)
        {
            if (Texture.Background is ImageBrush imageBrush && imageSource != null)
            {
                imageBrush.ImageSource = imageSource;
            }
            else if (imageSource != null)
            {
                Button.Background = new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00));
                Texture.Background = new ImageBrush
                {
                    ImageSource = imageSource,
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
            }
            else
            {
                Button.Background = null;
                Texture.Background = null;
            }
        }

        public void Recycle()
        {
            _message = null;

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken, true);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageDocument)
            {
                return true;
            }
            else if (content is MessageText text && text.LinkPreview != null && !primary)
            {
                return text.LinkPreview.Type is LinkPreviewTypeDocument;
            }

            return false;
        }

        private Document GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            if (content is MessageDocument document)
            {
                return document.Document;
            }
            else if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeDocument previewDocument)
            {
                return previewDocument.Document;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var document = GetContent(_message);
            if (document == null)
            {
                return;
            }

            var file = document.DocumentValue;
            if (file.Local.IsDownloadingActive)
            {
                _message.ClientService.CancelDownloadFile(file);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                if (_message.SendingState is MessageSendingStateFailed or MessageSendingStatePending)
                {
                    _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
                }
                else
                {
                    _message.ClientService.Send(new CancelPreliminaryUploadFile(file.Id));
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
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
                _message.Delegate.OpenFile(file);
            }
        }

        private void Button_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            MessageHelper.DragStarting(_message, args);
        }
    }
}
