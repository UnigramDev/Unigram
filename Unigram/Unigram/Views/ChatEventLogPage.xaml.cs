using System.ComponentModel;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class ChatEventLogPage : HostedPage, INavigablePage, ISearchablePage, IActivablePage
    {
        public DialogEventLogViewModel ViewModel => DataContext as DialogEventLogViewModel;
        public ChatView View => Content as ChatView;

        public ChatEventLogPage()
        {
            InitializeComponent();

            Transitions = ApiInfo.CreateSlideTransition();

            Content = new ChatView(deleg => (DataContext = TLContainer.Current.Resolve<DialogEventLogViewModel, IDialogDelegate>(deleg)) as DialogEventLogViewModel);
            Header = View.Header;
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        public void OnBackRequested(HandledEventArgs args)
        {
            View.OnBackRequested(args);
        }

        public void Search()
        {
            View.Search();
        }

        public void Dispose()
        {
            View.Dispose();
        }

        public void Activate()
        {
            View.Activate();
        }
    }
}
