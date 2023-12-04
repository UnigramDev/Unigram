using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    // Name of the file is FrameworkElementEx.cs because supposedly this code should be
    // added to all classes inheriting FrameworkElement, but this isn't really possible in C#.

    public class ControlEx : Control
    {
        private bool _loaded;
        public bool IsConnected => _loaded;

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
            if (Parent != null && !_loaded)
            {
                _loaded = true;
                _connected?.Invoke(this, e);
            }
            else if (Parent == null && _loaded)
            {
                _loaded = false;
                _disconnected?.Invoke(sender, e);
            }
        }
    }
}
