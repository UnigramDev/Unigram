//
// Copyright (c) Fela Ameghino 2015-2026
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
    public partial class SettingsPrivacyShowStatusViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyShowStatusViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingShowStatus())
        {
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetReadDatePrivacySettings());
            if (response is ReadDatePrivacySettings settings)
            {
                _previousHideReadDate = !settings.ShowReadDate;
                HideReadDate = !settings.ShowReadDate;
                RaisePropertyChanged(nameof(HasChanged));
            }

            await base.OnNavigatedToAsync(parameter, mode, state);
        }

        private bool? _previousHideReadDate;

        private bool _hideReadDate;
        public bool HideReadDate
        {
            get => _hideReadDate;
            set => Invalidate(ref _hideReadDate, value);
        }

        public void SubscribeToPremium()
        {
            NavigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureAdvancedChatManagement()));
        }

        public override bool HasChanged => _previousHideReadDate != null && (base.HasChanged || _previousHideReadDate != HideReadDate);

        protected override void ContinueImpl(NavigatingEventArgs args)
        {
            if (_previousHideReadDate.HasValue && _previousHideReadDate != HideReadDate)
            {
                ClientService.Send(new SetReadDatePrivacySettings(new ReadDatePrivacySettings(!HideReadDate)));
            }

            base.ContinueImpl(args);
        }
    }
}
