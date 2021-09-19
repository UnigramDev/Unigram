using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages.Content
{
    public sealed class AnimatedStickerContent : HyperlinkButton, IContentWithFile, IContentWithPlayback
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private CompositionAnimation _thumbnailShimmer;

        private int _interacting;

        public AnimatedStickerContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(AnimatedStickerContent);
            Click += Button_Click;
        }

        #region InitializeComponent

        private LottieView Player;
        private Grid Interactions;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Player = GetTemplateChild(nameof(Player)) as LottieView;
            Interactions = GetTemplateChild(nameof(Interactions)) as Grid;

            Player.FirstFrameRendered += Player_FirstFrameRendered;

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

            var sticker = GetContent(message);
            if (sticker == null || !_templateApplied)
            {
                return;
            }

            if (message.Content is MessageText text)
            {
                Width = Player.Width = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                Height = Player.Height = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                Player.ColorReplacements = Emoji.GetColorReplacements(text.Text.Text);

                var sound = message.ProtoService.GetEmojiSound(sticker.Emoji);
                if (sound != null && sound.Local.CanBeDownloaded && !sound.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(sound.Id, 1);
                }
            }
            else
            {
                Width = Player.Width = 200;
                Height = Player.Height = 200;
                Player.ColorReplacements = null;
            }

            if (!sticker.StickerValue.Local.IsDownloadingCompleted)
            {
                UpdateThumbnail(message, sticker.Outline);
            }

            UpdateFile(message, sticker.StickerValue);
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var sticker = GetContent(message);
            if (sticker == null || !_templateApplied)
            {
                return;
            }

            if (sticker.StickerValue.Id != file.Id)
            {
                if (message.Interaction?.StickerValue.Id == file.Id && file.Local.IsDownloadingCompleted)
                {
                    PlayInteraction(message, message.Interaction);
                }

                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                Player.IsLoopingEnabled = message.Content is MessageSticker && SettingsService.Current.Stickers.IsLoopingEnabled;
                Player.Source = UriEx.ToLocal(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                Player.Source = null;
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        private void UpdateThumbnail(MessageViewModel message, IList<ClosedVectorPath> contours)
        {
            _thumbnailShimmer = CompositionPathParser.ParseThumbnail(contours, out ShapeVisual visual);
            ElementCompositionPreview.SetElementChildVisual(Player, visual);
        }

        private void Player_FirstFrameRendered(object sender, EventArgs e)
        {
            _thumbnailShimmer = null;
            ElementCompositionPreview.SetElementChildVisual(Player, null);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageSticker sticker)
            {
                return sticker.Sticker.IsAnimated;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Sticker != null && text.WebPage.Sticker.IsAnimated;
            }

            return false;
        }

        private Sticker GetContent(MessageViewModel message)
        {
            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageSticker sticker)
            {
                return sticker.Sticker;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Sticker;
            }

            return null;
        }

        public IPlayerView GetPlaybackElement()
        {
            return Player;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var sticker = GetContent(_message);
            if (sticker == null)
            {
                return;
            }

            if (_message.Content is MessageText)
            {
                var started = Player.Play();
                if (started)
                {
                    var sound = _message.ProtoService.GetEmojiSound(sticker.Emoji);
                    if (sound != null && sound.Local.IsDownloadingCompleted)
                    {
                        SoundEffects.Play(sound);
                    }
                }

                var response = await _message.ProtoService.SendAsync(new ClickAnimatedEmojiMessage(_message.ChatId, _message.Id));
                if (response is Sticker interaction)
                {
                    PlayInteraction(_message, interaction);
                }
            }
            else
            {
                _message.Delegate.OpenSticker(sticker);
            }
        }

        public void PlayInteraction(MessageViewModel message, Sticker interaction)
        {
            message.Interaction = null;

            var file = interaction.StickerValue;
            if (file.Local.IsDownloadingCompleted)
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();
                var container = this.Ancestors<ListViewItem>().FirstOrDefault();

                var player = new LottieView();
                player.Width = Player.Width * 3;
                player.Height = Player.Height * 3;
                player.IsFlipped = !message.IsOutgoing;
                player.IsLoopingEnabled = false;
                player.IsHitTestVisible = false;
                player.FrameSize = new Windows.Graphics.SizeInt32 { Width = 512, Height = 512 };
                player.Source = UriEx.ToLocal(interaction.StickerValue.Local.Path);
                player.PositionChanged += (s, args) =>
                {
                    if (args == 1)
                    {
                        dispatcher.TryEnqueue(() =>
                        {
                            Interactions.Children.Remove(player);

                            if (_interacting-- > 1)
                            {
                                return;
                            }

                            Canvas.SetZIndex(container, 0);
                        });
                    }
                };

                var random = new Random();
                var x = Player.Width * (0.08 - (0.16 * random.NextDouble()));
                var y = Player.Height * (0.08 - (0.16 * random.NextDouble()));
                var shift = Player.Width * 0.075;


                var left = (Player.Width * 2) - shift + x;
                var right = 0 + shift - x;
                var top = Player.Height + y;
                var bottom = Player.Height - y;

                player.Background = new SolidColorBrush(Color.FromArgb(0x33, (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));

                if (message.IsOutgoing)
                {
                    player.Margin = new Thickness(-left, -top, -right, -bottom);
                }
                else
                {
                    player.Margin = new Thickness(-right, -top, -left, -bottom);
                }

                Interactions.Children.Add(player);

                _interacting++;
                Canvas.SetZIndex(container, 1);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.Interaction = interaction;
                message.Delegate.DownloadFile(message, file);
            }
        }
    }
}
