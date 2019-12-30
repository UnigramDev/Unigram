using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Template10.Common;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
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
