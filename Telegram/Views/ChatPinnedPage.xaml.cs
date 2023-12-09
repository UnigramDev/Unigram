//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Navigation;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public sealed partial class ChatPinnedPage : HostedPage, INavigablePage, ISearchablePage, IActivablePage
    {
        public DialogPinnedViewModel ViewModel => DataContext as DialogPinnedViewModel;

        public ChatPinnedPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        public override string GetTitle()
        {
            return View.ChatTitle;
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            View.OnBackRequested(args);
        }

        public void Search()
        {
            View.Search();
        }

        public void Deactivate(bool navigation)
        {
            View.Deactivate(navigation);

            if (navigation)
            {
                return;
            }

            DataContext = new object();
        }

        public void Activate(int sessionId)
        {
            var viewModel = TypeResolver.Current.Resolve<DialogPinnedViewModel, IDialogDelegate>(View, sessionId);
            DataContext = viewModel;
            View.Activate(viewModel);
        }

        public void PopupOpened()
        {
            View.PopupOpened();
        }

        public void PopupClosed()
        {
            View.PopupClosed();
        }
    }
}
