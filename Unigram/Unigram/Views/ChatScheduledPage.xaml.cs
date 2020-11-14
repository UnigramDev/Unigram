using System.ComponentModel;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class ChatScheduledPage : HostedPage, INavigablePage, ISearchablePage, IActivablePage
    {
        public DialogScheduledViewModel ViewModel => DataContext as DialogScheduledViewModel;
        public ChatView View => Content as ChatView;

        public ChatScheduledPage()
        {
            InitializeComponent();

            Transitions = ApiInfo.CreateSlideTransition();

            Content = new ChatView(deleg => (DataContext = TLContainer.Current.Resolve<DialogScheduledViewModel, IDialogDelegate>(deleg)) as DialogScheduledViewModel);
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
