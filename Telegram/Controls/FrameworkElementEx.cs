//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    // Name of the file is FrameworkElementEx.cs because supposedly this code should be
    // added to all classes inheriting FrameworkElement, but this isn't really possible in C#.

    public static class FrameworkElementEx
    {
        public static bool IsConnected(this FrameworkElement element)
        {
            return Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(element) != null;
        }

        public static DependencyObject GetParent(this FrameworkElement element)
        {
            try
            {
                // element.Parent seems to throw E_FAIL at times
                return element.Parent ?? Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
            }
            catch
            {
                return null;
            }
        }
    }

    // TODO: move all to C++ if it makes sense
    // It's reasonable to have a custom class for ChatListListViewItem with holding embedded
    // GridEx is quite an hot path too
    // ToggleButtonEx is inherited by ReactionButton

    public partial class UserControlEx : UserControl
    {
        private bool _loaded;
        private bool _unloaded;

        public bool IsConnected => _loaded;
        public bool IsDisconnected => _unloaded;

        public event RoutedEventHandler Connected;
        public event RoutedEventHandler Disconnected;

        public UserControlEx()
        {
            Loaded += OnChanged;
            Unloaded += OnChanged;
        }

        protected virtual void OnLoaded()
        {

        }

        protected virtual void OnUnloaded()
        {

        }

        private void OnChanged(object sender, RoutedEventArgs e)
        {
            // TODO: unfortunately FrameworkElement.Parent returns null
            // whenever the control is a DataTemplate root or similar,
            // hence we're forced to use VisualTreeHelper here, but I'm quite sure it's slower.

            var parent = this.GetParent();
            if (parent != null && !_loaded)
            {
                _loaded = true;
                _unloaded = false;
                OnLoaded();
                Connected?.Invoke(this, e);
            }
            else if (parent == null && _loaded)
            {
                _loaded = false;
                _unloaded = true;
                OnUnloaded();
                Disconnected?.Invoke(sender, e);
            }
        }
    }

    public partial class PageEx : Page
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

            var parent = this.GetParent();
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

    public partial class ContentDialogEx : ContentDialog
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

            var parent = this.GetParent();
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

    public partial class ListViewEx : ListView
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

            var parent = this.GetParent();
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

    public partial class ListViewItemEx : ListViewItem
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

            var parent = this.GetParent();
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
