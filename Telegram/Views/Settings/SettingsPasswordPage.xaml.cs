//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.ViewModels.Settings;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsPasswordPage : HostedPage
    {
        public SettingsPasswordViewModel ViewModel => DataContext as SettingsPasswordViewModel;

        public SettingsPasswordPage()
        {
            InitializeComponent();
            Title = Strings.TwoStepVerification;
        }
    }
}
