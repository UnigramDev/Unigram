using System.ComponentModel;
using Unigram.Navigation;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class ChatPinnedPage : HostedPage, INavigablePage, ISearchablePage, IActivablePage
    {
        public DialogPinnedViewModel ViewModel => DataContext as DialogPinnedViewModel;
        public ChatView View => Content as ChatView;

        public ChatPinnedPage()
        {
            InitializeComponent();

            Content = new ChatView(deleg => (DataContext = TLContainer.Current.Resolve<DialogPinnedViewModel, IDialogDelegate>(deleg)) as DialogPinnedViewModel);
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
