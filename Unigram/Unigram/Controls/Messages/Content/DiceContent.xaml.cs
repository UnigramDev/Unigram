using Microsoft.UI.Xaml.Controls;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services.Updates;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class DiceContent : HyperlinkButton, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

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

            Width = Player.Width = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625);
            Height = Player.Height = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625);

            var state = dice.FinalState ?? dice.InitialState;
            if (state is DiceStickersRegular regular)
            {
                if (regular.Sticker.Thumbnail != null && !regular.Sticker.StickerValue.Local.IsDownloadingCompleted)
                {
                    UpdateThumbnail(message, regular.Sticker.Thumbnail.File);
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

            var state = dice.FinalState ?? dice.InitialState;
            if (state == null)
            {
                return;
            }

            if (state is DiceStickersRegular regular)
            {
                if (regular.Sticker.Thumbnail != null && regular.Sticker.Thumbnail.File.Id == file?.Id)
                {
                    UpdateThumbnail(message, file);
                    return;
                }
                //else if (regular.Sticker.StickerValue.Id != file?.Id)
                //{
                //    return;
                //}
            }

            if (IsDownloadingCompleted(state))
            {
                Player.IsContentUnread = message.GeneratedContentUnread;
                Player.Value = dice;

                if (message.IsOutgoing &&
                    message.GeneratedContentUnread &&
                    dice.FinalState != null &&
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

        private void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Player.Thumbnail = PlaceholderHelper.GetWebPFrame(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        private bool IsDownloadingCompleted(DiceStickers stickers)
        {
            if (stickers is DiceStickersRegular regular)
            {
                return regular.Sticker.StickerValue.Local.IsDownloadingCompleted;
            }
            else if (stickers is DiceStickersSlotMachine slotMachine)
            {
                return slotMachine.Background.StickerValue.Local.IsDownloadingCompleted
                    && slotMachine.LeftReel.StickerValue.Local.IsDownloadingCompleted
                    && slotMachine.CenterReel.StickerValue.Local.IsDownloadingCompleted
                    && slotMachine.RightReel.StickerValue.Local.IsDownloadingCompleted
                    && slotMachine.Lever.StickerValue.Local.IsDownloadingCompleted;
            }

            return false;
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
            // We can't recycle it as we must destroy CanvasAnimatedControl on Unload.
            if (Player.IsUnloaded)
            {
                return false;
            }

            if (content is MessageDice)
            {
                return true;
            }

            return false;
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
