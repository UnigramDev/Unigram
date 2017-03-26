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

        private Pointer _lastPointer;

        public ZoomableGridViewItem(ZoomableGridView parent)
        {
            _parent = parent;
            IsHoldingEnabled = true;
        }

        protected override void OnHolding(HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if (_lastPointer != null && _lastPointer.IsInContact)
                {
                    _parent.CapturePointer(_lastPointer);
                    _lastPointer = null;
                }

                _parent.OnItemHolding(this, Content);
            }

            base.OnHolding(e);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            _lastPointer = e.Pointer;

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
