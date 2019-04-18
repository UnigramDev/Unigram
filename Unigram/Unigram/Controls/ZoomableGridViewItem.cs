using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class ZoomableGridViewItem : GridViewItem
    {
        private readonly ZoomableGridView _parent;

        public ZoomableGridViewItem(ZoomableGridView parent)
        {
            _parent = parent;
            IsHoldingEnabled = true;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            _parent.OnPointerPressed(this, e);
            base.OnPointerPressed(e);
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            _parent.OnItemPointerEntered(this, Tag);
            base.OnPointerEntered(e);
        }
    }
}
