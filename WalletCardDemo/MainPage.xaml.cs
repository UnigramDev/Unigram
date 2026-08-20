using Windows.UI.Xaml.Controls;

namespace WalletCardDemo
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();

            // Created in code so the demo has exactly one place that knows how
            // the card is put together.
            LayoutRoot.Children.Add(new WalletCardView(WalletCardModel.Demo));
        }
    }
}
