using LinqToVisualTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls.Messages;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Chats
{
    public class ChatListView : PaddedListView
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ScrollViewer ScrollingHost { get; private set; }
        public ItemsStackPanel ItemsStack { get; private set; }

        private DisposableMutex _loadMoreLock = new DisposableMutex();

        public ChatListView()
        {
            DefaultStyleKey = typeof(ListView);

            Loaded += OnLoaded;
        }

        public void ScrollToBottom()
        {
            if (ScrollingHost != null)
            {
                ScrollingHost.ChangeView(null, ScrollingHost.ScrollableHeight, null);
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel != null)
            {
                ItemsStack = panel;
                ItemsStack.SizeChanged += Panel_SizeChanged;

                SetScrollMode();
            }

            using (await _loadMoreLock.WaitAsync())
            {
                if (ScrollingHost.ScrollableHeight < 200 && Items.Count > 0)
                {
                    if (ViewModel.IsFirstSliceLoaded != true)
                    {
                        await ViewModel.LoadPreviousSliceAsync(false, true);
                    }

                    await ViewModel.LoadNextSliceAsync(false, true);
                }
            }
        }

        protected override void OnApplyTemplate()
        {
            ScrollingHost = (ScrollViewer)GetTemplateChild("ScrollViewer");
            ScrollingHost.ViewChanged += ScrollingHost_ViewChanged;

            base.OnApplyTemplate();
        }

        private async void Panel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                if (ScrollingHost.ScrollableHeight < 200)
                {
                    if (ViewModel.IsLastSliceLoaded != true)
                    {
                        await ViewModel.LoadNextSliceAsync(false, true);
                    }

                    if (ViewModel.IsFirstSliceLoaded != true)
                    {
                        await ViewModel.LoadPreviousSliceAsync(false, true);
                    }
                }
            }
        }

        private async void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (ScrollingHost == null || ItemsStack == null || ViewModel == null)
            {
                return;
            }

            //if (ScrollingHost.VerticalOffset < 200 && ScrollingHost.ScrollableHeight > 0 && !e.IsIntermediate)
            //if (ItemsStack.FirstCacheIndex == 0 && !e.IsIntermediate)
            using (await _loadMoreLock.WaitAsync())
            {
                if (ItemsStack.FirstVisibleIndex == 0 && !e.IsIntermediate)
                {
                    await ViewModel.LoadNextSliceAsync(true);
                }
                else if (ScrollingHost.ScrollableHeight - ScrollingHost.VerticalOffset < 200 && ScrollingHost.ScrollableHeight > 0 && !e.IsIntermediate)
                {
                    if (ViewModel.IsFirstSliceLoaded != true)
                    {
                        await ViewModel.LoadPreviousSliceAsync(true);
                    }
                    else
                    {
                        SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, true);
                        Logs.Logger.Debug(Logs.LoggerTag.Chat, "Setting scroll mode to KeepLastItemInView");
                    }
                }
            }
        }

        private ItemsUpdatingScrollMode? _pendingMode;
        private bool? _pendingForce;

        public void SetScrollMode()
        {
            if (_pendingMode is ItemsUpdatingScrollMode mode && _pendingForce is bool force)
            {
                _pendingMode = null;
                _pendingForce = null;

                SetScrollMode(mode, force);
            }
        }

        public void SetScrollMode(ItemsUpdatingScrollMode mode, bool force)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel == null)
            {
                _pendingMode = mode;
                _pendingForce = force;

                return;
            }

            var scroll = ScrollingHost;
            if (scroll == null)
            {
                _pendingMode = mode;
                _pendingForce = force;

                return;
            }

            if (mode == ItemsUpdatingScrollMode.KeepItemsInView && (force || scroll.VerticalOffset < 200))
            {
                Debug.WriteLine("Changed scrolling mode to KeepItemsInView");

                panel.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepItemsInView;
            }
            else if (mode == ItemsUpdatingScrollMode.KeepLastItemInView && (force || scroll.ScrollableHeight - scroll.VerticalOffset < 200))
            {
                Debug.WriteLine("Changed scrolling mode to KeepLastItemInView");

                panel.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new ChatListViewItem(this);
        }

        protected override bool CantSelect(object item)
        {
            return item is MessageViewModel message && message.IsService();
        }

        protected override long IdFromContainer(DependencyObject container)
        {
            var item = ItemFromContainer(container) as MessageViewModel;
            if (item != null)
            {
                return item.Id;
            }

            return -1;
        }

        public async Task ScrollToItem(object item, VerticalAlignment alignment, bool highlight, double? pixel = null, ScrollIntoViewAlignment direction = ScrollIntoViewAlignment.Leading)
        {
            var scrollViewer = ScrollingHost;
            if (scrollViewer == null)
            {
                Logs.Logger.Debug(Logs.LoggerTag.Chat, "ScrollingHost == null");

                return;
            }

            // We are going to try two times, as the first one seem to fail sometimes
            // leading the chat to the wrong scrolling position
            var iter = 2;

            var selectorItem = ContainerFromItem(item) as SelectorItem;
            while (selectorItem == null && iter > 0)
            {
                Logs.Logger.Debug(Logs.LoggerTag.Chat, string.Format("selectorItem == null, {0} try", iter + 1));

                // call task-based ScrollIntoViewAsync to realize the item
                await this.ScrollIntoViewAsync(item, direction);

                // this time the item shouldn't be null again
                selectorItem = (SelectorItem)ContainerFromItem(item);
                iter--;
            }

            if (selectorItem == null)
            {
                Logs.Logger.Debug(Logs.LoggerTag.Chat, "selectorItem == null, abort");
                return;
            }

            // calculate the position object in order to know how much to scroll to
            var transform = selectorItem.TransformToVisual((UIElement)scrollViewer.Content);
            var position = transform.TransformPoint(new Point(0, 0));

            if (alignment == VerticalAlignment.Top)
            {
                if (pixel is double adjust)
                {
                    position.Y -= adjust;
                }
            }
            else if (alignment == VerticalAlignment.Center)
            {
                position.Y -= (ActualHeight - selectorItem.ActualHeight) / 2d;
            }
            else if (alignment == VerticalAlignment.Bottom)
            {
                position.Y -= ActualHeight - selectorItem.ActualHeight;

                if (pixel is double adjust)
                {
                    position.Y += adjust;
                }
            }

            // scroll to desired position with animation!
            scrollViewer.ChangeView(position.X, position.Y, null, alignment != VerticalAlignment.Center);

            if (highlight)
            {
                var bubble = selectorItem.Descendants<MessageBubble>().FirstOrDefault() as MessageBubble;
                if (bubble == null)
                {
                    return;
                }

                bubble.Highlight();
            }
        }
    }
}
