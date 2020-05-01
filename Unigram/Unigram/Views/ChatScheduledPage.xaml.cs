using System;
using System.ComponentModel;
using Unigram.Navigation;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class ChatScheduledPage : Page, INavigablePage, ISearchablePage, IDisposable
    {
        public DialogScheduledViewModel ViewModel => DataContext as DialogScheduledViewModel;
        public ChatView View => Content as ChatView;

        public ChatScheduledPage()
        {
            InitializeComponent();

            Content = new ChatView(deleg => (DataContext = TLContainer.Current.Resolve<DialogScheduledViewModel, IDialogDelegate>(deleg)) as DialogScheduledViewModel);
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
    }
}
