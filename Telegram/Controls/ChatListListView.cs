//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls
{
    public class ChatListListView : TopNavView
    {
        public ChatListViewModel ViewModel => DataContext as ChatListViewModel;

        public MasterDetailState _viewState;

        private readonly Dictionary<long, SelectorItem> _itemToSelector = new();

        public ChatListListView()
        {
            DefaultStyleKey = typeof(ListView);

            Unloaded += OnUnloaded;
            ContainerContentChanging += OnContainerContentChanging;

            RegisterPropertyChangedCallback(SelectionModeProperty, OnSelectionModeChanged);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _itemToSelector.Clear();
        }

        public bool TryGetChatAndCell(long chatId, out Chat chat, out ChatCell cell)
        {
            if (_itemToSelector.TryGetValue(chatId, out SelectorItem container))
            {
                chat = container.Tag as Chat;
                cell = container.ContentTemplateRoot as ChatCell;
                return chat != null && cell != null;
            }

            chat = null;
            cell = null;
            return false;
        }

        public bool TryGetCell(Chat chat, out ChatCell cell)
        {
            if (_itemToSelector.TryGetValue(chat.Id, out SelectorItem container))
            {
                cell = container.ContentTemplateRoot as ChatCell;
                return cell != null;
            }

            cell = null;
            return false;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is not Chat chat)
            {
                return;
            }

            if (args.InRecycleQueue)
            {
                _itemToSelector.Remove(chat.Id);
                return;
            }

            _itemToSelector[chat.Id] = args.ItemContainer;
            args.ItemContainer.Tag = args.Item;

            if (args.Phase == 0)
            {
                VisualStateManager.GoToState(args.ItemContainer, "DataPlaceholder", false);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
                args.ItemContainer.ContentTemplateRoot.Opacity = 0;
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is ChatCell content)
            {
                content.UpdateViewState(chat, _viewState == MasterDetailState.Compact, false);
                content.UpdateChat(ViewModel.ClientService, chat, ViewModel.Items.ChatList);
                content.Opacity = 1;
                args.Handled = true;
            }

            VisualStateManager.GoToState(args.ItemContainer, "DataAvailable", false);
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

        public void UpdateVisibleChats()
        {
            // TODO: supposedly, _itemToSelector should only contain cached items
            foreach (var item in _itemToSelector)
            {
                if (item.Value.ContentTemplateRoot is ChatCell chatView && ViewModel.ClientService.TryGetChat(item.Key, out Chat chat))
                {
                    chatView.UpdateViewState(chat, _viewState == MasterDetailState.Compact, true);
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ChatListListViewItem(this);
        }
    }

    public class ChatListListViewItem : TopNavViewItem
    {
        private readonly ChatListListView _list;

        private readonly bool _multi;
        private bool _selected;

        public ChatListListViewItem()
        {
            _multi = true;
            DefaultStyleKey = typeof(ChatListListViewItem);
        }

        public bool IsSingle => !_multi;

        public void UpdateState(bool selected)
        {
            if (_selected == selected)
            {
                return;
            }

            if (ContentTemplateRoot is IMultipleElement test)
            {
                _selected = selected;
                test.UpdateState(selected, true, _list.SelectionMode == ListViewSelectionMode.Multiple);
            }
        }


        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatListListViewItemAutomationPeer(this);
        }

        public ChatListListViewItem(ChatListListView list)
        {
            DefaultStyleKey = typeof(ChatListListViewItem);

            _multi = true;
            _list = list;
            RegisterPropertyChangedCallback(IsSelectedProperty, OnSelectedChanged);
        }

        private void OnSelectedChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (ContentTemplateRoot is ChatCell content)
            {
                content?.UpdateViewState(_list.ItemFromContainer(this) as Chat, _list._viewState == MasterDetailState.Compact, false);
            }
        }
    }

    public class ChatListVisualStateManager : VisualStateManager
    {
        private bool _multi;

        protected override bool GoToStateCore(Control control, FrameworkElement templateRoot, string stateName, VisualStateGroup group, VisualState state, bool useTransitions)
        {
            var selector = control as ChatListListViewItem;
            if (selector == null)
            {
                return false;
            }

            if (group.Name == "MultiSelectStates")
            {
                _multi = stateName == "MultiSelectEnabled";
                selector.UpdateState((_multi || selector.IsSingle) && selector.IsSelected);
            }
            else if ((_multi || selector.IsSingle) && stateName.EndsWith("Selected"))
            {
                stateName = stateName.Replace("Selected", string.Empty);

                if (string.IsNullOrEmpty(stateName))
                {
                    stateName = "Normal";
                }
            }

            return base.GoToStateCore(control, templateRoot, stateName, group, state, useTransitions);
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
