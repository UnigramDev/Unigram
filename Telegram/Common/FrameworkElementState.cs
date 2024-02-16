//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Diagnostics;
using Windows.UI.Xaml;

namespace Telegram.Common
{
    public class FrameworkElementState
    {
        public FrameworkElementState(FrameworkElement element)
        {
            element.Loaded += OnChanged;
            element.Unloaded += OnChanged;
        }

        ~FrameworkElementState()
        {
            Debug.WriteLine("~LifecycleManager");
        }

        private bool _loaded;
        private bool _unloaded;

        public bool IsLoaded => _loaded;
        public bool IsUnloaded => _unloaded;

        public event RoutedEventHandler Loaded;
        public event RoutedEventHandler Unloaded;

        private void OnChanged(object sender, RoutedEventArgs e)
        {
            // TODO: unfortunately FrameworkElement.Parent returns null
            // whenever the control is a DataTemplate root or similar,
            // hence we're forced to use VisualTreeHelper here, but I'm quite sure it's slower.
            var element = sender as FrameworkElement;

            var parent = element.Parent ?? Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
            if (parent != null && !_loaded)
            {
                _loaded = true;
                _unloaded = false;
                Loaded?.Invoke(this, e);
            }
            else if (parent == null && _loaded)
            {
                _loaded = false;
                _unloaded = true;
                Unloaded?.Invoke(sender, e);
            }
        }
    }
}
