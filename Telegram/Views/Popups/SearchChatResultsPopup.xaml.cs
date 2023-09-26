using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Chats;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class SearchChatResultsPopup : ContentPopup
    {
        private readonly SearchChatMessagesCollection _collection;

        public SearchChatResultsPopup(SearchChatMessagesCollection results)
        {
            InitializeComponent();

            _collection = results;

            if (results.TotalCount > 0)
            {
                Title = Locale.Declension(Strings.R.messages, results.TotalCount);
            }
            else
            {
                Title = Strings.MessagesOverview;
            }

            ScrollingHost.ItemsSource = results;

            SecondaryButtonText = Strings.Close;
        }

        public Message SelectedItem { get; private set; }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new AccessibleChatListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            if (args.Item is Message message)
            {
                var content = args.ItemContainer.ContentTemplateRoot as ChatCell;
                if (content == null)
                {
                    return;
                }

                content.UpdateMessage(_collection.ClientService, message);
            }

            args.Handled = true;
        }

        #endregion

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            SelectedItem = e.ClickedItem as Message;
            Hide(ContentDialogResult.Primary);
        }
    }
}
