//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyShowStatusViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyShowStatusViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingShowStatus())
        {
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            ClientService.Send(new GetReadDatePrivacySettings(), result =>
            {
                if (result is ReadDatePrivacySettings settings)
                {
                    _previousHideReadDate = !settings.ShowReadDate;
                    BeginOnUIThread(() => HideReadDate = !settings.ShowReadDate);
                }
            });

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        private bool? _previousHideReadDate;

        private bool _hideReadDate;
        public bool HideReadDate
        {
            get => _hideReadDate;
            set => Set(ref _hideReadDate, value);
        }

        public void SubscribeToPremium()
        {
            NavigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureAdvancedChatManagement()));
        }

        public override void Save()
        {
            if (_previousHideReadDate.HasValue && _previousHideReadDate != HideReadDate)
            {
                ClientService.Send(new SetReadDatePrivacySettings(new ReadDatePrivacySettings(!HideReadDate)));
            }

            base.Save();
        }
    }
}
