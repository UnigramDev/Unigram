//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Cells
{
    public sealed partial class GroupCallMessageCell : UserControl
    {
        private GroupCallMessage _message;

        public GroupCallMessageCell()
        {
            InitializeComponent();
        }

        public void Update(IClientService clientService, GroupCallMessage message)
        {
            _message = message;

            bool reaction = false;
            if (Emoji.ContainsSingleEmoji(message.Text.Text))
            {
                if (message.Text.Entities.Count == 0)
                {
                    if (clientService.ActiveReactions.Contains(message.Text.Text))
                    {
                        reaction = true;
                        Animate(clientService, message, message.Text.Text);
                    }
                }
                else if (message.Text.Entities.Count == 1)
                {
                    var entity = message.Text.Entities[0];
                    if (entity.Offset == 0 && entity.Length == message.Text.Text.Length && entity.Type is TextEntityTypeCustomEmoji customEmoji)
                    {
                        reaction = true;
                        Animate(clientService, message, customEmoji.CustomEmojiId);
                    }
                }
            }

            var title = clientService.GetTitle(message.SenderId);
            var formatted = title.AsFormattedText(new TextEntityTypeBold());
            formatted = ClientEx.Format("{0} {1}", formatted, reaction ? Icons.ZWNJ : message.Text);

            Photo.Source = ProfilePictureSource.MessageSender(clientService, message.SenderId);
            Text.SetText(clientService, formatted);

            Reaction.Visibility = reaction
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void Animate(IClientService clientService, GroupCallMessage message, string emoji)
        {
            var response = await clientService.SendAsync(new GetEmojiReaction(emoji));
            if (response is EmojiReaction reaction && reaction.AroundAnimation != null)
            {
                var source = new DelayedFileSource(clientService, reaction.ActivateAnimation);

                var around = await clientService.DownloadFileAsync(reaction.AroundAnimation.StickerValue, 32);
                if (around.Local.IsDownloadingCompleted && this.IsConnected())
                {
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Animate(message, source, around, true));
                }
            }
        }

        private async void Animate(IClientService clientService, GroupCallMessage message, long customEmojiId)
        {
            var response = await clientService.SendAsync(new GetCustomEmojiReactionAnimations());
            if (response is Stickers stickers)
            {
                var source = new CustomEmojiFileSource(clientService, customEmojiId);

                var random = new Random();
                var next = random.Next(0, stickers.StickersValue.Count);

                var around = await clientService.DownloadFileAsync(stickers.StickersValue[next].StickerValue, 32);
                if (around.Local.IsDownloadingCompleted && this.IsConnected())
                {
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Animate(message, source, around, true));
                }
            }
        }

        private void Animate(GroupCallMessage message, AnimatedImageSource source, File around, bool cache)
        {
            if (_message != message)
            {
                return;
            }

            Reaction.Source = source;
            Reaction.Play();

            var popup = ReactionPopup;
            var dispatcher = DispatcherQueue.GetForCurrentThread();

            var aroundView = new AnimatedImage();
            aroundView.Width = 20 * 3;
            aroundView.Height = 20 * 3;
            aroundView.LoopCount = 1;
            aroundView.FrameSize = new Size(20 * 3, 20 * 3);
            aroundView.DecodeFrameType = DecodePixelType.Logical;
            aroundView.IsCachingEnabled = cache;
            aroundView.AutoPlay = true;
            aroundView.Source = new LocalFileSource(around);
            aroundView.LoopCompleted += (s, args) =>
            {
                dispatcher.TryEnqueue(Continue);
            };

            var root = new Grid();
            root.Width = 20 * 3;
            root.Height = 20 * 3;
            root.Children.Add(aroundView);

            popup.Child = root;
            popup.XamlRoot = XamlRoot;
            popup.IsOpen = true;
        }

        private void Continue()
        {
            Logger.Info();

            var popup = ReactionPopup;
            if (popup == null)
            {
                return;
            }

            popup.IsOpen = false;
            popup.Child = null;
        }
    }

    public partial class GroupCallMessagePanel : Panel
    {
        private bool _horizontal;

        protected override Size MeasureOverride(Size availableSize)
        {
            var text = Children[0];
            var reaction = Children[1];

            text.Measure(availableSize);
            reaction.Measure(availableSize);

            if (text.DesiredSize.Width + reaction.DesiredSize.Width > availableSize.Width)
            {
                _horizontal = false;
                return new Size(text.DesiredSize.Width, text.DesiredSize.Height + reaction.DesiredSize.Height);
            }

            _horizontal = true;
            return new Size(text.DesiredSize.Width + reaction.DesiredSize.Width, Math.Max(text.DesiredSize.Height, reaction.DesiredSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var text = Children[0];
            var reaction = Children[1];

            if (_horizontal)
            {
                text.Arrange(new(0, 0, text.DesiredSize.Width, text.DesiredSize.Height));
                reaction.Arrange(new(text.DesiredSize.Width, 0, reaction.DesiredSize.Width, reaction.DesiredSize.Height));
            }
            else
            {
                text.Arrange(new(0, 0, text.DesiredSize.Width, text.DesiredSize.Height));
                reaction.Arrange(new(0, text.DesiredSize.Height, reaction.DesiredSize.Width, reaction.DesiredSize.Height));
            }

            return finalSize;
        }
    }
}
