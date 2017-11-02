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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel != null)
            {
                ItemsStack = panel;
                ItemsStack.ItemsUpdatingScrollMode = UpdatingScrollMode == UpdatingScrollMode.KeepItemsInView || UpdatingScrollMode == UpdatingScrollMode.ForceKeepItemsInView
                    ? ItemsUpdatingScrollMode.KeepItemsInView
                    : ItemsUpdatingScrollMode.KeepLastItemInView;
                ItemsStack.SizeChanged += Panel_SizeChanged;
            }

            ViewModel.Items.CollectionChanged += OnCollectionChanged;
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

        #region UpdatingScrollMode

        public UpdatingScrollMode UpdatingScrollMode
        {
            get { return (UpdatingScrollMode)GetValue(UpdatingScrollModeProperty); }
            set { SetValue(UpdatingScrollModeProperty, value); }
        }

        public static readonly DependencyProperty UpdatingScrollModeProperty =
            DependencyProperty.Register("UpdatingScrollMode", typeof(UpdatingScrollMode), typeof(BubbleListView), new PropertyMetadata(UpdatingScrollMode.KeepItemsInView, OnUpdatingScrollModeChanged));

        private static void OnUpdatingScrollModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as BubbleListView;
            if (sender.ItemsStack != null)
            {
                var mode = (UpdatingScrollMode)e.NewValue;
                if (mode == UpdatingScrollMode.ForceKeepItemsInView)
                {
                    sender.ItemsStack.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepItemsInView;
                }
                else if (mode == UpdatingScrollMode.ForceKeepLastItemInView)
                {
                    sender.ItemsStack.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView;
                }
                else if (mode == UpdatingScrollMode.KeepItemsInView && sender.ScrollingHost.VerticalOffset < 120)
                {
                    sender.ItemsStack.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepItemsInView;
                }
                else if (mode == UpdatingScrollMode.KeepLastItemInView && sender.ScrollingHost.ScrollableHeight - sender.ScrollingHost.VerticalOffset < 120)
                {
                    sender.ItemsStack.ItemsUpdatingScrollMode = ItemsUpdatingScrollMode.KeepLastItemInView;
                }
            }
        }

        #endregion

        private int count;
        protected override DependencyObject GetContainerForItemOverride()
        {
            //Debug.WriteLine($"New listview item: {++count}");
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

                var width = Math.Min(Math.Max(ActualWidth, 320) - 12 - 52, 320);
                var height = Math.Min(Math.Max(ActualWidth, 320) - 12 - 52, 420);

                container.HorizontalAlignment = message.IsOut ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                container.Padding = new Thickness();
                container.Width = position.pw / 700d * width;
                container.Height = position.ph * height;

                container.Width -= 2;
                container.Height -= 2;

                var left = 700d;
                var top = 0d;

                for (int i = 0; i < group.Messages.Count; i++)
                {
                    var msg = group.Messages[i];
                    var pos = group.Positions[msg];
                    var msgId = msg.RandomId ?? msg.Id;

                    if (msgId > messageId && pos.MinY == position.MinY && message.IsOut)
                    {
                        left -= pos.pw;
                    }
                    else if (msgId < messageId && pos.MinY == position.MinY && !message.IsOut)
                    {
                        left -= pos.pw;
                    }

                    if (msgId < messageId && pos.MinY == position.MinY)
                    {
                        top = pos.ph * 2 - position.ph;
                    }

                    if (msgId <= messageId && position.SpanSize == 1000)
                    {
                        if (i == 1)
                        {
                            top = group.Positions[group.Messages[0]].ph * 2 - pos.ph;
                        }
                        else if (i > 1)
                        {
                            top = top - group.Positions[group.Messages[i -1]].ph - pos.ph;
                        }
                    }
                }

                if (position.SpanSize == 1000)
                {
                    left = message.IsOut ? 700d : position.pw;
                }

                left = (700d - left) / 700d * width;
                top = message.IsFirst ? 6d : -top * height;

                if (message.IsOut)
                {
                    container.Margin = new Thickness(2, 2 + top, 12 + left, position.Last ? 2 : 0);
                }
                else
                {
                    container.Margin = new Thickness(12 + left, 2 + top, 2, position.Last ? 2 : 0);
                }

                return true;
            }

            return false;
        }
    }

    public enum UpdatingScrollMode
    {
        KeepItemsInView,
        ForceKeepItemsInView,
        KeepLastItemInView,
        ForceKeepLastItemInView
    }
}
