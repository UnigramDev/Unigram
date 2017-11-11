using LinqToVisualTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class BubbleListView : PaddedListView
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public ScrollViewer ScrollingHost { get; private set; }
        public ItemsStackPanel ItemsStack { get; private set; }

        public BubbleListView()
        {
            DefaultStyleKey = typeof(ListView);

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
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

            ViewModel.Items.CollectionChanged += OnCollectionChanged;

            if (ScrollingHost.ScrollableHeight < 120 && Items.Count > 0)
            {
                if (!ViewModel.IsFirstSliceLoaded)
                {
                    await ViewModel.LoadPreviousSliceAsync(false, ItemsStack.LastVisibleIndex == ItemsStack.LastCacheIndex);
                }

                await ViewModel.LoadNextSliceAsync();
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var groups = new Dictionary<long, GroupedMessages>();

            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is TLMessage message && message.HasGroupedId && message.GroupedId is long groupedId && ViewModel.GroupedItems.TryGetValue(groupedId, out GroupedMessages group))
                    {
                        groups[groupedId] = group;
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is TLMessage message && message.HasGroupedId && message.GroupedId is long groupedId && ViewModel.GroupedItems.TryGetValue(groupedId, out GroupedMessages group))
                    {
                        groups[groupedId] = group;
                    }
                }
            }

            foreach (var group in groups.Values)
            {
                foreach (var message in group.Messages)
                {
                    var container = ContainerFromItem(message) as BubbleListViewItem;
                    if (container == null)
                    {
                        continue;
                    }

                    PrepareContainerForItemOverride(container, message);
                }
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var previousWidth = Math.Min(Math.Max(e.PreviousSize.Width, 320) - 12 - 52, 320);
            var previousHeight = Math.Min(Math.Max(e.PreviousSize.Width, 320) - 12 - 52, 420);

            var newWidth = Math.Min(Math.Max(e.NewSize.Width, 320) - 12 - 52, 320);
            var newHeight = Math.Min(Math.Max(e.NewSize.Width, 320) - 12 - 52, 420);

            if (ItemsStack != null && (newWidth != previousWidth || newHeight != previousHeight))
            {
                for (int i = ItemsStack.FirstCacheIndex; i <= ItemsStack.LastCacheIndex; i++)
                {
                    var container = ContainerFromIndex(i);
                    if (container == null)
                    {
                        continue;
                    }

                    var message = ItemFromContainer(container) as TLMessage;
                    if (message == null)
                    {
                        continue;
                    }

                    if (message.HasGroupedId && message.GroupedId is long groupedId)
                    {
                        PrepareContainerForItemGrouping(container, message, groupedId);
                    }
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
            if (ScrollingHost.ScrollableHeight < 120)
            {
                if (!ViewModel.IsFirstSliceLoaded)
                {
                    await ViewModel.LoadPreviousSliceAsync(false, ItemsStack.LastVisibleIndex == ItemsStack.LastCacheIndex);
                }

                await ViewModel.LoadNextSliceAsync();
            }
        }

        private async void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (ScrollingHost == null || ItemsStack == null || ViewModel == null)
            {
                return;
            }

            if (ScrollingHost.VerticalOffset < 120 && !e.IsIntermediate)
            {
                await ViewModel.LoadNextSliceAsync(true);
            }
            else if (ScrollingHost.ScrollableHeight - ScrollingHost.VerticalOffset < 120 && !e.IsIntermediate)
            {
                if (ViewModel.IsFirstSliceLoaded == false)
                {
                    await ViewModel.LoadPreviousSliceAsync(true, ItemsStack.LastVisibleIndex == ItemsStack.LastCacheIndex);
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

            if (mode == ItemsUpdatingScrollMode.KeepItemsInView && (force || scroll.VerticalOffset < 120))
            {
                Debug.WriteLine("Changed scrolling mode to KeepItemsInView");

                panel.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepItemsInView;
            }
            else if (mode == ItemsUpdatingScrollMode.KeepLastItemInView && (force || scroll.ScrollableHeight - scroll.VerticalOffset < 120))
            {
                Debug.WriteLine("Changed scrolling mode to KeepLastItemInView");

                panel.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new BubbleListViewItem(this);
        }

        protected override bool PrepareContainerForItemGrouping(DependencyObject element, TLMessage message, long groupedId)
        {
            var container = element as BubbleListViewItem;
            if (container == null)
            {
                return false;
            }

            if (ViewModel.GroupedItems.TryGetValue(groupedId, out GroupedMessages group) && 
                group.Positions.TryGetValue(message, out GroupedMessagePosition position) && 
                group.Messages.Count > 1)
            {
                var messageId = message.RandomId ?? message.Id;

                var photo = message.ToId is TLPeerChat || (message.ToId is TLPeerChannel && !message.IsPost);

                var width = Math.Min(Math.Max(ActualWidth, 320) - 12 - 52, 320);
                var height = Math.Min(Math.Max(ActualWidth, 320) - 12 - 52, 420);

                container.HorizontalAlignment = message.IsOut ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                container.Width = position.Width / 700d * width;
                container.Height = position.Height * height;

                container.Width -= 2;
                container.Height -= 2;

                double maxWidth = group.Width;

                double left = group.Width;
                double top = 0;

                for (int i = 0; i < group.Messages.Count; i++)
                {
                    var msg = group.Messages[i];
                    var pos = group.Positions[msg];
                    var msgId = msg.RandomId ?? msg.Id;

                    if (msgId > messageId && pos.MinY == position.MinY && message.IsOut)
                    {
                        left -= pos.Width;
                    }
                    else if (msgId < messageId && pos.MinY == position.MinY && !message.IsOut)
                    {
                        left -= pos.Width;
                    }

                    if (msgId < messageId && pos.MinY == position.MinY)
                    {
                        top = pos.Height * 2 - position.Height;
                    }

                    if (msgId <= messageId && (position.SpanSize == 700 || position.SpanSize == 1000))
                    {
                        if (i == 1)
                        {
                            top = group.Positions[group.Messages[0]].Height * 2 - pos.Height;
                        }
                        else if (i > 1)
                        {
                            top = top - group.Positions[group.Messages[i -1]].Height - pos.Height;
                        }
                    }
                }

                if (position.SpanSize == 700 || position.SpanSize == 1000)
                {
                    left = message.IsOut ? maxWidth : position.Width;
                }

                left = (maxWidth - left) / 700d * width;
                top = message == group.Messages[0] && message.IsFirst ? 6d : -top * height;

                if (message.IsOut)
                {
                    container.Margin = new Thickness(2, 2 + top, 12 + left, position.IsLast ? 2 : 0);
                }
                else
                {
                    container.Margin = new Thickness(photo ? position.IsLast ? 0 : 52 + left : 12 + left, 2 + top, 2, position.IsLast ? 2 : 0);
                }

                if (message == group.Messages[0] && ((message.HasReplyToMsgId && message.ReplyToMsgId.HasValue) || (message.HasFwdFrom && message.FwdFrom != null) || (message.HasViaBotId && message.ViaBotId.HasValue)))
                {
                    var add = message.HasReplyToMsgId && message.ReplyToMsgId.HasValue ? 50 : 26;
                    container.Padding = new Thickness(0, add, 0, 0);
                    container.ContentMargin = new Thickness(0, -add, 0, 0);
                    container.Height += add;
                }
                else if (position.IsLast && photo)
                {
                    var add = left + 52;
                    container.Padding = new Thickness(add, 0, 0, 0);
                    container.ContentMargin = new Thickness(-add, 0, 0, 0);
                    container.Width += add;
                }
                else
                {
                    container.Padding = new Thickness();
                    container.ContentMargin = new Thickness();
                }

                return true;
            }

            return false;
        }
    }
}
