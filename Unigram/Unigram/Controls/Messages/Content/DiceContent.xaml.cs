using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services.Updates;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class DiceContent : HyperlinkButton, IContentWithFile, IContentWithPlayback
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private CompositionAnimation _thumbnailShimmer;

        public DiceContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var dice = message.Content as MessageDice;
            if (dice == null)
            {
                return;
            }

            Width = Player.Width = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
            Height = Player.Height = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);

            var state = dice.GetState();
            if (state is DiceStickersRegular regular)
            {
                if (!regular.Sticker.StickerValue.Local.IsDownloadingCompleted)
                {
                    UpdateThumbnail(message, regular.Sticker.Outline);
                }
            }

            UpdateFile(message, null);
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var dice = _message?.Content as MessageDice;
            if (dice == null)
            {
                return;
            }

            var state = dice.GetState();
            if (state == null || (file != null && !state.UpdateFile(file)))
            {
                return;
            }

            if (state != dice.FinalState && dice.FinalState != null)
            {
                DownloadFile(message, dice.FinalState);
            }

            if (state is DiceStickersRegular regular)
            {
                //if (regular.Sticker.StickerValue.Id != file?.Id)
                //{
                //    return;
                //}
            }

            if (state.IsDownloadingCompleted())
            {
                Player.IsContentUnread = message.GeneratedContentUnread;
                Player.SetValue(state, state == dice.FinalState ? dice.Value : 0);

                if (message.IsOutgoing &&
                    message.GeneratedContentUnread &&
                    dice.IsFinalState() &&
                    dice.SuccessAnimationFrameNumber != 0)
                {
                    Player.IndexChanged += OnIndexChanged;
                }
                else
                {
                    Player.IndexChanged -= OnIndexChanged;
                }
            }
            else
            {
                DownloadFile(message, state);
            }
        }

        private void OnIndexChanged(object sender, int e)
        {
            if (_message?.Content is MessageDice dice && dice.SuccessAnimationFrameNumber == e)
            {
                _message.Delegate.Aggregator.Publish(new UpdateConfetti());
                Player.IndexChanged -= OnIndexChanged;
            }
        }

        private void UpdateThumbnail(MessageViewModel message, IList<ClosedVectorPath> contours)
        {
            if (ApiInfo.CanUseDirectComposition)
            {
                _thumbnailShimmer = CompositionPathParser.ParseThumbnail(contours, ActualWidth, out ShapeVisual visual);
                ElementCompositionPreview.SetElementChildVisual(Player, visual);
            }
        }

        private void Player_FirstFrameRendered(object sender, EventArgs e)
        {
            _thumbnailShimmer = null;
            ElementCompositionPreview.SetElementChildVisual(Player, null);
        }

        private void DownloadFile(MessageViewModel message, DiceStickers stickers)
        {
            if (stickers is DiceStickersRegular regular)
            {
                if (regular.Sticker.StickerValue.Local.CanBeDownloaded && !regular.Sticker.StickerValue.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(regular.Sticker.StickerValue.Id, 1);
                }
            }
            else if (stickers is DiceStickersSlotMachine slotMachine)
            {
                if (slotMachine.Background.StickerValue.Local.CanBeDownloaded && !slotMachine.Background.StickerValue.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(slotMachine.Background.StickerValue.Id, 1);
                }
                if (slotMachine.LeftReel.StickerValue.Local.CanBeDownloaded && !slotMachine.LeftReel.StickerValue.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(slotMachine.LeftReel.StickerValue.Id, 1);
                }
                if (slotMachine.CenterReel.StickerValue.Local.CanBeDownloaded && !slotMachine.CenterReel.StickerValue.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(slotMachine.CenterReel.StickerValue.Id, 1);
                }
                if (slotMachine.RightReel.StickerValue.Local.CanBeDownloaded && !slotMachine.RightReel.StickerValue.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(slotMachine.RightReel.StickerValue.Id, 1);
                }
                if (slotMachine.Lever.StickerValue.Local.CanBeDownloaded && !slotMachine.Lever.StickerValue.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(slotMachine.Lever.StickerValue.Id, 1);
                }
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageDice)
            {
                return true;
            }

            return false;
        }

        public IPlayerView GetPlaybackElement()
        {
            return Player;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dice = _message?.Content as MessageDice;
            if (dice == null)
            {
                return;
            }

            string text;
            switch (dice.Emoji)
            {
                case "\uD83C\uDFB2":
                    text = Strings.Resources.DiceInfo2;
                    break;
                case "\uD83C\uDFAF":
                    text = Strings.Resources.DartInfo;
                    break;
                default:
                    text = string.Format(Strings.Resources.DiceEmojiInfo, dice.Emoji);
                    break;
            }

            var formatted = Client.Execute(new ParseMarkdown(new FormattedText(text, new TextEntity[0]))) as FormattedText;
            Window.Current.ShowTeachingTip(this, formatted, _message.IsOutgoing && !_message.IsChannelPost ? TeachingTipPlacementMode.TopLeft : TeachingTipPlacementMode.TopRight);
        }
    }
}
