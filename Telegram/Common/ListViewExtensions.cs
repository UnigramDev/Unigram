//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Point = Windows.Foundation.Point;

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
            var position = transform.TransformPoint(new Point(0, 0));

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
            try
            {
                scrollViewer.ViewChanged += viewChanged;
                if (scrollViewer.ChangeView(horizontalOffset, verticalOffset, null, disableAnimation))
                {
                    await tcs.Task;
                }
            }
            finally
            {
                scrollViewer.ViewChanged -= viewChanged;
                scrollViewer.LayoutUpdated -= layoutUpdated;
            }
        }

        public static ScrollViewer GetScrollViewer(this ListViewBase listViewBase)
        {
            //if (listViewBase is ChatsListView bubble)
            //{
            //    return bubble.ScrollingHost;
            //}

            return listViewBase.Descendants<ScrollViewer>().FirstOrDefault();
        }

        public static void ScrollToTop(this ListViewBase listViewBase)
        {
            var scrollViewer = GetScrollViewer(listViewBase);
            if (scrollViewer.HorizontalScrollMode != ScrollMode.Disabled)
            {
                scrollViewer?.ChangeView(0, null, null);
            }
            else
            {
                scrollViewer?.ChangeView(null, 0, null);
            }
        }
    }
}
