using System.Diagnostics;
using Windows.UI.Xaml;

namespace Telegram.Common
{
    public class FrameworkElementState
    {
        private bool _loaded;

        public FrameworkElementState(FrameworkElement element)
        {
            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;
        }

        public FrameworkElementState(FrameworkElement element, RoutedEventHandler loaded, RoutedEventHandler unloaded)
        {
            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;

            Loaded += loaded;
            Unloaded += unloaded;
        }

        ~FrameworkElementState()
        {
            Debug.WriteLine("~LifecycleManager");
        }

        public event RoutedEventHandler Loaded;
        public event RoutedEventHandler Unloaded;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Changed(sender as FrameworkElement, e);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Changed(sender as FrameworkElement, e);
        }

        private void Changed(FrameworkElement sender, RoutedEventArgs e)
        {
            if (sender.Parent != null && !_loaded)
            {
                _loaded = true;
                Loaded?.Invoke(sender, e);
            }
            else if (sender.Parent == null && _loaded)
            {
                _loaded = false;
                Unloaded?.Invoke(sender, e);
            }
        }
    }
}
