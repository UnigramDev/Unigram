using System;
using Template10.Common;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class ChatPage : Page, INavigablePage, ISearchablePage, IDisposable
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;
        public ChatView View => Content as ChatView;

        public ChatPage()
        {
            InitializeComponent();

            Content = new ChatView(deleg => (DataContext = TLContainer.Current.Resolve<DialogViewModel, IDialogDelegate>(deleg)) as DialogViewModel);
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        public void OnBackRequested(HandledRoutedEventArgs args)
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
    }
}
