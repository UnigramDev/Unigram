using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class LazoListViewItem : ListViewItem
    {
        private LazoListView _parent;

        public LazoListViewItem(LazoListView parent)
        {
            _parent = parent;
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is ListViewItemPresenter)
            {
                _parent.OnPointerPressed(this, e);
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            //if (e.OriginalSource is ListViewItemPresenter)
            {
                _parent.OnPointerEntered(this, e);
            }

            base.OnPointerEntered(e);
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            //if (e.OriginalSource is ListViewItemPresenter)
            {
                _parent.OnPointerMoved(this, e);
            }

            base.OnPointerMoved(e);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            //if (e.OriginalSource is ListViewItemPresenter)
            {
                _parent.OnPointerReleased(this, e);
            }

            base.OnPointerReleased(e);
        }
    }
}
