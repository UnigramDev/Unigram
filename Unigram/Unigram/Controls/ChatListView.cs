using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Controls.Cells;
using Unigram.Controls.Items;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class ChatListView : GroupedListView
    {
        public UnigramViewModelBase ViewModel => DataContext as UnigramViewModelBase;

        public MasterDetailState _viewState;

        public ChatListView()
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

            var content = args.ItemContainer.ContentTemplateRoot as ChatCell;
            if (content != null)
            {
                content.UpdateViewState(args.Item as Telegram.Td.Api.Chat, args.ItemContainer.IsSelected && SelectionMode == ListViewSelectionMode.Single, _viewState == MasterDetailState.Compact);
                content.UpdateChat(ViewModel.ProtoService, args.Item as Telegram.Td.Api.Chat);
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
                    content.UpdateViewState(ItemFromContainer(container) as Telegram.Td.Api.Chat, container.IsSelected && SelectionMode == ListViewSelectionMode.Single, _viewState == MasterDetailState.Compact);
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ChatListViewItem(this);
        }
    }

    public class ChatListViewItem : ListViewItem
    {
        private ChatListView _list;

        public ChatListViewItem()
        {

        }

        public ChatListViewItem(ChatListView list)
        {
            _list = list;
            RegisterPropertyChangedCallback(IsSelectedProperty, OnSelectedChanged);
        }

        private void OnSelectedChanged(DependencyObject sender, DependencyProperty dp)
        {
            var content = ContentTemplateRoot as ChatCell;
            if (content != null)
            {
                content.UpdateViewState(_list.ItemFromContainer(this) as Telegram.Td.Api.Chat, this.IsSelected && _list.SelectionMode == ListViewSelectionMode.Single, _list._viewState == MasterDetailState.Compact);
            }
        }
    }
}
