using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;
using Unigram.Controls.Cells;
using Unigram.ViewModels;

namespace Unigram.Controls
{
    public class ChatListListView : MultipleListView
    {
        public ChatListViewModel ViewModel => DataContext as ChatListViewModel;

        public MasterDetailState _viewState;

        public ChatListListView()
        {
            ContainerContentChanging += OnContainerContentChanging;
            RegisterPropertyChangedCallback(SelectionModeProperty, OnSelectionModeChanged);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;

            var content = args.ItemContainer.ContentTemplateRoot as ChatCell;
            if (content != null && args.Item is Chat chat)
            {
                content.UpdateService(ViewModel.ProtoService);
                content.UpdateViewState(chat, SelectedItem == args.Item && SelectionMode == ListViewSelectionMode.Single, _viewState == MasterDetailState.Compact);
                content.UpdateChat(ViewModel.ProtoService, chat, ViewModel.Items.ChatList);
                args.Handled = true;
            }
        }

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            UpdateVisibleChats();
        }

        public void UpdateViewState(MasterDetailState state)
        {
            _viewState = state;
            UpdateVisibleChats();
        }

        private void UpdateVisibleChats()
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel == null)
            {
                return;
            }

            for (int i = panel.FirstCacheIndex; i <= panel.LastCacheIndex; i++)
            {
                var container = ContainerFromIndex(i) as ListViewItem;
                if (container == null)
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as ChatCell;
                if (content != null)
                {
                    var item = ItemFromContainer(container) as Chat;
                    if (item == null)
                    {
                        continue;
                    }

                    content.UpdateViewState(item, SelectedItem == item && SelectionMode == ListViewSelectionMode.Single, _viewState == MasterDetailState.Compact);
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ChatListListViewItem(this);
        }
    }

    public class ChatListListViewItem : MultipleListViewItem
    {
        private readonly ChatListListView _list;

        public ChatListListViewItem()
        {
            DefaultStyleKey = typeof(ChatListListViewItem);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListListViewItemAutomationPeer(this);
        }

        public ChatListListViewItem(ChatListListView list)
        {
            DefaultStyleKey = typeof(ChatListListViewItem);

            _list = list;
            RegisterPropertyChangedCallback(IsSelectedProperty, OnSelectedChanged);
        }

        private void OnSelectedChanged(DependencyObject sender, DependencyProperty dp)
        {
            var content = ContentTemplateRoot as ChatCell;
            if (content != null)
            {
                content.UpdateViewState(_list.ItemFromContainer(this) as Chat, IsSelected && _list.SelectionMode == ListViewSelectionMode.Single, _list._viewState == MasterDetailState.Compact);
            }
        }
    }

    public class ChatListListViewItemAutomationPeer : ListViewItemAutomationPeer
    {
        private readonly ChatListListViewItem _owner;

        public ChatListListViewItemAutomationPeer(ChatListListViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            if (_owner.ContentTemplateRoot is ChatCell cell)
            {
                return cell.GetAutomationName() ?? base.GetNameCore();
            }

            return base.GetNameCore();
        }
    }
}
