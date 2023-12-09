using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    // Name of the file is FrameworkElementEx.cs because supposedly this code should be
    // added to all classes inheriting FrameworkElement, but this isn't really possible in C#.

    // TODO: would be great to find a way to have this as a template.

    public class ControlEx : Control
    {
        private bool _loaded;
        private bool _unloaded;

        public bool IsConnected => _loaded;
        public bool IsDisconnected => _unloaded;

        private event RoutedEventHandler _connected;
        public event RoutedEventHandler Connected
        {
            add
            {
                if (_connected == null && _disconnected == null)
                {
                    Loaded += OnChanged;
                    Unloaded += OnChanged;
                }

                _connected += value;
            }
            remove
            {
                _connected -= value;

                if (_connected == null && _disconnected == null)
                {
                    Loaded -= OnChanged;
                    Unloaded -= OnChanged;
                }
            }
        }

        private event RoutedEventHandler _disconnected;
        public event RoutedEventHandler Disconnected
        {
            add
            {
                if (_connected == null && _disconnected == null)
                {
                    Loaded += OnChanged;
                    Unloaded += OnChanged;
                }

                _disconnected += value;
            }
            remove
            {
                _disconnected -= value;

                if (_connected == null && _disconnected == null)
                {
                    Loaded -= OnChanged;
                    Unloaded -= OnChanged;
                }
            }
        }

        private void OnChanged(object sender, RoutedEventArgs e)
        {
            // TODO: unfortunately FrameworkElement.Parent returns null
            // whenever the control is a DataTemplate root or similar,
            // hence we're forced to use VisualTreeHelper here, but I'm quite sure it's slower.

            var parent = Parent ?? Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(this);
            if (parent != null && !_loaded)
            {
                _loaded = true;
                _unloaded = false;
                _connected?.Invoke(this, e);
            }
            else if (parent == null && _loaded)
            {
                _loaded = false;
                _unloaded = true;
                _disconnected?.Invoke(sender, e);
            }
        }
    }

    public class GridEx : Grid
    {
        private bool _loaded;
        private bool _unloaded;

        public bool IsConnected => _loaded;
        public bool IsDisconnected => _unloaded;

        private event RoutedEventHandler _connected;
        public event RoutedEventHandler Connected
        {
            add
            {
                if (_connected == null && _disconnected == null)
                {
                    Loaded += OnChanged;
                    Unloaded += OnChanged;
                }

                _connected += value;
            }
            remove
            {
                _connected -= value;

                if (_connected == null && _disconnected == null)
                {
                    Loaded -= OnChanged;
                    Unloaded -= OnChanged;
                }
            }
        }

        private event RoutedEventHandler _disconnected;
        public event RoutedEventHandler Disconnected
        {
            add
            {
                if (_connected == null && _disconnected == null)
                {
                    Loaded += OnChanged;
                    Unloaded += OnChanged;
                }

                _disconnected += value;
            }
            remove
            {
                _disconnected -= value;

                if (_connected == null && _disconnected == null)
                {
                    Loaded -= OnChanged;
                    Unloaded -= OnChanged;
                }
            }
        }

        private void OnChanged(object sender, RoutedEventArgs e)
        {
            // TODO: unfortunately FrameworkElement.Parent returns null
            // whenever the control is a DataTemplate root or similar,
            // hence we're forced to use VisualTreeHelper here, but I'm quite sure it's slower.

            var parent = Parent ?? Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(this);
            if (parent != null && !_loaded)
            {
                _loaded = true;
                _unloaded = false;
                _connected?.Invoke(this, e);
            }
            else if (parent == null && _loaded)
            {
                _loaded = false;
                _unloaded = true;
                _disconnected?.Invoke(sender, e);
            }
        }
    }

    public class ListViewEx : ListView
    {
        private bool _loaded;
        private bool _unloaded;

        public bool IsConnected => _loaded;
        public bool IsDisconnected => _unloaded;

        private event RoutedEventHandler _connected;
        public event RoutedEventHandler Connected
        {
            add
            {
                if (_connected == null && _disconnected == null)
                {
                    Loaded += OnChanged;
                    Unloaded += OnChanged;
                }

                _connected += value;
            }
            remove
            {
                _connected -= value;

                if (_connected == null && _disconnected == null)
                {
                    Loaded -= OnChanged;
                    Unloaded -= OnChanged;
                }
            }
        }

        private event RoutedEventHandler _disconnected;
        public event RoutedEventHandler Disconnected
        {
            add
            {
                if (_connected == null && _disconnected == null)
                {
                    Loaded += OnChanged;
                    Unloaded += OnChanged;
                }

                _disconnected += value;
            }
            remove
            {
                _disconnected -= value;

                if (_connected == null && _disconnected == null)
                {
                    Loaded -= OnChanged;
                    Unloaded -= OnChanged;
                }
            }
        }

        private void OnChanged(object sender, RoutedEventArgs e)
        {
            // TODO: unfortunately FrameworkElement.Parent returns null
            // whenever the control is a DataTemplate root or similar,
            // hence we're forced to use VisualTreeHelper here, but I'm quite sure it's slower.

            var parent = Parent ?? Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(this);
            if (parent != null && !_loaded)
            {
                _loaded = true;
                _unloaded = false;
                _connected?.Invoke(this, e);
            }
            else if (parent == null && _loaded)
            {
                _loaded = false;
                _unloaded = true;
                _disconnected?.Invoke(sender, e);
            }
        }
    }

    public class ListViewItemEx : ListViewItem
    {
        private bool _loaded;
        private bool _unloaded;

        public bool IsConnected => _loaded;
        public bool IsDisconnected => _unloaded;

        private event RoutedEventHandler _connected;
        public event RoutedEventHandler Connected
        {
            add
            {
                if (_connected == null && _disconnected == null)
                {
                    Loaded += OnChanged;
                    Unloaded += OnChanged;
                }

                _connected += value;
            }
            remove
            {
                _connected -= value;

                if (_connected == null && _disconnected == null)
                {
                    Loaded -= OnChanged;
                    Unloaded -= OnChanged;
                }
            }
        }

        private event RoutedEventHandler _disconnected;
        public event RoutedEventHandler Disconnected
        {
            add
            {
                if (_connected == null && _disconnected == null)
                {
                    Loaded += OnChanged;
                    Unloaded += OnChanged;
                }

                _disconnected += value;
            }
            remove
            {
                _disconnected -= value;

                if (_connected == null && _disconnected == null)
                {
                    Loaded -= OnChanged;
                    Unloaded -= OnChanged;
                }
            }
        }

        private void OnChanged(object sender, RoutedEventArgs e)
        {
            // TODO: unfortunately FrameworkElement.Parent returns null
            // whenever the control is a DataTemplate root or similar,
            // hence we're forced to use VisualTreeHelper here, but I'm quite sure it's slower.

            var parent = Parent ?? Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(this);
            if (parent != null && !_loaded)
            {
                _loaded = true;
                _unloaded = false;
                _connected?.Invoke(this, e);
            }
            else if (parent == null && _loaded)
            {
                _loaded = false;
                _unloaded = true;
                _disconnected?.Invoke(sender, e);
            }
        }
    }
}
