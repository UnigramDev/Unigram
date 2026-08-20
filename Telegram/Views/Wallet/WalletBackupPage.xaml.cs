//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.ViewModels.Wallet;

namespace Telegram.Views.Wallet
{
    public sealed partial class WalletBackupPage : HostedPage
    {
        public WalletBackupViewModel ViewModel => DataContext as WalletBackupViewModel;

        public WalletBackupPage()
        {
            InitializeComponent();
            Title = "[Keys & Backup]";
        }
    }
}
