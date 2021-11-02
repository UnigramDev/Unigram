using Microsoft.UI.Xaml.Controls;
using Unigram.ViewModels;

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
