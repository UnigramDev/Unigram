//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Business.Popups
{
    public sealed partial class BusinessChatLinkPopup : ContentPopup
    {
        private readonly BusinessChatLinksViewModel _viewModel;
        private readonly BusinessChatLink _chatLink;

        public BusinessChatLinkPopup(BusinessChatLinksViewModel viewModel, BusinessChatLink chatLink)
        {
            InitializeComponent();

            _viewModel = viewModel;
            _chatLink = chatLink;

            Title = string.IsNullOrEmpty(chatLink.Title)
                ? Strings.BusinessLink
                : chatLink.Title;
            Subtitle.Text = chatLink.Link.Replace("https://", string.Empty);

            BackgroundControl.Update(viewModel.ClientService, viewModel.Aggregator);

            LinkButton.Text = chatLink.Link.Replace("https://", string.Empty);

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(viewModel.Session);
            CaptionInput.DataContext = viewModel;
            CaptionInput.CustomEmoji = CustomEmoji;
            CaptionInput.SetText(chatLink.Text);
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(CaptionPanel, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                CaptionInput.InsertText(emoji.Value);
                CaptionInput.Focus(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                CaptionInput.InsertEmoji(sticker);
                CaptionInput.Focus(FocusState.Programmatic);
            }
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            MessageHelper.CopyLink(XamlRoot, _chatLink.Link);
        }

        private void More_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(_viewModel.Copy, _chatLink, Strings.Copy, Icons.Copy);
            //flyout.CreateFlyoutItem(_viewModel.Share, _chatLink, Strings.ShareFile, Icons.Share);
            flyout.CreateFlyoutItem(_viewModel.Rename, _chatLink, Strings.Rename, Icons.Edit);
            flyout.CreateFlyoutItem(_viewModel.Delete, _chatLink, Strings.Delete, Icons.Delete, destructive: true);

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            ToastPopup.Show(XamlRoot, Strings.BusinessLinkSaved, ToastPopupIcon.Success);

            var text = CaptionInput.GetFormattedText();

            _chatLink.Text = text;

            _viewModel.Delegate?.UpdateBusinessChatLink(_chatLink);

            var response = await _viewModel.ClientService.SendAsync(new EditBusinessChatLink(_chatLink.Link, new InputBusinessChatLink(text, _chatLink.Title)));
            if (response is BusinessChatLink chatLink)
            {
                _chatLink.Title = chatLink.Title;
                _chatLink.Text = chatLink.Text;

                _viewModel.Delegate?.UpdateBusinessChatLink(_chatLink);
            }
        }
    }
}
