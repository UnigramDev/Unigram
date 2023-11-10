using LinqToVisualTree;
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Controls.Messages.Content;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ComposeWebPagePopup : ContentPopup
    {
        private IMessageDelegate _delegate;
        private IClientService _clientService;

        private MessageViewModel _preview;

        public ComposeWebPagePopup(DialogViewModel viewModel, MessageComposerHeader info, Message preview)
        {
            InitializeComponent();

            var chat = viewModel.ClientService.GetChat(preview.ChatId);

            _delegate = new ChatMessageDelegate(viewModel.ClientService, viewModel.Settings, chat);
            _clientService = viewModel.ClientService;

            _preview = new MessageViewModel(viewModel.ClientService, viewModel.PlaybackService, _delegate, chat, preview);
            UpdatePreview(true);

            BackgroundControl.Update(info.ClientService, null);

            Title = Strings.MessageOptionsReplyTitle;

            PrimaryButtonText = Strings.Save;
            SecondaryButtonText = Strings.Cancel;
        }

        private void UpdateButtons(MessageText text)
        {
            Move.Content = text.WebPage.ShowAboveText
                ? Strings.LinkBelow
                : Strings.LinkAbove;

            Move.Glyph = text.WebPage.ShowAboveText
                ? Icons.MoveDown
                : Icons.MoveUp;

            Resize.Content = text.LinkPreviewOptions.ForceLargeMedia
                ? Strings.LinkMediaSmaller
                : Strings.LinkMediaLarger;

            Resize.Glyph = text.LinkPreviewOptions.ForceLargeMedia
                ? Icons.Shrink
                : Icons.Enlarge;

            Resize.Visibility = text.WebPage.HasLargeMedia
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void Message_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is MessageBubble bubble)
            {
                bubble.Loaded += Message_Loaded;

                var formatted = bubble.Descendants<FormattedTextBlock>().FirstOrDefault();
                if (formatted != null)
                {
                    formatted.TextEntityClick += Formatted_TextEntityClick;
                }
            }
        }

        private async void Formatted_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (e.Type is TextEntityTypeUrl && sender is FormattedTextBlock block && _preview.Content is MessageText text)
            {
                block.SetQuery((string)e.Data);

                var bubble = _preview.IsOutgoing && !_preview.IsChannelPost
                    ? Message2
                    : Message1;

                var preview = bubble.Descendants<WebPageContent>().FirstOrDefault();
                preview?.ShowSkeleton();

                var options = new LinkPreviewOptions(false, (string)e.Data, text.LinkPreviewOptions.ForceSmallMedia, text.LinkPreviewOptions.ForceLargeMedia, text.WebPage.ShowAboveText);

                var response = await _clientService.SendAsync(new GetWebPagePreview(new FormattedText((string)e.Data, Array.Empty<TextEntity>()), options));
                if (response is WebPage webPage)
                {
                    webPage.ShowAboveText = text.WebPage.ShowAboveText;

                    text.WebPage = webPage;
                    text.LinkPreviewOptions = options;
                }

                UpdatePreview(false);
                preview?.HideSkeleton();
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Move_Click(object sender, RoutedEventArgs e)
        {
            if (_preview.Content is MessageText text)
            {
                text.WebPage.ShowAboveText = !text.WebPage.ShowAboveText;
            }

            UpdatePreview(true);
        }

        private void Resize_Click(object sender, RoutedEventArgs e)
        {
            if (_preview.Content is MessageText text)
            {
                text.LinkPreviewOptions.ForceLargeMedia = !text.LinkPreviewOptions.ForceLargeMedia;
                text.LinkPreviewOptions.ForceSmallMedia = !text.LinkPreviewOptions.ForceSmallMedia;

                text.WebPage.ShowLargeMedia = text.LinkPreviewOptions.ForceLargeMedia;
            }

            UpdatePreview(false);
        }

        private async void UpdatePreview(bool scroll)
        {
            if (_preview.Content is MessageText text)
            {
                text.LinkPreviewOptions ??= new LinkPreviewOptions();

                var bubble = _preview.IsOutgoing && !_preview.IsChannelPost
                    ? Message2
                    : Message1;

                var url = text.LinkPreviewOptions?.Url;
                if (string.IsNullOrEmpty(url))
                {
                    var entity = text.Text.Entities.FirstOrDefault(x => x.Type is TextEntityTypeUrl or TextEntityTypeTextUrl);
                    if (entity == null)
                    {
                        return;
                    }

                    url = text.Text.Text.Substring(entity.Offset, entity.Length);
                }

                bubble.Visibility = Visibility.Visible;
                bubble.Loaded -= Message_Loaded;
                bubble.Loaded += Message_Loaded;

                bubble.UpdateMessage(_preview);
                bubble.UpdateQuery(url);

                UpdateButtons(text);

                if (scroll)
                {
                    await bubble.UpdateLayoutAsync();

                    ScrollingHost.VerticalAnchorRatio = text.WebPage.ShowAboveText ? 0 : 1;
                    ScrollingHost.ChangeView(null, text.WebPage.ShowAboveText ? 0 : ScrollingHost.ScrollableHeight, null);
                }
            }
        }
    }
}
