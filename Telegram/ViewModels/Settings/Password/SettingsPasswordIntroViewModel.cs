//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Views.Settings.Password;

namespace Telegram.ViewModels.Settings.Password
{
    public class SettingsPasswordIntroViewModel : ViewModelBase
    {
        public SettingsPasswordIntroViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public void Continue()
        {
            NavigationService.Navigate(typeof(SettingsPasswordCreatePage));
        }
    }
}
