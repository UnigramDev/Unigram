using System.ComponentModel;
using Unigram.Navigation;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupMembersPage : HostedPage, INavigablePage, ISearchablePage
    {
        public SupergroupMembersPage()
        {
            InitializeComponent();
            DataContext = View.DataContext = TLContainer.Current.Resolve<SupergroupMembersViewModel, ISupergroupDelegate>(View);

            Header = View.Header;
        }

        public void OnBackRequested(HandledEventArgs args)
        {
            View.OnBackRequested(args);
        }

        public void Search()
        {
            View.Search();
        }
    }
}
