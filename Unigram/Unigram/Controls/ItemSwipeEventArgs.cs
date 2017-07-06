using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Controls
{
    /// <summary>
    /// Provides event data for the <see cref="SwipeListViewItem.ItemSwipe"/> event.
    /// </summary>
    public class ItemSwipeEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a reference to the swiped item.
        /// </summary>
        public object SwipedItem { get; private set; }

        /// <summary>
        /// Gets the direction item is swiped from.
        /// </summary>
        public SwipeListDirection Direction { get; private set; }

        public ItemSwipeEventArgs(object item, SwipeListDirection direction)
        {
            SwipedItem = item;
            Direction = direction;
        }
    }
}
