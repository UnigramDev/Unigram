using Unigram.Controls.Chats;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class LazoListViewItem : ListViewItem
    {
        private readonly ChatListView _parent;

        public LazoListViewItem(ChatListView parent)
        {
            _parent = parent;
        }

        public new void PointerPressed(PointerRoutedEventArgs e)
        {
            if (!CantSelect())
            {
                _parent.OnPointerPressed(this, e);
            }
        }

        public new void PointerEntered(PointerRoutedEventArgs e)
        {
            _parent.OnPointerEntered(this, e);
        }

        public new void PointerMoved(PointerRoutedEventArgs e)
        {
            _parent.OnPointerMoved(this, e);
        }

        public new void PointerReleased(PointerRoutedEventArgs e)
        {
            _parent.OnPointerReleased(this, e);
        }

        public new void PointerCanceled(PointerRoutedEventArgs e)
        {
            _parent.OnPointerCanceled(this, e);
        }

        public virtual bool CantSelect()
        {
            return true;
        }
    }
}
