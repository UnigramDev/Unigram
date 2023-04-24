using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class SettingsFooter : Control
    {
        public SettingsFooter()
        {
            DefaultStyleKey = typeof(SettingsFooter);
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SettingsFooter), new PropertyMetadata(string.Empty));

        #endregion
    }
}
