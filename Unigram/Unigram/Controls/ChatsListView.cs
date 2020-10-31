using LinqToVisualTree;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Controls.Cells;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class ChatsListView : SelectListView
    {
        public ChatsViewModel ViewModel => DataContext as ChatsViewModel;

        public MasterDetailState _viewState;

        public ChatsListView()
        {
            ContainerContentChanging += OnContainerContentChanging;
            RegisterPropertyChangedCallback(SelectionModeProperty, OnSelectionModeChanged);

            //DragEnter += OnDragEnter;
            //DragOver += OnDragOver;
            //Drop += OnDrop;
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

        public void SetSelectedItems()
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
            if (content != null && args.Item is Chat chat)
            {
                content.UpdateService(ViewModel.ProtoService, ViewModel);
                content.UpdateViewState(chat, SelectedItem2 == args.Item && SelectionMode2 == ListViewSelectionMode.Single, _viewState == MasterDetailState.Compact, ViewModel.Settings.UseThreeLinesLayout);
                content.UpdateChat(ViewModel.ProtoService, ViewModel, chat, ViewModel.Items.ChatList);
                content.SetSelectionMode(SelectionMode2, false);
                args.Handled = true;
            }
        }

        private ObservableCollection<Chat> _rows;
        private ListViewItem _currentContainer;
        private object _currentItem;
        private int _currentIndex;
        private int _originalIndex;
        private double _drag;

        private void Content_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            args.AllowedOperations = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

            var container = sender.Ancestors<ListViewItem>().FirstOrDefault() as ListViewItem;
            if (container == null)
            {
                return;
            }

            container.RenderTransform = new TranslateTransform();

            _currentContainer = container;
            _currentItem = container.Tag;
            _currentIndex = IndexFromContainer(container);
            _originalIndex = _currentIndex;
            _drag = 0;

            _rows = new ObservableCollection<Chat>(ViewModel.Items.ToList());
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            for (int i = 0; i < 5; i++)
            {
                var container = ContainerFromIndex(i) as ListViewItem;
                if (container != null)
                {
                    container.RenderTransform = null;
                    Canvas.SetZIndex(container, 0);

                    var visual = ElementCompositionPreview.GetElementVisual((ListViewItemPresenter)VisualTreeHelper.GetChild(container, 0));
                    visual.StopAnimation("Offset");
                    visual.Offset = Vector3.Zero;
                }
            }

            if (_currentContainer == null) return;

            //var position = e.GetPosition(this);

            //var indexFloat = (position.Y - 48) / _currentContainer.ActualHeight;
            //var indexDelta = indexFloat - Math.Truncate(indexFloat);

            ////Debug.WriteLine($"Drop, Index: {(int)Math.Truncate(indexFloat)}, Delta: {indexDelta}");

            //var index = (int)Math.Max(0, Math.Min(_rows.Count - 1, Math.Truncate(indexFloat)));
            if (_currentIndex != _originalIndex)
            {
                var source = ItemsSource as IList;
                if (source != null)
                {
                    var item = source[_originalIndex];
                    source.RemoveAt(_originalIndex);
                    source.Insert(_currentIndex, item);

                    //await ViewModel.Dialogs.UpdatePinnedItemsAsync();
                }
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (_currentContainer == null) return;

            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
            e.DragUIOverride.IsContentVisible = false;
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

            var position = e.GetPosition(this);

            var indexFloat = (position.Y - 48) / _currentContainer.ActualHeight;
            var indexDelta = indexFloat - Math.Truncate(indexFloat);

            //Debug.WriteLine($"Over, Index: {(int)Math.Truncate(indexFloat)}, Delta: {indexDelta}");

            var index = (int)Math.Max(0, Math.Min(_rows.Count - 1, Math.Truncate(indexFloat)));
            if (index != _currentIndex)
            {
                var item = _rows[index];

                var container = ContainerFromItem(item) as ListViewItem;
                if (container != null)
                {
                    var original = IndexFromContainer(container);

                    var delta = position.Y - _drag;
                    var drag = 0;

                    if (delta < 0 && indexDelta < 0.5) drag = _currentIndex > original ? 1 : 0;
                    else if (delta > 0 && indexDelta > 0.5) drag = _currentIndex < original ? -1 : 0;

                    _rows.Move(index, original + drag);
                    _currentIndex = _rows.IndexOf(_currentItem as Chat);

                    var anim = drag == 1 ? container.ActualHeight : drag == -1 ? -container.ActualHeight : 0;
                    var visual = ElementCompositionPreview.GetElementVisual((ListViewItemPresenter)VisualTreeHelper.GetChild(container, 0));
                    var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
                    animation.InsertKeyFrame(1, new Vector3(0, (float)anim, 0));
                    visual.StartAnimation("Offset", animation);
                }
            }

            var translate = _currentContainer.RenderTransform as TranslateTransform;
            translate.Y += position.Y - _drag;

            _drag = position.Y;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (_currentContainer == null) return;

            _drag = e.GetPosition(this).Y;

            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;
            e.DragUIOverride.IsContentVisible = false;
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

            e.Handled = true;
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
    }

    public class ChatsListViewItem : ListViewItem
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatsListViewItemAutomationPeer(this);
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
}
