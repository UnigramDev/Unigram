//
// Copyright (c) Fela Ameghino 2015-2026
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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    // TODO: turn the whole control into a Button
    public sealed partial class DocumentContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;
        private long _thumbnailToken;

        private ThumbnailController _thumbnailController;

        // The scrim under the button, needed only once the thumbnail is behind it.
        private SolidColorBrush _scrim;

        public DocumentContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(DocumentContent);
            Telegram.Common.Instrumentation.Register(this);
        }

        #region InitializeComponent

        private AutomaticDragHelper ButtonDrag;

        private Border Texture;
        private ImageBrush ThumbnailTexture;
        private FileButton Button;
        private TextBlock Title;
        private TextBlock TitleTrim;
        private TextBlock Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Texture = GetTemplateChild(nameof(Texture)) as Border;
            ThumbnailTexture = Texture.Background as ImageBrush;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            Title = GetTemplateChild(nameof(Title)) as TextBlock;
            TitleTrim = GetTemplateChild(nameof(TitleTrim)) as TextBlock;
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

            var index = document.FileName.LastIndexOf('.');
            if (index > 0)
            {
                Title.Text = document.FileName.Substring(0, index + 1);
                TitleTrim.Text = document.FileName.Substring(index + 1);
            }
            else
            {
                Title.Text = document.FileName;
                TitleTrim.Text = string.Empty;
            }

            if (document.Thumbnail != null)
            {
                UpdateManager.Subscribe(this, message, document.Thumbnail.File, ref _thumbnailToken, UpdateThumbnail, true);
                UpdateThumbnail(message, document.Thumbnail, document.Thumbnail.File);
            }
            else
            {
                UpdateThumbnail(message, null, null);
            }

            UpdateManager.Subscribe(this, message, document.DocumentValue, ref _fileToken, UpdateFile);
            UpdateFile(message, document.DocumentValue);
        }

        private void UpdateFile(File file)
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

            var size = Math.Max(file.Size, file.ExpectedSize);
            var state = file.GetFileState(message, document);

            if (state == MessageContentState.Downloading)
            {
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (state == MessageContentState.Uploading)
            {
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
            }
            else if (state == MessageContentState.Download)
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

        private void UpdateThumbnail(File file)
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
            // No thumbnail at all, rather than one that has yet to arrive: whatever the
            // control drew for the message before this one has to go.
            if (thumbnail == null)
            {
                _thumbnailController?.Recycle();
                Button.Background = null;
                return;
            }

            if (thumbnail.File.Id != file.Id)
            {
                return;
            }

            _thumbnailController ??= new ThumbnailController(ThumbnailTexture);

            if (file.Local.IsDownloadingCompleted)
            {
                double ratioX = (double)48 / thumbnail.Width;
                double ratioY = (double)48 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                _thumbnailController.Bitmap(file.Local.Path, width, height, HashCode.Combine(message.ChatId, message.Id));
                Button.Background = _scrim ??= new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00));
            }
            else
            {
                _thumbnailController.Recycle();
                Button.Background = null;

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    message.ClientService.DownloadFile(file.Id, 1);
                }
            }
        }

        public void Recycle()
        {
            _message = null;
            _thumbnailController?.Recycle();

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken);

            if (_templateApplied)
            {
                Button.Background = null;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content switch
            {
                MessageDocument => true,
                MessageText text when text.LinkPreview != null && !primary => text.LinkPreview.Type is LinkPreviewTypeDocument,
                MessagePoll poll when poll.Media is PollMediaDocument && !primary => true,
                _ => false,
            };
        }

        private Document GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            switch (content)
            {
                case MessageDocument document:
                    return document.Document;
                case MessageText text when text.LinkPreview?.Type is LinkPreviewTypeDocument previewDocument:
                    return previewDocument.Document;
                case MessagePoll poll when poll.Media is PollMediaDocument pollDocument:
                    return pollDocument.Document;
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
            var state = file.GetFileState(_message);

            if (state == MessageContentState.Downloading)
            {
                _message.ClientService.CancelDownloadFile(file);
            }
            else if (state == MessageContentState.Uploading)
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
            else if (state == MessageContentState.Download)
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
            else if (document.IsPhoto())
            {
                _message.Delegate.OpenMedia(_message, Texture);
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
