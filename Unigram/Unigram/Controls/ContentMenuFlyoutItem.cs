using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class ContentMenuFlyoutItem : MenuFlyoutItem
    {
        public ContentMenuFlyoutItem()
        {
            DefaultStyleKey = typeof(ContentMenuFlyoutItem);
        }

        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register("Content", typeof(object), typeof(ContentMenuFlyoutItem), new PropertyMetadata(null));
    }
}
