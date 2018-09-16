using Unigram.ViewModels.Channels;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Channels
{
    public sealed partial class ChannelCreateStep3Page : Page
    {
        public ChannelCreateStep3Page()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ChannelCreateStep3ViewModel>();
            View.Attach();
        }
    }
}
