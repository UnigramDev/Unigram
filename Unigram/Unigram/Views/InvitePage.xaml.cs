using Unigram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views
{
    public sealed partial class InvitePage : Page
    {
        public InviteViewModel ViewModel => DataContext as InviteViewModel;

        public InvitePage()
        {
            InitializeComponent();
        }
    }
}
