using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Messages.Content
{
    public sealed class PhotoContent : Control, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private string _fileToken;
        private string _thumbnailToken;

        public PhotoContent(MessageViewModel message)
        {
            _message = message;

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
            _message = message;

            var photo = GetContent(message.Content);
            if (photo == null || !_templateApplied)
            {
                return;
            }

            LayoutRoot.Constraint = message;
            LayoutRoot.Background = null;
            Texture.Source = null;

            //UpdateMessageContentOpened(message);

            var small = photo.GetSmall()?.Photo;
            var big = photo.GetBig();

            if (small == null || big == null)
            {
                return;
            }

            Texture.HorizontalAlignment = big.Width > big.Height ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            Texture.VerticalAlignment = big.Height > big.Width ? VerticalAlignment.Center : VerticalAlignment.Stretch;

            if (!big.Photo.Local.IsDownloadingCompleted && !message.IsSecret())
            {
                UpdateThumbnail(message, small, photo.Minithumbnail, true);
            }

            UpdateManager.Subscribe(this, message, big.Photo, ref _fileToken, UpdateFile);
            UpdateFile(message, big.Photo);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            availableSize = base.MeasureOverride(availableSize);

            var photo = GetContent(_message?.Content);
            var big = photo?.GetBig();

            if (big == null)
            {
                return availableSize;
            }

            Texture.HorizontalAlignment = big.Width > big.Height && availableSize.Height > availableSize.Width
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Stretch;
            Texture.VerticalAlignment = big.Height > big.Width && availableSize.Width > availableSize.Height
                ? VerticalAlignment.Center
                : VerticalAlignment.Stretch;

            return availableSize;
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
            if (message.Ttl > 0 && _templateApplied)
            {
                Timer.Maximum = message.Ttl;
                Timer.Value = DateTime.Now.AddSeconds(message.TtlExpiresIn);
            }
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var photo = GetContent(message.Content);
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

                if (message.IsSecret())
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
                    _message.ProtoService.DownloadFile(file.Id, 32);
                }
            }
            else
            {
                if (message.IsSecret())
                {
                    //Button.Glyph = Icons.Ttl;
                    Button.SetGlyph(file.Id, MessageContentState.Ttl);
                    Button.Progress = 1;

                    Button.Opacity = 1;
                    Overlay.Opacity = 1;

                    Texture.Source = null;
                    Subtitle.Text = Locale.FormatTtl(message.Ttl, true);
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

                    UpdateTexture(message, big, file);
                }
            }
        }

        private async void UpdateTexture(MessageViewModel message, PhotoSize big, File file)
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

            try
            {
                BitmapImage image;
                Texture.Source = image = new BitmapImage { DecodePixelWidth = width, DecodePixelHeight = height }; // UriEx.GetLocal(file.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height };

                var test = await message.ProtoService.GetFileAsync(file);
                if (test == null)
                {
                    Texture.Source = null;
                    return;
                }

                using (var stream = await test.OpenReadAsync())
                {
                    await image.SetSourceAsync(stream);
                }
            }
            catch
            {
                Texture.Source = null;
            }
        }

        private void UpdateThumbnail(object target, File file)
        {
            var photo = GetContent(_message.Content);
            if (photo == null || !_templateApplied)
            {
                return;
            }

            UpdateThumbnail(_message, file, photo.Minithumbnail, false);
        }

        private void UpdateThumbnail(MessageViewModel message, File file, Minithumbnail minithumbnail, bool download)
        {
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    LayoutRoot.Background = new ImageBrush { ImageSource = PlaceholderHelper.GetBlurred(file.Local.Path, message.IsSecret() ? 15 : 3), Stretch = Stretch.UniformToFill };
                }
                else if (download)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        if (minithumbnail != null)
                        {
                            LayoutRoot.Background = new ImageBrush { ImageSource = PlaceholderHelper.GetBlurred(minithumbnail.Data, message.IsSecret() ? 15 : 3), Stretch = Stretch.UniformToFill };
                        }

                        message.ProtoService.DownloadFile(file.Id, 1);
                    }

                    UpdateManager.Subscribe(this, message, file, ref _thumbnailToken, UpdateThumbnail, true);
                }
            }
            else if (minithumbnail != null)
            {
                LayoutRoot.Background = new ImageBrush { ImageSource = PlaceholderHelper.GetBlurred(minithumbnail.Data, message.IsSecret() ? 15 : 3), Stretch = Stretch.UniformToFill };
            }
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

            return false;
        }

        private Photo GetContent(MessageContent content)
        {
            if (content is MessagePhoto photo)
            {
                return photo.Photo;
            }
            else if (content is MessageGame game)
            {
                return game.Game.Photo;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Photo;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var photo = GetContent(_message?.Content);
            if (photo == null)
            {
                return;
            }

            var big = photo.GetBig();
            if (big == null)
            {
                if (_message?.SendingState is MessageSendingStateFailed)
                {
                    _message.ProtoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
                }

                return;
            }

            var file = big.Photo;
            if (file.Local.IsDownloadingActive)
            {
                _message.ProtoService.CancelDownloadFile(file.Id);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ProtoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _message.ProtoService.DownloadFile(file.Id, 32);
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
