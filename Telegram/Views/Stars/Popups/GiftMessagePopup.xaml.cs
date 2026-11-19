//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Drawers;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Stars.Popups
{
    /// <summary>
    /// The message added to a gift bought on sale for someone else, previewed as the chat will show it.
    /// </summary>
    public sealed partial class GiftMessagePopup : ModalPopup
    {
        private readonly IClientService _clientService;
        private readonly ISettingsService _settings;

        private readonly UpgradedGift _gift;
        private readonly MessageSender _receiverId;
        private readonly Chat _chat;

        public GiftMessagePopup(IClientService clientService, UpgradedGift gift, MessageSender receiverId)
        {
            InitializeComponent();

            _clientService = clientService;
            _settings = clientService.Session.Resolve<ISettingsService>();

            _gift = gift;
            _receiverId = receiverId;

            clientService.TryGetChatFromUser(clientService.Options.MyId, out _chat);

            Title = Strings.GiftMessageAddTitle;
            TextBlockHelper.SetMarkdown(Description, string.Format(Strings.GiftMessageAddDescription, clientService.GetTitle(receiverId, true), gift.ToName()));

            var emoji = EmojiDrawerViewModel.Create(clientService.Session);
            EmojiPanel.DataContext = emoji;
            CaptionInput.DataContext = emoji;
            CaptionInput.CustomEmoji = CustomEmoji;
            CaptionInput.MaxLength = (int)clientService.Options.GiftTextLengthMax;
            CaptionInput.PlaceholderText = Strings.GiftMessageAddHint;
            CaptionInput.AllowedEntities = FormattedTextEntity.Bold
                | FormattedTextEntity.Italic
                | FormattedTextEntity.Underline
                | FormattedTextEntity.Strikethrough
                | FormattedTextEntity.Spoiler
                | FormattedTextEntity.CustomEmoji;

            PrimaryButtonText = Strings.GiftMessageSendNow;

            BackgroundControl.Update(clientService, null);
            UpdatePreview(string.Empty.AsFormattedText());
        }

        public FormattedText Text => CaptionInput.GetFormattedText();

        public bool IsPrivate => MakePublic.IsChecked is not true;

        private void UpdatePreview(FormattedText text)
        {
            // Not private even when unticked: the receiver still sees the sender, and the preview is theirs.
            var price = _gift.ResaleParameters?.GramOnly is true
                ? new GiftResalePriceGram(_gift.ResaleParameters.GramCentCount)
                : (GiftResalePrice)new GiftResalePriceStar(_gift.ResaleParameters?.StarCount ?? 0);

            var content = new MessageUpgradedGift(_gift, _clientService.MyId, _receiverId, new UpgradedGiftOriginResale(price), string.Empty, text, false, false, false, false, 0, 0, 0, 0, 0, 0);
            var message = new Message(0, _clientService.MyId, null, 0, null, null, false, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, null, null, null, 0, 0, 0, null, 0, 0, string.Empty, 0, string.Empty, 0, 0, null, string.Empty, content, null, null);

            var delegato = new ChatMessageDelegate(_clientService, _settings, _chat);
            var viewModel = new MessageViewModel(_clientService, delegato, _chat, null, null, message, true);

            Message.UpdateMessage(viewModel);
            Message.ButtonText = Strings.GiftMessageSendNow;
        }

        private void OnContentChanged(object sender, EventArgs e)
        {
            UpdatePreview(CaptionInput.GetFormattedText());
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(CaptionPanel, FlyoutPlacementMode.Auto, FlyoutShowMode.Transient, source: EmojiButton);
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                CaptionInput.InsertText(emoji.Value);
                CaptionInput.FocusInput(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                CaptionInput.InsertEmoji(sticker);
                CaptionInput.FocusInput(FocusState.Programmatic);
            }
        }
    }
}
