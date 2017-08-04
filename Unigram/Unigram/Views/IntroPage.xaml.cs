using Unigram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views
{
    public sealed partial class IntroPage : Page
    {
        public IntroViewModel ViewModel => DataContext as IntroViewModel;

        public IntroPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<IntroViewModel>();
        }
    }
}
