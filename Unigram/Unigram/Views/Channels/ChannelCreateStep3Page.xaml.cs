using Unigram.ViewModels.Channels;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Channels
{
    public sealed partial class ChannelCreateStep3Page : Page
    {
        public ChannelCreateStep3Page()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ChannelCreateStep3ViewModel>();
            View.Attach();
        }
    }
}
