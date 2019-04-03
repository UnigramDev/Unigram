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
            base.OnHolding(e);

            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                VisualStateManager.GoToState(this, "Normal", false);

                if (_lastPointer != null && _lastPointer.IsInContact)
                {
                    _parent.CapturePointer(_lastPointer);
                    _lastPointer = null;
                }

                _parent.OnItemHolding(this, Tag);
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);

            _lastPointer = e.Pointer;

            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                VisualStateManager.GoToState(this, "Normal", false);

                _parent.CapturePointer(e.Pointer);
                _parent.OnItemHolding(this, Tag);
            }
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            //if (_parent.capt)
            _parent.OnItemPointerEntered(this, Tag);

            base.OnPointerEntered(e);
        }
    }
}
