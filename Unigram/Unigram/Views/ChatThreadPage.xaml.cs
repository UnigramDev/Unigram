using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;
using Unigram.Navigation;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;

namespace Unigram.Views
{
    public sealed partial class ChatThreadPage : HostedPage, INavigablePage, ISearchablePage, IActivablePage
    {
        public DialogThreadViewModel ViewModel => DataContext as DialogThreadViewModel;
        public ChatView View => Content as ChatView;

        public ChatThreadPage()
        {
            InitializeComponent();

            Content = new ChatView(deleg => (DataContext = TLContainer.Current.Resolve<DialogThreadViewModel, IDialogDelegate>(deleg)) as DialogThreadViewModel);
            Header = View.Header;
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            View.OnNavigatingFrom(e.SourcePageType);
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
