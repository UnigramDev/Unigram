using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Unigram.Common;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class MosaicLayout : VirtualizingLayout
    {
        protected override void InitializeForContextCore(VirtualizingLayoutContext context)
        {
            base.InitializeForContextCore(context);

            if (!(context.LayoutState is MosaicLayoutState state))
            {
                // Store any state we might need since (in theory) the layout could be in use by multiple 
                // elements simultaneously
                // In reality for the Xbox Activity Feed there's probably only a single instance.
                context.LayoutState = _state = new MosaicLayoutState();
            }
        }

        protected override void UninitializeForContextCore(VirtualizingLayoutContext context)
        {
            base.UninitializeForContextCore(context);

            // clear any state
            context.LayoutState = null;
        }

        protected override void OnItemsChangedCore(VirtualizingLayoutContext context, object source, NotifyCollectionChangedEventArgs args)
        {
            var state = context.LayoutState as MosaicLayoutState;
            if (state == null)
            {
                return;
            }

            // TODO: rects should be cleared only on Reset event
            // In all the other cases a new mosaic should be generated
            // only for the changed items, so that layout won't change completely

            state.LayoutRects.Clear();
            base.OnItemsChangedCore(context, source, args);
        }

        private IList GetItems(VirtualizingLayoutContext context)
        {
            var result = new List<object>();

            for (int i = 0; i < context.ItemCount; i++)
            {
                result.Add(context.GetItemAt(i));
            }

            return result;
        }

        // I don't like this, should be implemented by
        // Microsoft and removed from here
        #region 

        private MosaicLayoutState _state;

        public int GetFirstVisibleIndex(ScrollViewer scrollViewer)
        {
            var state = _state;
            if (state == null)
            {
                return -1;
            }

            for (int i = 0; i < _state.LayoutRects.Count; i++)
            {
                var rect = _state.LayoutRects[i];
                if (rect.Top <= scrollViewer.VerticalOffset && rect.Bottom > scrollViewer.VerticalOffset)
                {
                    return i;
                }
            }

            return -1;
        }

        public int GetLastVisibleIndex(ScrollViewer scrollViewer)
        {
            var state = _state;
            if (state == null)
            {
                return -1;
            }

            var height = scrollViewer.ViewportHeight;
            if (state.LayoutRects.Count > 0)
            {
                height = Math.Min(scrollViewer.ViewportHeight, state.LayoutRects.Max(x => x.Bottom));
            }

            for (int i = _state.LayoutRects.Count - 1; i >= 0; i--)
            {
                var rect = _state.LayoutRects[i];
                if (rect.Top < scrollViewer.VerticalOffset + height && rect.Bottom >= scrollViewer.VerticalOffset + height)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion

        protected override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
        {
            if (context.ItemCount < 1)
            {
                return new Size(0, 0);
            }

            // Item size is 96 + 2px of margin

            var state = context.LayoutState as MosaicLayoutState;
            if (state.LayoutRects.Count < 1)
            {
                state.LayoutRects.Clear();
                state.ActualWidth = availableSize.Width;

                var items = GetItems(context);

                var mosaic = MosaicMedia.Calculate(items);
                var top = 0d;

                foreach (var row in mosaic)
                {
                    var left = 0d;

                    foreach (var item in row)
                    {
                        state.LayoutRects.Add(new Rect(left, top, item.Width, 98));
                        left += item.Width;
                    }

                    top += 98;
                }

                state.Rows = mosaic;
                state.ExtentHeight = top - 2;
            }

            if (state.LayoutRects.Count < 1)
            {
                return new Size(0, 0);
            }

            var firstRowIndex = Math.Min(Math.Max((int)(context.RealizationRect.Y / 98) - 1, 0), state.Rows.Count - 1);
            var lastRowIndex = Math.Max(Math.Min((int)(context.RealizationRect.Bottom / 98) + 1, state.Rows.Count - 1), 0);

            var firstItemIndex = state.Rows[firstRowIndex][0].Index;
            var lastItemIndex = state.Rows[lastRowIndex].Last().Index;

            var availableWidth = availableSize.Width + 2;

            for (int i = firstItemIndex; i <= lastItemIndex; i++)
            {
                var container = context.GetOrCreateElementAt(i);
                container.Measure(new Size(
                    state.LayoutRects[i].Width * availableWidth,
                    state.LayoutRects[i].Height));
            }

            state.FirstRealizedIndex = firstItemIndex;
            state.LastRealizedIndex = lastItemIndex;

            // Report this as the desired size for the layout
            return new Size(availableSize.Width, state.ExtentHeight);
        }

        protected override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
        {
            // walk through the cache of containers and arrange
            var state = context.LayoutState as MosaicLayoutState;
            if (state.LayoutRects.Count < 1)
            {
                return finalSize;
            }

            var firstItemIndex = state.FirstRealizedIndex;
            var lastItemIndex = state.LastRealizedIndex;

            var finalWidth = finalSize.Width + 2;

            for (int i = firstItemIndex; i <= lastItemIndex; i++)
            {
                var container = context.GetOrCreateElementAt(i);
                container.Arrange(new Rect(
                    state.LayoutRects[i].X * finalWidth,
                    state.LayoutRects[i].Y,
                    state.LayoutRects[i].Width * finalWidth,
                    state.LayoutRects[i].Height));
            }

            return finalSize;
        }
    }

    internal class MosaicLayoutState
    {
        public IList<MosaicMediaRow> Rows { get; set; }

        public double ExtentHeight { get; set; }

        public double ActualWidth { get; set; }

        public int FirstRealizedIndex { get; set; }
        public int LastRealizedIndex { get; set; }

        /// <summary>
        /// List of layout bounds for items starting with the
        /// FirstRealizedIndex.
        /// </summary>
        public List<Rect> LayoutRects
        {
            get
            {
                if (_layoutRects == null)
                {
                    _layoutRects = new List<Rect>();
                }

                return _layoutRects;
            }
        }

        private List<Rect> _layoutRects;
    }
}
