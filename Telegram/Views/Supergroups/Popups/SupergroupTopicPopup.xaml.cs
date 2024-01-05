//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Supergroups.Popups
{
    public sealed partial class SupergroupTopicPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public SupergroupTopicPopup(IClientService clientService, ForumTopicInfo topic)
        {
            InitializeComponent();

            _clientService = clientService;
            Title = topic == null ? Strings.NewTopic : Strings.EditTopic;

            PrimaryButtonText = topic == null ? Strings.Create : Strings.Done;
            SecondaryButtonText = Strings.Cancel;

            NameLabel.Text = topic?.Name ?? string.Empty;
            Identity.SetStatus(clientService, topic.Icon);

            Emoji.DataContext = EmojiDrawerViewModel.Create(clientService.SessionId, EmojiDrawerMode.Topics);
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
