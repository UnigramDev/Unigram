//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages.Content
{
    public sealed class PhotoContent : Control, IContentWithFile
    {
        private readonly bool _album;

        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;
        private long _thumbnailToken;

        private bool _hidden = true;

        public PhotoContent(MessageViewModel message, bool album = false)
        {
            _message = message;
            _album = album;

            DefaultStyleKey = typeof(PhotoContent);
        }

        public PhotoContent()
        {
            DefaultStyleKey = typeof(PhotoContent);
        }

        #region InitializeComponent

        private AspectView LayoutRoot;
        private Image Texture;
        private Border Overlay;
        private TextBlock Subtitle;
        private FileButton Button;
        private SelfDestructTimer Timer;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            Texture = GetTemplateChild(nameof(Texture)) as Image;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            Timer = GetTemplateChild(nameof(Timer)) as SelfDestructTimer;

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
            var prevId = _message?.Id;
            var nextId = message?.Id;

            _message = message;

            var photo = GetContent(message, out bool hasSpoiler, out bool isSecret);
            if (photo == null || !_templateApplied)
            {
                _hidden = (prevId != nextId || _hidden) && hasSpoiler;
                return;
            }

            _hidden = (prevId != nextId || _hidden) && hasSpoiler;

            LayoutRoot.Constraint = message;
            LayoutRoot.Background = null;
            Texture.Source = null;
            Texture.Stretch = _album
                ? Stretch.UniformToFill
                : Stretch.Uniform;

            //UpdateMessageContentOpened(message);

            var small = photo.GetSmall()?.Photo;
            var big = photo.GetBig();

            if (small == null || big == null)
            {
                return;
            }

            if (!big.Photo.Local.IsDownloadingCompleted || isSecret)
            {
                UpdateThumbnail(message, small, photo.Minithumbnail, true, isSecret);
            }
            else
            {
                UpdateThumbnail(message, null, photo.Minithumbnail, false, isSecret);
            }

            UpdateManager.Subscribe(this, message, big.Photo, ref _fileToken, UpdateFile);
            UpdateFile(message, big.Photo);
        }

        public void Mockup(MessagePhoto photo)
        {
            var big = photo.Photo.GetBig();

            LayoutRoot.Constraint = photo;
            LayoutRoot.Background = null;
            Texture.Source = new BitmapImage(new Uri(big.Photo.Local.Path));

            Overlay.Opacity = 0;
            Button.Opacity = 0;
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.SelfDestructTime > 0 && _templateApplied)
            {
                Timer.Maximum = message.SelfDestructTime;
                Timer.Value = DateTime.Now.AddSeconds(message.SelfDestructIn);
            }
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var photo = GetContent(message, out bool hasSpoiler, out bool isSecret);
            if (photo == null || !_templateApplied)
            {
                return;
            }

            var big = photo.GetBig();
            if (big == null || big.Photo.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Button.Opacity = 1;
                Overlay.Opacity = 0;
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Button.Opacity = 1;
                Overlay.Opacity = 0;

                if (isSecret)
                {
                    Texture.Source = null;
                }
                else
                {
                    UpdateTexture(message, big, file);
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                //Button.Glyph = Icons.Download;
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                Button.Opacity = 1;
                Overlay.Opacity = 0;

                if (message.Delegate.CanBeDownloaded(photo, file))
                {
                    _message.ClientService.DownloadFile(file.Id, 32);
                }
            }
            else
            {
                if (isSecret)
                {
                    //Button.Glyph = Icons.Ttl;
                    Button.SetGlyph(file.Id, MessageContentState.Ttl);
                    Button.Progress = 1;

                    Button.Opacity = 1;
                    Overlay.Opacity = 1;

                    Texture.Source = null;
                    Subtitle.Text = Locale.FormatTtl(message.SelfDestructTime, true);
                }
                else
                {
                    //Button.Glyph = message.SendingState is MessageSendingStatePending ? Icons.Confirm : Icons.Play;
                    Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Play);
                    Button.Progress = 1;

                    if (message.Content is MessageText text && text.WebPage?.EmbedUrl?.Length > 0 || (message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0))
                    {
                        Button.Opacity = 1;
                    }
                    else
                    {
                        Button.Opacity = 0;
                    }

                    Overlay.Opacity = 0;

                    if (hasSpoiler && _hidden)
                    {
                        Texture.Source = null;
                    }
                    else
                    {
                        UpdateTexture(message, big, file);
                    }
                }
            }
        }

        private void UpdateTexture(MessageViewModel message, PhotoSize big, File file)
        {
            var width = 0;
            var height = 0;

            if (width > MaxWidth || height > MaxHeight)
            {
                double ratioX = MaxWidth / big.Width;
                double ratioY = MaxHeight / big.Height;
                double ratio = Math.Max(ratioX, ratioY);

                width = (int)(big.Width * ratio);
                height = (int)(big.Height * ratio);
            }

            Texture.Source = UriEx.ToBitmap(file.Local.Path, width, height);
        }

        private void UpdateThumbnail(object target, File file)
        {
            var photo = GetContent(_message, out _, out bool isSecret);
            if (photo == null || !_templateApplied)
            {
                return;
            }

            UpdateThumbnail(_message, file, photo.Minithumbnail, false, isSecret);
        }

        private async void UpdateThumbnail(MessageViewModel message, File file, Minithumbnail minithumbnail, bool download, bool isSecret)
        {
            ImageSource source = null;
            ImageBrush brush;

            if (LayoutRoot.Background is ImageBrush existing)
            {
                brush = existing;
            }
            else
            {
                brush = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                LayoutRoot.Background = brush;
            }

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    source = await PlaceholderHelper.GetBlurredAsync(file.Local.Path, isSecret ? 15 : 3);
                }
                else
                {
                    if (download)
                    {
                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            message.ClientService.DownloadFile(file.Id, 1);
                        }

                        UpdateManager.Subscribe(this, message, file, ref _thumbnailToken, UpdateThumbnail, true);
                    }

                    if (minithumbnail != null)
                    {
                        source = await PlaceholderHelper.GetBlurredAsync(minithumbnail.Data, isSecret ? 15 : 3);
                    }
                }
            }
            else if (minithumbnail != null)
            {
                source = await PlaceholderHelper.GetBlurredAsync(minithumbnail.Data, isSecret ? 15 : 3);
            }

            brush.ImageSource = source;
        }

        public void Recycle()
        {
            _message = null;

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken, true);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessagePhoto)
            {
                return true;
            }
            else if (content is MessageGame game && !primary)
            {
                return game.Game.Photo != null;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.IsPhoto();
            }
            else if (content is MessageInvoice invoice && invoice.ExtendedMedia is MessageExtendedMediaPhoto)
            {
                return true;
            }

            return false;
        }

        private Photo GetContent(MessageViewModel message, out bool hasSpoiler, out bool isSecret)
        {
            if (message?.Delegate == null)
            {
                hasSpoiler = false;
                isSecret = false;
                return null;
            }

            var content = message.GeneratedContent ?? message.Content;
            if (content is MessagePhoto photo)
            {
                hasSpoiler = photo.HasSpoiler;
                isSecret = photo.IsSecret;
                return photo.Photo;
            }
            else if (content is MessageGame game)
            {
                hasSpoiler = false;
                isSecret = false;
                return game.Game.Photo;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                hasSpoiler = false;
                isSecret = false;
                return text.WebPage.Photo;
            }
            else if (content is MessageInvoice invoice && invoice.ExtendedMedia is MessageExtendedMediaPhoto extendedMediaPhoto)
            {
                hasSpoiler = false;
                isSecret = false;
                return extendedMediaPhoto.Photo;
            }

            hasSpoiler = false;
            isSecret = false;
            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var photo = GetContent(_message, out bool hasSpoiler, out _);
            if (photo == null)
            {
                return;
            }

            if (hasSpoiler && _hidden)
            {
                _hidden = false;
                UpdateMessage(_message);

                return;
            }

            var big = photo.GetBig();
            if (big == null)
            {
                if (_message?.SendingState is MessageSendingStateFailed)
                {
                    _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
                }

                return;
            }

            var file = big.Photo;
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
                _message.ClientService.DownloadFile(file.Id, 30);
            }
            else
            {
                if (_message.Content is MessageText text && text.WebPage?.EmbedUrl?.Length > 0)
                {
                    _message.Delegate.OpenUrl(text.WebPage.Url, false);
                    //await EmbedUrlView.GetForCurrentView().ShowAsync(_message, text.WebPage, () => this);
                }
                else
                {
                    _message.Delegate.OpenMedia(_message, this);
                }
            }
        }
    }
}
