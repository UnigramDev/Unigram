//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using RLottie;
using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls.Messages.Content
{
    public sealed class AnimatedStickerContent : HyperlinkButton, IContent, IPlayerView
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private string _fileToken;
        private string _interactionToken;

        private CompositionAnimation _thumbnailShimmer;

        public AnimatedStickerContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(AnimatedStickerContent);
            Click += Button_Click;
        }

        #region InitializeComponent

        private LottieView Player;
        private Popup InteractionsPopup;
        private Grid Interactions;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Player = GetTemplateChild(nameof(Player)) as LottieView;
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

            var sticker = GetContent(message, out bool premium);
            if (sticker == null || !_templateApplied)
            {
                return;
            }

            if (message.Content is MessageAnimatedEmoji animatedEmoji)
            {
                Width = Player.Width = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                Height = Player.Height = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                Player.FitzModifier = (FitzModifier)(animatedEmoji.AnimatedEmoji.FitzpatrickType - 1);
                Player.IsFlipped = false;

                var sound = animatedEmoji.AnimatedEmoji.Sound;
                if (sound != null && sound.Local.CanBeDownloaded && !sound.Local.IsDownloadingActive)
                {
                    message.ClientService.DownloadFile(sound.Id, 1);
                }
            }
            else if (message.Content is MessageText)
            {
                Width = Player.Width = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                Height = Player.Height = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                Player.ColorReplacements = null;
                Player.IsFlipped = false;
            }
            else
            {
                var premiumAnimation = sticker.FullType is StickerFullTypeRegular regular && regular.PremiumAnimation != null;
                Width = Player.Width = premiumAnimation ? 180 : 180;
                Height = Player.Height = 180;
                Player.ColorReplacements = null;
                Player.IsFlipped = premium && premiumAnimation && !message.IsOutgoing;
            }

            if (!sticker.StickerValue.Local.IsDownloadingCompleted)
            {
                UpdateThumbnail(message, sticker);
            }

            UpdateManager.Subscribe(this, message, sticker.StickerValue, ref _fileToken, UpdateFile, true);
            UpdateFile(message, sticker.StickerValue);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var sticker = GetContent(message, out _);
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
                else if (sticker.FullType is StickerFullTypeRegular regular && regular.PremiumAnimation?.Id == file.Id && file.Local.IsDownloadingCompleted)
                {
                    PlayPremium(message, sticker);
                }

                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                Player.IsLoopingEnabled = message.Content is MessageSticker && SettingsService.Current.Stickers.IsLoopingEnabled;
                Player.Source = UriEx.ToLocal(file.Local.Path);

                message.Delegate.ViewVisibleMessages(false);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                Player.Source = null;
                message.ClientService.DownloadFile(file.Id, 1);
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Sticker sticker)
        {
            _thumbnailShimmer = CompositionPathParser.ParseThumbnail(sticker, out ShapeVisual visual);
            ElementCompositionPreview.SetElementChildVisual(Player, visual);
        }

        private void Player_FirstFrameRendered(object sender, EventArgs e)
        {
            _thumbnailShimmer = null;
            ElementCompositionPreview.SetElementChildVisual(Player, null);

            var sticker = _message?.Content as MessageSticker;
            if (sticker?.Sticker.FullType is StickerFullTypeRegular regular && regular.PremiumAnimation != null && sticker.IsPremium && _message.GeneratedContentUnread && IsLoaded)
            {
                _message.GeneratedContentUnread = false;
                PlayPremium(_message, sticker.Sticker);
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageSticker sticker)
            {
                return sticker.Sticker.Format is StickerFormatTgs;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Sticker != null && text.WebPage.Sticker.Format is StickerFormatTgs;
            }

            return false;
        }

        private Sticker GetContent(MessageViewModel message, out bool premium)
        {
            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageSticker sticker)
            {
                premium = sticker.IsPremium;
                return sticker.Sticker;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                premium = false;
                return text.WebPage.Sticker;
            }

            premium = false;
            return null;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var sticker = GetContent(_message, out bool premium);
            if (sticker == null)
            {
                return;
            }

            if (_message.Content is MessageAnimatedEmoji animatedEmoji)
            {
                var started = Player.Play();
                if (started)
                {
                    var sound = animatedEmoji.AnimatedEmoji.Sound;
                    if (sound != null && sound.Local.IsDownloadingCompleted)
                    {
                        SoundEffects.Play(sound);
                    }
                }

                var response = await _message.ClientService.SendAsync(new ClickAnimatedEmojiMessage(_message.ChatId, _message.Id));
                if (response is Sticker interaction)
                {
                    PlayInteraction(_message, interaction);
                }
            }
            else if (_message.Content is MessageText)
            {
                Player.Play();
            }
            else
            {
                if (premium && sticker.FullType is StickerFullTypeRegular regular && regular.PremiumAnimation != null)
                {
                    if (Interactions?.Children.Count > 0)
                    {
                        _message.Delegate.OpenSticker(sticker);
                    }
                    else
                    {
                        PlayPremium(_message, sticker);
                    }
                }
                else
                {
                    _message.Delegate.OpenSticker(sticker);
                }
            }
        }

        public void PlayInteraction(MessageViewModel message, Sticker interaction)
        {
            if (Interactions == null)
            {
                InteractionsPopup = GetTemplateChild(nameof(InteractionsPopup)) as Popup;
                Interactions = GetTemplateChild(nameof(Interactions)) as Grid;
            }

            message.Interaction = null;

            var file = interaction.StickerValue;
            if (file.Local.IsDownloadingCompleted && Interactions.Children.Count < 4)
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();

                var player = new LottieView();
                player.Width = Player.Height * 3;
                player.Height = Player.Height * 3;
                player.IsFlipped = !message.IsOutgoing;
                player.IsLoopingEnabled = false;
                player.IsHitTestVisible = false;
                player.FrameSize = new Size(512, 512);
                player.Source = UriEx.ToLocal(file.Local.Path);
                player.PositionChanged += (s, args) =>
                {
                    if (args == 1)
                    {
                        dispatcher.TryEnqueue(() =>
                        {
                            Interactions.Children.Remove(player);

                            if (Interactions.Children.Count > 0)
                            {
                                return;
                            }

                            InteractionsPopup.IsOpen = false;
                        });
                    }
                };

                var random = new Random();
                var x = Player.Height * (0.08 - (0.16 * random.NextDouble()));
                var y = Player.Height * (0.08 - (0.16 * random.NextDouble()));
                var shift = Player.Width * 0.075;

                var left = (Player.Width * 2) - shift + x;
                var right = 0 + shift - x;
                var top = Player.Height + y;
                var bottom = Player.Height - y;

                if (message.IsOutgoing)
                {
                    player.Margin = new Thickness(-left, -top, -right, -bottom);
                }
                else
                {
                    player.Margin = new Thickness(-right, -top, -left, -bottom);
                }

                Interactions.Children.Add(player);
                InteractionsPopup.IsOpen = true;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.Interaction = interaction;
                message.Delegate.DownloadFile(message, file);

                UpdateManager.Subscribe(this, message, file, ref _interactionToken, UpdateFile, true);
            }
        }

        public void PlayPremium(MessageViewModel message, Sticker sticker)
        {
            if (Interactions == null)
            {
                InteractionsPopup = GetTemplateChild(nameof(InteractionsPopup)) as Popup;
                Interactions = GetTemplateChild(nameof(Interactions)) as Grid;
            }

            if (sticker.FullType is not StickerFullTypeRegular regular)
            {
                return;
            }

            var file = regular.PremiumAnimation;
            if (file.Local.IsDownloadingCompleted && Interactions.Children.Count < 1)
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();

                var player = new LottieView();
                player.Width = 270;
                player.Height = 270;
                player.IsFlipped = !message.IsOutgoing;
                player.IsLoopingEnabled = false;
                player.IsHitTestVisible = false;
                player.FrameSize = new Size(270 * 2, 270 * 2);
                player.Source = UriEx.ToLocal(file.Local.Path);
                player.PositionChanged += (s, args) =>
                {
                    if (args == 1)
                    {
                        dispatcher.TryEnqueue(() =>
                        {
                            Interactions.Children.Remove(player);
                            InteractionsPopup.IsOpen = false;
                        });
                    }
                };

                var left = 75;
                var right = 15;
                var top = 45;
                var bottom = 45;

                if (message.IsOutgoing)
                {
                    player.Margin = new Thickness(-left, -top, -right, -bottom);
                }
                else
                {
                    player.Margin = new Thickness(-right, -top, -left, -bottom);
                }

                Interactions.Children.Add(player);
                InteractionsPopup.IsOpen = true;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.Delegate.DownloadFile(message, file);
                UpdateManager.Subscribe(this, message, file, ref _interactionToken, UpdateFile, true);
            }
        }

        #region IPlaybackView

        public bool IsLoopingEnabled => Player?.IsLoopingEnabled ?? false;

        public bool Play()
        {
            return Player?.Play() ?? false;
        }

        public void Pause()
        {
            Player?.Pause();
        }

        public void Unload()
        {
            Player?.Unload();
        }

        #endregion
    }
}
