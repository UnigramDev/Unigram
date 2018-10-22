using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class LazoListView : SelectListView
    {
        private IList<ItemIndexRange> _ranges;
        private object _firstItem;
        private bool _operation;

        private bool _pressed;
        private Point _position;

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new LazoListViewItem(this);
        }

        internal void OnPointerPressed(LazoListViewItem item, PointerRoutedEventArgs e)
        {
            _pressed = true;
        }

        internal void OnPointerEntered(LazoListViewItem item, PointerRoutedEventArgs e)
        {
            if (_firstItem == null || !_pressed || !e.Pointer.IsInContact /*|| SelectionMode != ListViewSelectionMode.Multiple*/ || e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
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

            var begin = Items.IndexOf(_firstItem);
            if (begin < 0)
            {
                return;
            }

            var index = IndexFromContainer(item);
            var first = Math.Min(begin, index);
            var last = Math.Max(begin, index);

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

                for (int i = first; i <= last; i++)
                {
                    if (CantSelect(Items[i]))
                    {
                        SelectedItems.Remove(Items[i]);
                    }
                }
            }
            else
            {
                DeselectRange(new ItemIndexRange(first, (uint)Math.Max(1, last - first + 1)));
            }
        }

        internal void OnPointerMoved(LazoListViewItem item, PointerRoutedEventArgs e)
        {
            var child = ItemFromContainer(item);
            if (child == null)
            {
                return;
            }

            if ((_firstItem != null && _firstItem != child) || !_pressed || !e.Pointer.IsInContact || e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Mouse)
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
                _firstItem = ItemFromContainer(item);
                _ranges = SelectedRanges.ToArray();
                _operation = !item.IsSelected;

                _position = point.Position;
            }
            else if (_firstItem == child)
            {
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
            var first = _firstItem != null ? ContainerFromItem(_firstItem) as SelectorItem : null;
            var handled = first != null && first.IsSelected == _operation;

            _firstItem = null;
            _ranges = null;

            _pressed = false;
            _position = new Point();

            if (SelectionMode != ListViewSelectionMode.Multiple)
            {
                return;
            }

            e.Handled = handled;
        }

        protected virtual bool CantSelect(object item)
        {
            return false;
        }

        protected virtual long IdFromContainer(DependencyObject containter)
        {
            return IndexFromContainer(containter);
        }
    }
}
