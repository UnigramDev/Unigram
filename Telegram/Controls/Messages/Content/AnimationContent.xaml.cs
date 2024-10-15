//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class AnimationContent : Control, IContent, IPlayerView
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;
        private long _thumbnailToken;

        public AnimationContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(AnimationContent);
        }

        #region InitializeComponent

        private AutomaticDragHelper ButtonDrag;

        private AspectView LayoutRoot;
        private Image Texture;
        private FileButton Button;
        private AnimatedImage Player;
        private Border Overlay;
        private TextBlock Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            Texture = GetTemplateChild(nameof(Texture)) as Image;
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            Player = GetTemplateChild(nameof(Player)) as AnimatedImage;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
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

            var animation = GetContent(message, out bool isSecret, out _);
            if (animation == null || !_templateApplied)
            {
                return;
            }

            LayoutRoot.Constraint = isSecret ? Constants.SecretSize : message;
            Texture.Source = null;

            UpdateThumbnail(message, animation, animation.Thumbnail?.File, true, isSecret);

            UpdateManager.Subscribe(this, message, animation.AnimationValue, ref _fileToken, UpdateFile);
            UpdateFile(message, animation.AnimationValue);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var animation = GetContent(message, out bool isSecret, out bool isGame);
            if (animation == null || !_templateApplied)
            {
                return;
            }

            if (animation.AnimationValue.Id != file.Id)
            {
                return;
            }

            var canBeDownloaded = file.Local.CanBeDownloaded
                && !file.Local.IsDownloadingCompleted
                && !file.Local.IsDownloadingActive;

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive || (canBeDownloaded && message.Delegate.CanBeDownloaded(animation, file)))
            {
                if (canBeDownloaded)
                {
                    _message.ClientService.DownloadFile(file.Id, 32);
                }

                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;

                Subtitle.Text = isGame ? Strings.AttachGame : string.Format("{0} / {1}", FileSizeConverter.Convert(file.Local.DownloadedSize, size), FileSizeConverter.Convert(size));
                Overlay.Opacity = 1;

                Player.Source = null;
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed || (message.SendingState is MessageSendingStatePending && !file.Remote.IsUploadingCompleted))
            {
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;

                Subtitle.Text = isGame ? Strings.AttachGame : string.Format("{0} / {1}", FileSizeConverter.Convert(file.Remote.UploadedSize, size), FileSizeConverter.Convert(size));
                Overlay.Opacity = 1;

                Player.Source = null;
            }
            else if (canBeDownloaded)
            {
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                Subtitle.Text = isGame ? Strings.AttachGame : (Strings.AttachGif + ", " + FileSizeConverter.Convert(size));
                Overlay.Opacity = 1;

                Player.Source = null;
            }
            else
            {
                if (isSecret)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Ttl);
                    Button.Progress = 1;

                    if (message.SelfDestructType is MessageSelfDestructTypeTimer timer)
                    {
                        Subtitle.Text = Icons.PlayFilled12 + "\u2004\u200A" + Locale.FormatTtl(Math.Max(timer.SelfDestructTime, animation.Duration), true);
                    }
                    else
                    {
                        Subtitle.Text = Icons.ArrowClockwiseFilled12 + "\u2004\u200A1";
                    }

                    Overlay.Opacity = 1;

                    Player.Source = null;
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Animation);
                    Button.Progress = 1;

                    Subtitle.Text = isGame ? Strings.AttachGame : Strings.AttachGif;
                    Overlay.Opacity = 1;

                    Player.Source = new LocalFileSource(file);
                    message.Delegate.ViewVisibleMessages();
                }
            }

            Button.Opacity = Player.Source == null ? 1 : 0;
        }

        private void UpdateThumbnail(object target, File file)
        {
            var animation = GetContent(_message, out bool isSecret, out _);
            if (animation == null || !_templateApplied)
            {
                return;
            }

            UpdateThumbnail(_message, animation, animation.Thumbnail?.File, false, isSecret);
        }

        private void UpdateThumbnail(MessageViewModel message, Animation animation, File file, bool download, bool isSecret)
        {
            BitmapImage source = null;
            Image brush = Texture;

            if (animation.Thumbnail is { Format: ThumbnailFormatJpeg })
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    source = new BitmapImage();
                    PlaceholderHelper.GetBlurred(source, file.Local.Path, isSecret ? 15 : 3);
                }
                else if (download)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        if (animation.Minithumbnail != null)
                        {
                            source = new BitmapImage();
                            PlaceholderHelper.GetBlurred(source, animation.Minithumbnail.Data, isSecret ? 15 : 3);
                        }

                        message.ClientService.DownloadFile(file.Id, 1);
                    }

                    UpdateManager.Subscribe(this, message, file, ref _thumbnailToken, UpdateThumbnail, true);
                }
            }
            else if (animation.Minithumbnail != null)
            {
                source = new BitmapImage();
                PlaceholderHelper.GetBlurred(source, animation.Minithumbnail.Data, isSecret ? 15 : 3);
            }

            brush.Source = source;
        }

        public void Recycle()
        {
            _message = null;

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken, true);

            if (_templateApplied)
            {
                Player.Source = null;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageAnimation)
            {
                return true;
            }
            else if (content is MessageGame game && !primary)
            {
                return game.Game.Animation != null;
            }
            else if (content is MessageText text && text.LinkPreview != null && !primary)
            {
                return text.LinkPreview.Type is LinkPreviewTypeAnimation;
            }

            return false;
        }

        private Animation GetContent(MessageViewModel message, out bool isSecret, out bool isGame)
        {
            isSecret = false;
            isGame = false;

            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            if (content is MessageAnimation animation)
            {
                isSecret = animation.IsSecret;
                return animation.Animation;
            }
            else if (content is MessageGame game)
            {
                isGame = true;
                return game.Game.Animation;
            }
            else if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeAnimation previewAnimation)
            {
                return previewAnimation.Animation;
            }

            return null;
        }

        public IPlayerView GetPlaybackElement()
        {
            return Player;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var animation = GetContent(_message, out _, out _);
            if (animation == null)
            {
                return;
            }

            var file = animation.AnimationValue;
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
                _message.Delegate.OpenWebPage(text);
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

        #region IPlaybackView

        public int LoopCount => Player?.LoopCount ?? 1;

        private bool _withinViewport;

        public void ViewportChanged(bool within)
        {
            if (within && !_withinViewport)
            {
                _withinViewport = true;
                Play();
            }
            else if (_withinViewport && !within)
            {
                _withinViewport = false;
                Pause();
            }
        }

        public void Play()
        {
            Player?.Play();
        }

        public void Pause()
        {
            Player?.Pause();
        }

        #endregion
    }
}
