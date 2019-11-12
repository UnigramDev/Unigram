using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Controls.Cells;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.Controls
{
    public class ChatsListView : GroupedListView
    {
        public ChatsViewModel ViewModel => DataContext as ChatsViewModel;

        public MasterDetailState _viewState;

        public ChatsListView()
        {
            ContainerContentChanging += OnContainerContentChanging;
            RegisterPropertyChangedCallback(SelectionModeProperty, OnSelectionModeChanged);
        }

        #region Selection

        public ListViewSelectionMode SelectionMode2
        {
            get { return (ListViewSelectionMode)GetValue(SelectionMode2Property); }
            set { SetValue(SelectionMode2Property, value); }
        }

        public static readonly DependencyProperty SelectionMode2Property =
            DependencyProperty.Register("SelectionMode2", typeof(ListViewSelectionMode), typeof(ChatsListView), new PropertyMetadata(ListViewSelectionMode.None, OnSelectionModeChanged));

        private static void OnSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatsListView)d).OnSelectionModeChanged((ListViewSelectionMode)e.NewValue, (ListViewSelectionMode)e.OldValue);
        }

        private void OnSelectionModeChanged(ListViewSelectionMode newValue, ListViewSelectionMode oldValue)
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
                    var item = ItemFromContainer(container);
                    content.UpdateViewState(item as Chat, SelectedItem2 == item && SelectionMode2 == ListViewSelectionMode.Single, _viewState == MasterDetailState.Compact, ViewModel.Settings.UseThreeLinesLayout);
                    content.SetSelectionMode(newValue, true);
                }
            }

            //if (newValue != oldValue)
            //{
            //    ViewModel.SelectedItems.Clear();
            //}
        }

        public object SelectedItem2
        {
            get { return (object)GetValue(SelectedItem2Property); }
            set { SetValue(SelectedItem2Property, value); }
        }

        public static readonly DependencyProperty SelectedItem2Property =
            DependencyProperty.Register("SelectedItem2", typeof(object), typeof(ChatsListView), new PropertyMetadata(ListViewSelectionMode.None, OnSelectedItemChanged));

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatsListView)d).OnSelectedItemChanged((object)e.NewValue, (object)e.OldValue);
        }

        private void OnSelectedItemChanged(object newValue, object oldValue)
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
                    var item = ItemFromContainer(container);
                    content.UpdateViewState(item as Chat, SelectedItem2 == item && SelectionMode2 == ListViewSelectionMode.Single, _viewState == MasterDetailState.Compact, ViewModel.Settings.UseThreeLinesLayout);
                    content.SetSelectionMode(SelectionMode2, false);
                }
            }

            //if (newValue != oldValue)
            //{
            //    ViewModel.SelectedItems.Clear();
            //}
        }

        public void SetSelectedItems(IList<Chat> chats)
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
                    content.SetSelectionMode(SelectionMode2, false);
                }
            }
        }

        #endregion

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;

            var content = args.ItemContainer.ContentTemplateRoot as ChatCell;
            if (content != null)
            {
                content.UpdateService(ViewModel.ProtoService, ViewModel);
                content.UpdateViewState(args.Item as Chat, SelectedItem2 == args.Item && SelectionMode2 == ListViewSelectionMode.Single, _viewState == MasterDetailState.Compact, ViewModel.Settings.UseThreeLinesLayout);
                content.UpdateChat(ViewModel.ProtoService, ViewModel, args.Item as Chat);
                content.SetSelectionMode(SelectionMode2, false);
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
                    var item = ItemFromContainer(container);
                    content.UpdateViewState(item as Chat, SelectedItem2 == item && SelectionMode2 == ListViewSelectionMode.Single, _viewState == MasterDetailState.Compact, ViewModel.Settings.UseThreeLinesLayout);
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ChatsListViewItem(this);
        }
    }

    public class ChatsListViewItem : ListViewItem
    {
        private ChatsListView _list;

        public ChatsListViewItem()
        {

        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatsListViewItemAutomationPeer(this);
        }

        public ChatsListViewItem(ChatsListView list)
        {
            _list = list;
            //RegisterPropertyChangedCallback(IsSelectedProperty, OnSelectedChanged);
        }

        private void OnSelectedChanged(DependencyObject sender, DependencyProperty dp)
        {
            var content = ContentTemplateRoot as ChatCell;
            if (content != null)
            {
                content.UpdateViewState(_list.ItemFromContainer(this) as Chat, this.IsSelected && _list.SelectionMode == ListViewSelectionMode.Single, _list._viewState == MasterDetailState.Compact, SettingsService.Current.UseThreeLinesLayout);
            }
        }
    }

    public class ChatsListViewItemAutomationPeer : ListViewItemAutomationPeer
    {
        private ChatsListViewItem _owner;

        public ChatsListViewItemAutomationPeer(ChatsListViewItem owner)
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

    public class ChatsGridViewItem : GridViewItem
    {
        public ChatsGridViewItem()
        {

        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatsGridViewItemAutomationPeer(this);
        }
    }

    public class ChatsGridViewItemAutomationPeer : GridViewItemAutomationPeer
    {
        private ChatsGridViewItem _owner;

        public ChatsGridViewItemAutomationPeer(ChatsGridViewItem owner)
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
