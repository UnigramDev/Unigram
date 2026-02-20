//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using RLottie;
using System;
using Telegram.Common;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class StickerContent : HyperlinkButton, IContent, IPlayerView
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;

        private bool _isEmoji;

        public StickerContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(StickerContent);

            Click += Button_Click;
            DragStarting += Button_DragStarting;
        }

        #region InitializeComponent

        private AutomaticDragHelper ButtonDrag;

        private AspectView LayoutRoot;
        private AnimatedImage Player;
        private Popup InteractionsPopup;
        private Grid Interactions;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            Player = GetTemplateChild(nameof(Player)) as AnimatedImage;
            Player.Ready += Player_Ready;

            ButtonDrag = new AutomaticDragHelper(this, true);
            ButtonDrag.StartDetectingDrag();

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

            LayoutRoot.Constraint = message;

            var flip = false;
            var maxSize = 224d;

            if (message.Content is MessageAnimatedEmoji animatedEmoji)
            {
                maxSize = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);

                var sound = animatedEmoji.AnimatedEmoji.Sound;
                if (sound != null && sound.Local.CanBeDownloaded && !sound.Local.IsDownloadingActive)
                {
                    message.ClientService.DownloadFile(sound.Id, 1);
                }
            }
            else if (message.Content is MessageText)
            {
                maxSize = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
            }
            else
            {
                var premiumAnimation = sticker.FullType is StickerFullTypeRegular { PremiumAnimation: not null };
                flip = premium && premiumAnimation && (message.IsChannelPost || !message.IsOutgoing);
            }

            MaxWidth = maxSize;
            MaxHeight = maxSize;

            if (flip)
            {
                Player.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                Player.RenderTransform = new ScaleTransform
                {
                    ScaleX = -1
                };
            }
            else
            {
                Player.RenderTransform = null;
            }

            _isEmoji = message.Content is not MessageSticker;

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
                return;
            }

            var autoPlayStickers = message.Content is MessageSticker && PowerSavingPolicy.AutoPlayStickersInChats;
            var autoPlayEmojis = message.Content is MessageAnimatedEmoji && sticker.FullType is StickerFullTypeCustomEmoji && PowerSavingPolicy.AutoPlayStickersInChats;

            using (Player.BeginBatchUpdate())
            {
                Player.LoopCount = autoPlayStickers || autoPlayEmojis ? 0 : 1;
                Player.Source = new DelayedFileSource(_message.ClientService, sticker)
                {
                    FitzModifier = message.Content is MessageAnimatedEmoji animatedEmoji ? animatedEmoji.AnimatedEmoji.FitzpatrickType switch
                    {
                        1 => FitzModifier.Type12,
                        2 => FitzModifier.Type12,
                        3 => FitzModifier.Type3,
                        4 => FitzModifier.Type4,
                        5 => FitzModifier.Type5,
                        6 => FitzModifier.Type6,
                        _ => FitzModifier.None
                    } : FitzModifier.None
                };
            }

            if (file.Local.IsDownloadingCompleted)
            {
                message.Delegate.ViewVisibleMessages();
            }
        }

        private void Player_Ready(object sender, EventArgs e)
        {
            var sticker = _message?.Content as MessageSticker;
            if (sticker?.Sticker.FullType is StickerFullTypeRegular regular && regular.PremiumAnimation != null && sticker.IsPremium && _message.GeneratedContentUnread && IsLoaded)
            {
                _message.GeneratedContentUnread = false;
                PlayPremium(_message, sticker.Sticker);
            }
        }

        public void Recycle()
        {
            _message = null;

            UpdateManager.Unsubscribe(this, ref _fileToken);

            if (_templateApplied)
            {
                Player.Source = null;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageSticker)
            {
                return true;
            }
            else if (content is MessageText text && text.LinkPreview != null && !primary)
            {
                return text.LinkPreview.Type is LinkPreviewTypeSticker;
            }

            return false;
        }

        private Sticker GetContent(MessageViewModel message, out bool premium)
        {
            // TODO: temp, until a solution is found
            if (message?.Delegate == null)
            {
                premium = false;
                return null;
            }

            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageSticker sticker)
            {
                premium = sticker.IsPremium;
                return sticker.Sticker;
            }
            else if (content is MessageAnimatedEmoji animatedEmoji)
            {
                premium = false;
                return animatedEmoji.AnimatedEmoji.Sticker;
            }
            else if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeSticker previewSticker)
            {
                premium = false;
                return previewSticker.Sticker;
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
                if (Player.IsPlaying == false)
                {
                    Player.Play();

                    var sound = animatedEmoji.AnimatedEmoji.Sound;
                    if (sound != null && sound.Local.IsDownloadingCompleted)
                    {
                        SoundEffects.Play(sound);
                    }
                }

                var response = await _message.ClientService.SendAsync(new ClickAnimatedEmojiMessage(_message.ChatId, _message.Id));
                if (response is Sticker interaction && this.IsConnected())
                {
                    PlayInteraction(_message, interaction);
                }
                else if (response is Error && this.IsConnected())
                {
                    _message.Delegate?.OpenSticker(sticker);
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
                        ShowToast(_message, sticker);
                    }
                    else
                    {
                        PlayPremium(_message, sticker);
                    }
                }
                else if (PowerSavingPolicy.AutoPlayStickersInChats || Player.IsPlaying)
                {
                    _message.Delegate.OpenSticker(sticker);
                }
                else
                {
                    Player.Play();
                }
            }
        }

        private async void ShowToast(MessageViewModel message, Sticker sticker)
        {
            var response = await message.ClientService.SendAsync(new GetStickerSet(sticker.SetId));
            if (response is StickerSet stickerSet)
            {
                var confirm = await ToastPopup.ShowActionAsync(XamlRoot, string.Format("**{0}**\n{1}", stickerSet.Title, Strings.PremiumStickerTooltip), Strings.ViewAction, new DelayedFileSource(message.ClientService, sticker));
                if (confirm == ContentDialogResult.Primary)
                {
                    message.Delegate.OpenSticker(sticker);
                }
            }
        }

        private void Button_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            MessageHelper.DragStarting(_message, args);
        }

        public void PlayInteraction(MessageViewModel message, Sticker interaction)
        {
            if (Interactions == null)
            {
                InteractionsPopup = GetTemplateChild(nameof(InteractionsPopup)) as Popup;
                Interactions = GetTemplateChild(nameof(Interactions)) as Grid;
            }

            if (Interactions.Children.Count < 4)
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();

                var height = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                var player = new AnimatedImage();
                player.Width = height * 3;
                player.Height = height * 3;
                player.LoopCount = 1;
                player.IsHitTestVisible = false;
                player.FrameSize = new Size(height * 3, height * 3);
                player.DecodeFrameType = DecodePixelType.Logical;
                player.AutoPlay = true;
                player.Source = new DelayedFileSource(_message.ClientService, interaction.StickerValue);
                player.LoopCompleted += (s, args) =>
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
                };

                var random = new Random();
                var x = height * (0.08 - (0.16 * random.NextDouble()));
                var y = height * (0.08 - (0.16 * random.NextDouble()));
                var shift = height * 0.075;

                var left = (height * 2) - shift + x;
                var right = 0 + shift - x;
                var top = height + y;
                var bottom = height - y;

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

            if (Interactions.Children.Count < 1)
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();

                var kPremiumMultiplier = 1024.0 / 683;
                var kPremiumShift = 56.0 / 683;

                var inner = 224.0;
                var outer = inner * kPremiumMultiplier;

                var player = new AnimatedImage();
                player.Width = outer;
                player.Height = outer;
                player.LoopCount = 1;
                player.IsHitTestVisible = false;
                player.FrameSize = new Size(outer, outer);
                player.DecodeFrameType = DecodePixelType.Logical;
                player.AutoPlay = true;
                player.Source = new DelayedFileSource(_message.ClientService, regular.PremiumAnimation);
                player.LoopCompleted += (s, args) =>
                {
                    dispatcher.TryEnqueue(() =>
                    {
                        Interactions.Children.Remove(player);
                        InteractionsPopup.IsOpen = false;
                    });
                };

                if (message.IsChannelPost || !message.IsOutgoing)
                {
                    player.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                    player.RenderTransform = new ScaleTransform
                    {
                        ScaleX = -1
                    };
                }

                var shift = inner * kPremiumShift;
                var left = message.IsVisuallyOutgoing
                    ? inner + shift - outer
                    : -shift;
                var top = (inner - outer) / 2;
                var right = inner - left - outer;

                player.Margin = new Thickness(left, top, right, top);

                Interactions.Children.Add(player);
                InteractionsPopup.IsOpen = true;
            }
            else if (Interactions.Children[0] is AnimatedImage animated)
            {
                animated.Play();
            }
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
            if (_isEmoji && PowerSavingPolicy.AutoPlayEmojiInChats)
            {
                Player?.Play();
            }
            else if (PowerSavingPolicy.AutoPlayStickersInChats)
            {
                Player?.Play();
            }
        }

        public void Pause()
        {
            Player?.Pause();
        }

        #endregion
    }
}
