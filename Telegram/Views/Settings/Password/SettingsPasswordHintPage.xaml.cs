//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.ViewModels.Settings.Password;

namespace Telegram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordHintPage : HostedPage
    {
        public SettingsPasswordHintViewModel ViewModel => DataContext as SettingsPasswordHintViewModel;

        public SettingsPasswordHintPage()
        {
            InitializeComponent();
        }
    }
}
