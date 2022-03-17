using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Cells;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatSharedLinksPage : ChatSharedMediaPageBase
    {
        public ChatSharedLinksPage()
        {
            InitializeComponent();
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

            if (args.ItemContainer.ContentTemplateRoot is SharedLinkCell linkCell)
            {
                linkCell.UpdateMessage(ViewModel.ProtoService, ViewModel.NavigationService, message);
                linkCell.Tag = message;
            }
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
