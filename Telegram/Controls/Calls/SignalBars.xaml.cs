using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Calls
{
    public sealed partial class SignalBars : UserControl
    {
        public SignalBars()
        {
            InitializeComponent();
        }

        public int Count
        {
            set => SetCount(value);
        }

        private void SetCount(int value)
        {
            for (int i = 1; i < 5; i++)
            {
                var rectangle = FindName($"Signal{i}") as Rectangle;
                var brush = rectangle.Fill as SolidColorBrush;
                brush.Opacity = i <= value ? 1 : 0.4;
            }
        }
    }
}
