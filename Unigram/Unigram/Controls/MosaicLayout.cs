using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Unigram.Common;
using Windows.Foundation;

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
                context.LayoutState = new MosaicLayoutState();
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

        protected override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
        {
            if (context.ItemCount < 1 || (context.RealizationRect.Width == 0 && context.RealizationRect.Height == 0))
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
                        state.LayoutRects.Add(new Rect(left * availableSize.Width, top, item.Width * availableSize.Width, 98));
                        left += item.Width;
                    }

                    top += 98;
                }

                state.Rows = mosaic;
                state.ExtentHeight = top;
            }

            if (state.LayoutRects.Count < 1)
            {
                return new Size(0, 0);
            }

            var firstRowIndex = Math.Max((int)(context.RealizationRect.Y / 98) - 1, 0);
            var lastRowIndex = Math.Min((int)(context.RealizationRect.Bottom / 98) + 1, state.Rows.Count - 1);

            var firstItemIndex = state.Rows[firstRowIndex][0].Index;
            var lastItemIndex = state.Rows[lastRowIndex].Last().Index;

            for (int i = firstItemIndex; i <= lastItemIndex; i++)
            {
                var container = context.GetOrCreateElementAt(i);
                container.Measure(new Size(state.LayoutRects[i].Width, state.LayoutRects[i].Height));
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

            for (int i = firstItemIndex; i <= lastItemIndex; i++)
            {
                var container = context.GetOrCreateElementAt(i);
                container.Arrange(state.LayoutRects[i]);
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
