//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;

namespace Telegram.Views
{
    public sealed partial class ChatPinnedPage : HostedPage, INavigablePage, ISearchablePage, IActivablePage
    {
        public DialogPinnedViewModel ViewModel => DataContext as DialogPinnedViewModel;

        public ChatPinnedPage()
        {
            InitializeComponent();
            NavigationCacheMode = ApiInfo.NavigationCacheMode;
        }

        public override string GetTitle()
        {
            return View.ChatTitle;
        }

        public override HostedPagePositionBase GetPosition()
        {
            return null;
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

        public void Activate(INavigationService navigationService)
        {
            var viewModel = TypeResolver.Current.Resolve<DialogPinnedViewModel, IDialogDelegate>(View, navigationService.SessionId);
            viewModel.NavigationService = navigationService;
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
