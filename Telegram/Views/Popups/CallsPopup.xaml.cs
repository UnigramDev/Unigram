using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class CallsPopup : ContentPopup
    {
        public CallsViewModel ViewModel => DataContext as CallsViewModel;

        public CallsPopup()
        {
            InitializeComponent();
            Title = Strings.Calls;
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is CallCell content && args.Item is TLCallGroup call)
            {
                content.UpdateCall(ViewModel.ClientService, call);
                args.Handled = true;
            }
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var call = ScrollingHost.ItemFromContainer(sender) as TLCallGroup;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.DeleteCall, call, Strings.Delete, Icons.Delete, destructive: true);
            flyout.ShowAt(sender, args);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLCallGroup call)
            {
                Hide();
                ViewModel.NavigationService.NavigateToChat(call.ChatId, call.Message?.Id);
            }
        }
    }
}
