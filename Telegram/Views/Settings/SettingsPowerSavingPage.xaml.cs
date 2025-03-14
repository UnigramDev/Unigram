//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.ViewModels.Settings;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsPowerSavingPage : HostedPage
    {
        public SettingsPowerSavingViewModel ViewModel => DataContext as SettingsPowerSavingViewModel;

        public SettingsPowerSavingPage()
        {
            InitializeComponent();
            Title = Strings.PowerUsage;
        }
    }
}
