using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        protected override void OnHolding(HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                _parent.OnItemHolding(this, Content);
            }

            base.OnHolding(e);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                _parent.CapturePointer(e.Pointer);
                _parent.OnItemHolding(this, Content);
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            //if (_parent.capt)
            _parent.OnItemPointerEntered(this, Content);

            base.OnPointerEntered(e);
        }
    }
}
