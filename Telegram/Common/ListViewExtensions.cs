//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Common
{
    public static class ListViewExtensions
    {
        public static async Task ScrollToItem2(this ListViewBase listView, object item, VerticalAlignment alignment, ScrollIntoViewAlignment direction = ScrollIntoViewAlignment.Default, bool? disableAnimation = null)
        {
            var scrollViewer = listView.GetScrollViewer();
            if (scrollViewer == null)
            {
                return;
            }

            if (listView.ItemsPanelRoot is ItemsStackPanel or ItemsWrapGrid)
            {
                await ScrollIntoViewAsync(listView, scrollViewer, item, direction);
            }

            var selectorItem = listView.ContainerFromItem(item) as SelectorItem;
            if (selectorItem == null)
            {
                return;
            }

            // calculate the position object in order to know how much to scroll to
            var transform = selectorItem.TransformToVisual((UIElement)scrollViewer.Content);
            var position = transform.TransformPoint(new Point());

            if (scrollViewer.VerticalScrollMode == ScrollMode.Disabled)
            {
                if (alignment == VerticalAlignment.Center)
                {
                    if (selectorItem.ActualWidth < listView.ActualWidth)
                    {
                        position.X -= (listView.ActualWidth - selectorItem.ActualWidth) / 2d;
                    }
                }
                else if (alignment == VerticalAlignment.Bottom)
                {
                    position.X -= listView.ActualWidth - selectorItem.ActualWidth;
                }

                if (scrollViewer.HorizontalOffset < scrollViewer.ScrollableWidth || position.X < scrollViewer.ScrollableWidth)
                {
                    if (scrollViewer.HorizontalOffset.AlmostEquals(position.X))
                    {
                        return;
                    }

                    await scrollViewer.ChangeViewAsync(position.X, null, disableAnimation ?? alignment != VerticalAlignment.Center, false);
                }
            }
            else
            {
                if (alignment == VerticalAlignment.Center)
                {
                    if (selectorItem.ActualHeight < listView.ActualHeight)
                    {
                        position.Y -= (listView.ActualHeight - selectorItem.ActualHeight) / 2d;
                    }
                }
                else if (alignment == VerticalAlignment.Bottom)
                {
                    position.Y -= listView.ActualHeight - selectorItem.ActualHeight;
                }

                if (scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight || position.Y < scrollViewer.ScrollableHeight)
                {
                    if (scrollViewer.VerticalOffset.AlmostEquals(position.Y))
                    {
                        return;
                    }

                    await scrollViewer.ChangeViewAsync(null, position.Y, disableAnimation ?? alignment != VerticalAlignment.Center, false);
                }
            }
        }

        private static async Task ScrollIntoViewAsync(this ListViewBase listView, ScrollViewer scrollViewer, object item, ScrollIntoViewAlignment alignment)
        {
            var index = listView.Items.IndexOf(item);
            //var stack = await _waitItemsPanelRoot.Task;

            if (listView.ItemsPanelRoot is ItemsStackPanel stack)
            {
                if (index >= stack.FirstCacheIndex && index <= stack.LastCacheIndex)
                {
                    return;
                }
            }

            var tcs = new TaskCompletionSource<object>();

            void layoutUpdated(object s1, object e1)
            {
                tcs.TrySetResult(null);
            }

            void viewChanged(object s1, ScrollViewerViewChangedEventArgs e1)
            {
                listView.LayoutUpdated -= layoutUpdated;

                if (e1.IsIntermediate is false)
                {
                    listView.LayoutUpdated += layoutUpdated;
                    scrollViewer.ViewChanged -= viewChanged;
                }
            }

            try
            {
                listView.ScrollIntoView(item, alignment);
                listView.LayoutUpdated += layoutUpdated;
                scrollViewer.ViewChanged += viewChanged;

                await tcs.Task;
            }
            finally
            {
                listView.LayoutUpdated -= layoutUpdated;
                scrollViewer.ViewChanged -= viewChanged;
            }
        }

        public static async Task ChangeViewAsync(this ScrollViewer scrollViewer, double? horizontalOffset, double? verticalOffset, bool disableAnimation, bool updateLayout)
        {
            var tcs = new TaskCompletionSource<bool>();

            void layoutUpdated(object s1, object e1)
            {
                tcs.TrySetResult(true);
            }

            void viewChanged(object s, ScrollViewerViewChangedEventArgs e)
            {
                if (e.IsIntermediate)
                {
                    return;
                }

                scrollViewer.LayoutUpdated += layoutUpdated;

                if (updateLayout)
                {
                    scrollViewer.UpdateLayout();
                }
            }

            scrollViewer.ViewChanged += viewChanged;
            if (scrollViewer.TryChangeView(horizontalOffset, verticalOffset, null, disableAnimation))
            {
                await tcs.Task;
            }
            scrollViewer.ViewChanged -= viewChanged;
            scrollViewer.LayoutUpdated -= layoutUpdated;
        }

        public static async Task WaitForViewChangedAsync(this ScrollViewer scrollViewer, bool updateLayout)
        {
            var tcs = new TaskCompletionSource<bool>();

            void layoutUpdated(object s1, object e1)
            {
                tcs.TrySetResult(true);
            }

            void viewChanged(object s, ScrollViewerViewChangedEventArgs e)
            {
                if (e.IsIntermediate)
                {
                    return;
                }

                scrollViewer.LayoutUpdated += layoutUpdated;

                if (updateLayout)
                {
                    scrollViewer.UpdateLayout();
                }
            }

            scrollViewer.ViewChanged += viewChanged;
            await tcs.Task;
        }

        public static ScrollViewer GetScrollViewer(this ListViewBase listViewBase)
        {
            //if (listViewBase is ChatsListView bubble)
            //{
            //    return bubble.ScrollingHost;
            //}

            return listViewBase.GetChild<ScrollViewer>();
        }

        public static bool ChangeView(this ListViewBase listViewBase, double? horizontalOffset, double? verticalOffset, float? zoomFactor, bool disableAnimation = false)
        {
            var scrollViewer = listViewBase.GetScrollViewer();
            if (scrollViewer != null)
            {
                return scrollViewer.TryChangeView(horizontalOffset, verticalOffset, zoomFactor, disableAnimation);
            }

            return false;
        }

        public static void SetVerticalPadding(this ScrollViewer scrollViewer, double top, double bottom)
        {
            var scrollBar = scrollViewer?.GetLastChild<ScrollBar>(x => x.Orientation == Orientation.Vertical);
            scrollBar?.Margin = new Thickness(0, top, 0, bottom);
        }

        public static void ScrollToTop(this ListViewBase listViewBase)
        {
            var scrollViewer = GetScrollViewer(listViewBase);
            if (scrollViewer != null && scrollViewer.HorizontalScrollMode != ScrollMode.Disabled)
            {
                scrollViewer?.TryChangeView(0, null, null);
            }
            else
            {
                scrollViewer?.TryChangeView(null, 0, null);
            }
        }

        /// <summary>
        /// Where a list is scrolled, so that the position survives the list being given a
        /// different <see cref="ItemsControl.ItemsSource"/>.
        /// </summary>
        /// <remarks>
        /// Not <see cref="ListViewPersistenceHelper"/>, which anchors on the key of the top item
        /// and would be the better answer to virtualization. That one is built for restoring onto
        /// a list that has settled - the platform sample calls it from Loaded - and it has no
        /// anchor to find on a list whose source was replaced a moment ago and not yet measured,
        /// so it leaves the list at the top. An offset applied after an explicit layout pass is
        /// what actually holds the position here.
        /// </remarks>
        public static ScrollPosition SaveScrollPosition(this ListViewBase listViewBase)
        {
            var scrollViewer = GetScrollViewer(listViewBase);
            if (scrollViewer == null)
            {
                return default;
            }

            return new ScrollPosition(scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset);
        }

        /// <summary>
        /// Puts back a position taken by <see cref="SaveScrollPosition"/>. The list has to be
        /// showing the items that position was taken against.
        /// </summary>
        public static void RestoreScrollPosition(this ListViewBase listViewBase, ScrollPosition position)
        {
            if (position.IsEmpty)
            {
                return;
            }

            var scrollViewer = GetScrollViewer(listViewBase);
            if (scrollViewer == null)
            {
                return;
            }

            // An offset means nothing until the list has measured what it is showing now.
            listViewBase.UpdateLayout();

            if (position.Apply(scrollViewer))
            {
                return;
            }

            // Came up short: a virtualized extent is an estimate from the containers realized so
            // far, and a deep offset can outrun it. One more go once the list has grown, detached
            // either way so a list that never does is not left holding a handler. Named rather
            // than inline so that -= matches what += added.
            void layoutUpdated(object sender, object e)
            {
                listViewBase.LayoutUpdated -= layoutUpdated;
                position.Apply(scrollViewer);
            }

            listViewBase.LayoutUpdated += layoutUpdated;
        }
    }

    public readonly struct ScrollPosition
    {
        private readonly bool _hasValue;

        internal ScrollPosition(double horizontalOffset, double verticalOffset)
        {
            HorizontalOffset = horizontalOffset;
            VerticalOffset = verticalOffset;
            _hasValue = true;
        }

        /// <summary>The top of a list, for a caller that has no saved place to go back to.</summary>
        public static ScrollPosition Top { get; } = new ScrollPosition(0, 0);

        public double HorizontalOffset { get; }

        public double VerticalOffset { get; }

        /// <summary>
        /// True for a default instance: nothing was saved, so there is nothing to put back.
        /// Restoring one does nothing rather than scrolling to the top, which is where a list
        /// nobody saved a position for already is.
        /// </summary>
        public bool IsEmpty => !_hasValue;

        // False when the view came up short of what was asked for.
        internal bool Apply(ScrollViewer scrollViewer)
        {
            scrollViewer.TryChangeView(HorizontalOffset, VerticalOffset, null, true);

            return scrollViewer.VerticalOffset.AlmostEquals(VerticalOffset)
                && scrollViewer.HorizontalOffset.AlmostEquals(HorizontalOffset);
        }
    }
}
