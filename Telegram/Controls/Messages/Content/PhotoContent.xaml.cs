//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class PhotoContent : Control, IContentWithFile
    {
        private readonly bool _album;

        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private PaidMediaPhoto _paidMedia;

        private long _fileToken;
        private long _thumbnailToken;

        private ThumbnailController _thumbnailController;

        private bool _hidden = true;

        public PhotoContent(MessageViewModel message, PaidMediaPhoto paidMedia = null, bool album = false)
        {
            _message = message;
            _paidMedia = paidMedia;
            _album = album;

            DefaultStyleKey = typeof(PhotoContent);
        }

        public PhotoContent()
        {
            DefaultStyleKey = typeof(PhotoContent);
        }

        #region InitializeComponent

        private AutomaticDragHelper ButtonDrag;

        private AspectView LayoutRoot;
        private ImageBrush ThumbnailTexture;
        private ImageBrush Texture;
        private AnimatedImage Particles;
        private Border Overlay;
        private TextBlock Subtitle;
        private FileButton Button;
        private SelfDestructTimer Timer;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            ThumbnailTexture = LayoutRoot.Background as ImageBrush;
            Texture = GetTemplateChild(nameof(Texture)) as ImageBrush;
            Particles = GetTemplateChild(nameof(Particles)) as AnimatedImage;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            Timer = GetTemplateChild(nameof(Timer)) as SelfDestructTimer;

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

        private void Texture_ImageOpened(object sender, RoutedEventArgs e)
        {
            var visual = ElementComposition.GetElementVisual(LayoutRoot.Children[0]);
            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0, 0);
            animation.InsertKeyFrame(1, 1);

            visual.StartAnimation("Opacity", animation);
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            var prevId = _message?.Id;
            var nextId = message?.Id;

            _message = message;

            var photo = GetContent(message, out bool hasSpoiler, out bool isSecret, out _);
            if (photo == null || !_templateApplied)
            {
                _hidden = (prevId != nextId || _hidden) && hasSpoiler;
                return;
            }

            _hidden = (prevId != nextId || _hidden) && hasSpoiler;

            LayoutRoot.Constraint = _album ? null : isSecret ? Constants.SecretSize : ((object)_paidMedia ?? photo);
            //LayoutRoot.Background = null;
            Texture.Stretch = _album
                ? Stretch.UniformToFill
                : Stretch.Uniform;

            //UpdateMessageContentOpened(message);

            var small = photo.GetSmall()?.Photo;
            var big = photo.GetBig();

            if (small == null || big == null)
            {
                UpdateTexture(message, null);
                return;
            }

            if (small.Id != big.Photo.Id && !big.Photo.Local.IsDownloadingCompleted || isSecret || hasSpoiler)
            {
                UpdateThumbnail(message, small, photo.Minithumbnail, true, isSecret, hasSpoiler);
            }
            else
            {
                UpdateThumbnail(message, null, photo.Minithumbnail, false, isSecret, hasSpoiler);
            }

            UpdateManager.Subscribe(this, message, big.Photo, ref _fileToken, UpdateFile);
            UpdateFile(message, big.Photo);
        }

        public void Mockup(MessagePhoto photo)
        {
            var big = photo.Photo.GetBig();

            LayoutRoot.Constraint = photo;
            LayoutRoot.Background = null;
            Texture.ImageSource = new BitmapImage(new Uri(big.Photo.Local.Path));

            Overlay.Opacity = 0;
            Button.Opacity = 0;
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (message.SelfDestructType is MessageSelfDestructTypeTimer selfDestructTypeTimer && _templateApplied)
            {
                Timer.Maximum = selfDestructTypeTimer.SelfDestructTime;
                Timer.Value = DateTime.Now.AddSeconds(message.SelfDestructIn);
            }
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var photo = GetContent(message, out bool hasSpoiler, out bool isSecret, out bool isGame);
            if (photo == null || !_templateApplied)
            {
                return;
            }

            var big = photo.GetBig();
            if (big == null || big.Photo.Id != file.Id)
            {
                return;
            }

            if (isGame)
            {
                Subtitle.Text = Strings.AttachGame;
                Overlay.Opacity = 1;
            }
            else if (isSecret)
            {
                if (message.SelfDestructType is MessageSelfDestructTypeTimer selfDestructTypeTimer)
                {
                    Subtitle.Text = Icons.PlayFilled12 + "\u2004\u200A" + Locale.FormatTtl(selfDestructTypeTimer.SelfDestructTime, true);
                }
                else
                {
                    Subtitle.Text = Icons.ArrowClockwiseFilled12 + "\u2004\u200A1";
                }

                Overlay.Opacity = 1;
            }
            else
            {
                Overlay.Opacity = 0;
            }

            var canBeDownloaded = file.Local.CanBeDownloaded
                && !file.Local.IsDownloadingCompleted
                && !file.Local.IsDownloadingActive;

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive || (canBeDownloaded && message.Delegate.CanBeDownloaded(photo, file)))
            {
                if (canBeDownloaded)
                {
                    _message.ClientService.DownloadFile(file.Id, 32);
                }

                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Button.Opacity = 1;

                UpdateTexture(message, null);
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed || (message.SendingState is MessageSendingStatePending && !file.Remote.IsUploadingCompleted))
            {
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Button.Opacity = 1;

                if (isSecret || string.IsNullOrEmpty(file.Local.Path))
                {
                    UpdateTexture(message, null);
                }
                else
                {
                    UpdateTexture(message, big);
                }
            }
            else if (canBeDownloaded)
            {
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                Button.Opacity = 1;

                UpdateTexture(message, null);
            }
            else
            {
                if (isSecret)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Ttl);
                    Button.Progress = 1;

                    Button.Opacity = 1;

                    UpdateTexture(message, null);
                }
                else
                {
                    Button.Progress = 1;

                    if (message.Content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeEmbeddedVideoPlayer || (message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0))
                    {
                        Button.SetGlyph(file.Id, message.SendingState is MessageSendingStatePending && message.MediaAlbumId != 0 ? MessageContentState.Confirm : MessageContentState.Play);
                        Button.Opacity = 1;
                    }
                    else
                    {
                        Button.SetGlyph(file.Id, MessageContentState.Photo);
                        Button.Opacity = 0;
                    }

                    if (hasSpoiler && _hidden)
                    {
                        UpdateTexture(message, null);
                    }
                    else
                    {
                        UpdateTexture(message, big);
                    }
                }
            }
        }

        private int _textureId;

        private void UpdateTexture(MessageViewModel message, PhotoSize photoSize)
        {
            if (_textureId == (photoSize?.Photo.Id ?? 0))
            {
                return;
            }

            if (photoSize != null)
            {
                var width = 0;
                var height = 0;

                if (width > MaxWidth || height > MaxHeight)
                {
                    double ratioX = MaxWidth / photoSize.Width;
                    double ratioY = MaxHeight / photoSize.Height;
                    double ratio = Math.Max(ratioX, ratioY);

                    width = (int)(photoSize.Width * ratio);
                    height = (int)(photoSize.Height * ratio);
                }

                _textureId = photoSize.Photo.Id;
                Texture.ImageSource = UriEx.ToBitmap(photoSize.Photo.Local.Path, width, height);
            }
            else
            {
                _textureId = 0;
                Texture.ImageSource = null;
            }
        }

        private void UpdateThumbnail(object target, File file)
        {
            var photo = GetContent(_message, out bool hasSpoiler, out bool isSecret, out _);
            if (photo == null || !_templateApplied)
            {
                return;
            }

            UpdateThumbnail(_message, file, photo.Minithumbnail, false, isSecret, hasSpoiler);
        }

        private void UpdateThumbnail(MessageViewModel message, File file, Minithumbnail minithumbnail, bool download, bool isSecret, bool hasSpoiler)
        {
            _thumbnailController ??= new ThumbnailController(ThumbnailTexture);

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    _thumbnailController.Blur(file.Local.Path, isSecret || (hasSpoiler && _hidden) ? 15 : 3, HashCode.Combine(message.ChatId, message.Id));
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
                        _thumbnailController.Blur(minithumbnail.Data, isSecret || (hasSpoiler && _hidden) ? 15 : 3, HashCode.Combine(message.ChatId, message.Id));
                    }
                    else
                    {
                        _thumbnailController.Recycle();
                    }
                }
            }
            else if (minithumbnail != null)
            {
                _thumbnailController.Blur(minithumbnail.Data, isSecret || (hasSpoiler && _hidden) ? 15 : 3, HashCode.Combine(message.ChatId, message.Id));
            }
            else
            {
                _thumbnailController.Recycle();
            }

            Particles.Source = isSecret || (hasSpoiler && _hidden)
                ? new ParticlesImageSource()
                : null;
        }

        public void Recycle()
        {
            _message = null;
            _thumbnailController?.Recycle();

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken);
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
            else if (content is MessageText text && text.LinkPreview != null && !primary)
            {
                return text.LinkPreview.HasPhoto();
            }
            else if (content is MessageInvoice invoice && invoice.PaidMedia is PaidMediaPhoto)
            {
                return true;
            }
            else if (content is MessageSponsored { Content: MessagePhoto } && !primary)
            {
                return true;
            }

            return false;
        }

        private Photo GetContent(MessageViewModel message, out bool hasSpoiler, out bool isSecret, out bool isGame)
        {
            hasSpoiler = false;
            isSecret = false;
            isGame = false;

            if (message?.Delegate == null)
            {
                return null;
            }

            if (_paidMedia != null)
            {
                return _paidMedia.Photo;
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
                isGame = true;
                return game.Game.Photo;
            }
            else if (content is MessageText text)
            {
                if (text.LinkPreview?.Type is LinkPreviewTypePhoto previewPhoto)
                {
                    return previewPhoto.Photo;
                }
                else if (text.LinkPreview?.Type is LinkPreviewTypeAlbum previewAlbum && previewAlbum.Media[0] is LinkPreviewAlbumMediaPhoto albumPhoto)
                {
                    return albumPhoto.Photo;
                }

                return text.LinkPreview?.Type switch
                {
                    LinkPreviewTypeApp app => app.Photo,
                    LinkPreviewTypeArticle article => article.Photo,
                    LinkPreviewTypeChannelBoost channelBoost => channelBoost.Photo.ToPhoto(),
                    LinkPreviewTypeChat chat => chat.Photo.ToPhoto(),
                    LinkPreviewTypeEmbeddedAudioPlayer embeddedAudioPlayer => embeddedAudioPlayer.Thumbnail,
                    LinkPreviewTypeEmbeddedAnimationPlayer embeddedAnimationPlayer => embeddedAnimationPlayer.Thumbnail,
                    LinkPreviewTypeEmbeddedVideoPlayer embeddedVideoPlayer => embeddedVideoPlayer.Thumbnail,
                    LinkPreviewTypeSupergroupBoost supergroupBoost => supergroupBoost.Photo.ToPhoto(),
                    LinkPreviewTypeStoryAlbum storyAlbum => storyAlbum.PhotoIcon,
                    LinkPreviewTypeUser user => user.Photo.ToPhoto(),
                    LinkPreviewTypeVideoChat videoChat => videoChat.Photo.ToPhoto(),
                    LinkPreviewTypeWebApp webApp => webApp.Photo,
                    _ => null
                };
            }
            else if (content is MessageInvoice invoice && invoice.PaidMedia is PaidMediaPhoto paidMediaPhoto)
            {
                return paidMediaPhoto.Photo;
            }
            else if (content is MessageSponsored { Content: MessagePhoto sponsored })
            {
                return sponsored.Photo;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var photo = GetContent(_message, out bool hasSpoiler, out _, out _);
            if (photo == null)
            {
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
            else if (_message.Content is MessageText text && text.LinkPreview.HasText())
            {
                _message.Delegate.OpenWebPage(_message);
            }
            else if (_paidMedia != null)
            {
                _message.Delegate.OpenPaidMedia(_message, _paidMedia, this);
            }
            else if (hasSpoiler && _hidden)
            {
                _hidden = false;
                UpdateMessage(_message);
            }
            else
            {
                _message.Delegate.OpenMedia(_message, this);
            }
        }

        private void Button_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            MessageHelper.DragStarting(_message, args);
        }
    }
}
