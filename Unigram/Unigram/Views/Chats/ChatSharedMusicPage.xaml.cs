using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Cells;
using Unigram.Converters;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatSharedMusicPage : ChatSharedMediaPageBase
    {
        public ChatSharedMusicPage()
        {
            InitializeComponent();
            InitializeSearch(Search, () => new SearchMessagesFilterAudio());
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;

            var message = args.Item as Message;
            if (message == null)
            {
                return;
            }

            AutomationProperties.SetName(args.ItemContainer,
                Automation.GetSummary(ViewModel.ProtoService, message, true));

            if (args.ItemContainer.ContentTemplateRoot is SharedAudioCell audioCell)
            {
                audioCell.UpdateMessage(ViewModel.PlaybackService, ViewModel.ProtoService, message);
            }
            else if (message.Content is MessageHeaderDate && args.ItemContainer.ContentTemplateRoot is Border border && border.Child is TextBlock header)
            {
                header.Text = Converter.MonthGrouping(Utils.UnixTimestampToDateTime(message.Date));
            }

            if (args.ItemContainer.ContentTemplateRoot is FrameworkElement element)
            {
                element.Tag = message;
            }
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
