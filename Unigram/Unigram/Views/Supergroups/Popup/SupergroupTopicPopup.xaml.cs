//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels.Drawers;

namespace Unigram.Views.Supergroups.Popup
{
    public sealed partial class SupergroupTopicPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public SupergroupTopicPopup(IClientService clientService, ForumTopicInfo topic)
        {
            InitializeComponent();

            _clientService = clientService;
            Title = topic == null ? Strings.Resources.NewTopic : Strings.Resources.EditTopic;

            PrimaryButtonText = topic == null ? Strings.Resources.Create : Strings.Resources.Done;
            SecondaryButtonText = Strings.Resources.Cancel;

            NameLabel.Text = topic?.Name ?? string.Empty;
            Identity.SetStatus(clientService, topic.Icon);

            var viewModel = EmojiDrawerViewModel.GetForSession(clientService.SessionId, EmojiDrawerMode.CustomEmojis);
            viewModel.UpdateTopics();

            Emoji.DataContext = viewModel;
            Emoji.ItemClick += OnItemClick;

            SelectedEmojiId = topic?.Icon.CustomEmojiId ?? 0;
        }

        public string SelectedName => NameLabel.Text;
        public long SelectedEmojiId { get; private set; }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel sticker && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                SelectedEmojiId = customEmoji.CustomEmojiId;
                Identity.SetStatus(_clientService, new ForumTopicIcon(0, customEmoji.CustomEmojiId));
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
