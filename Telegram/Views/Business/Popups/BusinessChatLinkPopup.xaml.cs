using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Streams;
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

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(viewModel.SessionId);
            CaptionInput.DataContext = viewModel;
            CaptionInput.SetText(chatLink.Text);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(CaptionPanel, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                EmojiFlyout.Hide();

                CaptionInput.InsertText(emoji.Value);
                CaptionInput.Focus(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                EmojiFlyout.Hide();

                CaptionInput.InsertEmoji(sticker);
                CaptionInput.Focus(FocusState.Programmatic);
            }
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            MessageHelper.CopyLink(_chatLink.Link);
        }

        private void More_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(_viewModel.Copy, _chatLink, Strings.Copy, Icons.DocumentCopy);
            //flyout.CreateFlyoutItem(_viewModel.Share, _chatLink, Strings.ShareFile, Icons.Share);
            flyout.CreateFlyoutItem(_viewModel.Rename, _chatLink, Strings.Rename, Icons.Edit);
            flyout.CreateFlyoutItem(_viewModel.Delete, _chatLink, Strings.Delete, Icons.Delete, destructive: true);

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            ToastPopup.Show(Strings.BusinessLinkSaved, new LocalFileSource("ms-appx:///Assets/Toasts/Success.tgs"));

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
