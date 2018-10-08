using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class LazoListView : SelectListView
    {
        private IList<ItemIndexRange> _ranges;
        private ListViewItem _firstItem;
        private int _firstIndex = -1;
        private bool _operation;

        private Point _position;

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new LazoListViewItem(this);
        }

        internal void OnPointerPressed(LazoListViewItem item, PointerRoutedEventArgs e)
        {
        }

        internal void OnPointerEntered(LazoListViewItem item, PointerRoutedEventArgs e)
        {
            if (_firstItem == null || !e.Pointer.IsInContact /*|| SelectionMode != ListViewSelectionMode.Multiple*/ || e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                return;
            }

            var point = e.GetCurrentPoint(item);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;

            if (SelectionMode != ListViewSelectionMode.Multiple)
            {
                SelectionMode = ListViewSelectionMode.Multiple;
            }

            var index = IndexFromContainer(item);
            var first = Math.Min(_firstIndex, index);
            var last = Math.Max(_firstIndex, index);

            if (SelectedItems.Count > 0)
            {
                SelectedItems.Clear();

                foreach (var original in _ranges)
                {
                    SelectRange(original);
                }
            }

            if (_operation)
            {
                SelectRange(new ItemIndexRange(first, (uint)Math.Max(1, last - first + 1)));
            }
            else
            {
                DeselectRange(new ItemIndexRange(first, (uint)Math.Max(1, last - first + 1)));
            }
        }

        internal void OnPointerMoved(LazoListViewItem item, PointerRoutedEventArgs e)
        {
            if ((_firstItem != null && _firstItem != item) || !e.Pointer.IsInContact || e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                return;
            }

            var point = e.GetCurrentPoint(item);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            e.Handled = true;

            if (_firstItem == null)
            {
                _firstItem = item;
                _firstIndex = IndexFromContainer(item);
                _ranges = SelectedRanges.ToArray();
                _operation = !_firstItem.IsSelected;

                _position = point.Position;
            }
            else if (_firstItem == item)
            {
                var child = ItemFromContainer(item);
                if (child == null)
                {
                    return;
                }

                var contains = SelectedItems.Contains(child);

                var delta = Math.Abs(point.Position.Y - _position.Y);
                if (delta > 10)
                {
                    SelectionMode = ListViewSelectionMode.Multiple;

                    if (_operation && !contains)
                    {
                        SelectedItems.Add(child);
                    }
                    else if (!_operation && contains)
                    {
                        SelectedItems.Remove(child);
                    }
                }
                else
                {
                    if (_operation && contains)
                    {
                        SelectedItems.Remove(child);
                    }
                    else if (!_operation && !contains)
                    {
                        SelectedItems.Add(child);
                    }
                }
            }
        }

        internal void OnPointerReleased(LazoListViewItem item, PointerRoutedEventArgs e)
        {
            if (SelectionMode != ListViewSelectionMode.Multiple)
            {
                return;
            }

            e.Handled = _firstItem != null && _firstItem.IsSelected == _operation;

            _firstItem = null;
            _firstIndex = -1;
            _ranges = null;

            _position = new Point();
        }
    }
}
