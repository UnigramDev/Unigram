using LinqToVisualTree;
using System.Linq;
using Telegram.Controls;
using Telegram.Controls.Messages;
using Telegram.Services;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ComposeInfoPopup : ContentPopup
    {
        private IMessageDelegate _delegate;
        private IClientService _clientService;

        private MessageViewModel _preview;

        public ComposeInfoPopup(DialogViewModel viewModel, MessageComposerHeader info)
        {
            InitializeComponent();

            var preview = info.ReplyToMessage;
            var chat = viewModel.ClientService.GetChat(preview.ChatId);

            _delegate = new ChatMessageDelegate(viewModel.ClientService, viewModel.Settings, chat);
            _clientService = viewModel.ClientService;

            _preview = new MessageViewModel(viewModel.ClientService, viewModel.PlaybackService, _delegate, chat, preview.Get());

            if (_preview.IsOutgoing && !_preview.IsChannelPost)
            {
                Message1.Visibility = Visibility.Collapsed;
                Message2.UpdateMessage(_preview);
                Message2.Loaded += Message_Loaded;
            }
            else
            {
                Message2.Visibility = Visibility.Collapsed;
                Message1.UpdateMessage(_preview);
                Message1.Loaded += Message_Loaded;
            }

            BackgroundControl.Update(info.ClientService, null);

            Title = Strings.MessageOptionsReplyTitle;

            PrimaryButtonText = Strings.Save;
            SecondaryButtonText = Strings.Cancel;
        }

        private void Message_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is MessageBubble bubble)
            {
                var formatted = bubble.Descendants<FormattedTextBlock>().FirstOrDefault();
                var block = formatted?.Descendants<RichTextBlock>().FirstOrDefault();

                if (block != null)
                {
                    block.SelectionChanged += OnSelectionChanged;
                }
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is RichTextBlock block)
            {
                PrimaryButtonText = block.SelectedText.Length > 0
                    ? Strings.QuoteSelectedPart
                    : Strings.Save;
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
