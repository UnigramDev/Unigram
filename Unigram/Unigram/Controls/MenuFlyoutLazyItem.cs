using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class MenuFlyoutLazyItem : MenuFlyoutItem
    {
        private RoutedEventHandler _updated;

        public event RoutedEventHandler Updated
        {
            add
            {
                _updated = value;
            }
            remove
            {
                _updated = null;
            }
        }

        public void OnUpdated()
        {
            _updated?.Invoke(this, null);
        }
    }
}
