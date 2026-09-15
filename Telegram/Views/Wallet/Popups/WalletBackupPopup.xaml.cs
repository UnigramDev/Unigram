//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.ViewModels.Wallet;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// Keys and backup, over the wallet window.
    /// </summary>
    /// <remarks>
    /// A popup rather than a page because the wallet window has no frame to push a page into: its
    /// navigation service forwards to the window it was opened from, so navigating here would take
    /// the user out of the wallet and into the main window.
    ///
    /// The view model is resolved here for the same reason the window resolves its own - the
    /// navigation machinery only runs for pages - and it is the page's, unchanged, so the content
    /// can go back to being a page if the window ever grows a frame of its own.
    /// </remarks>
    public sealed partial class WalletBackupPopup : ModalPopup
    {
        public WalletBackupViewModel ViewModel => DataContext as WalletBackupViewModel;

        public WalletBackupPopup(INavigationService navigationService)
        {
            var viewModel = navigationService.Session.Resolve<WalletBackupViewModel>();
            viewModel.NavigationService = navigationService;
            viewModel.Dispatcher = navigationService.Dispatcher;

            DataContext = viewModel;

            InitializeComponent();

            Title = "[Keys & Backup]";
        }
    }
}
