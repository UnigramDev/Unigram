using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public sealed partial class SuggestedActionSetBirthdateCard : HyperlinkButton
    {
        public SuggestedActionSetBirthdateCard()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler HideClick
        {
            add => Hide.Click += value;
            remove => Hide.Click -= value;
        }
    }
}
