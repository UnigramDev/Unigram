//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings.Privacy;

namespace Telegram.ViewModels.Settings.Privacy
{
    public partial class SettingsPrivacyAllowCallsViewModel : SettingsPrivacyViewModelBase
    {
        private readonly SettingsPrivacyAllowP2PCallsViewModel _allowP2PCallsRules;

        public SettingsPrivacyAllowCallsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, SettingsPrivacyAllowP2PCallsViewModel p2pCall)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingAllowCalls())
        {
            _allowP2PCallsRules = p2pCall;

            Children.Add(_allowP2PCallsRules);
        }

        public SettingsPrivacyAllowP2PCallsViewModel AllowP2PCallsRules => _allowP2PCallsRules;

        public void OpenP2PCall()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyAllowP2PCallsPage));
        }
    }
}
