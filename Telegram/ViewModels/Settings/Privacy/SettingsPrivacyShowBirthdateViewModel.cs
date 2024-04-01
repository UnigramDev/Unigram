//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Settings.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyShowBirthdateViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyShowBirthdateViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingShowBirthdate())
        {
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (ClientService.TryGetUserFull(ClientService.Options.MyId, out UserFullInfo fullInfo))
            {
                CanSetBirthdate = fullInfo.Birthdate == null;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        private bool _canSetBirthdate;
        public bool CanSetBirthdate
        {
            get => _canSetBirthdate;
            set => Set(ref _canSetBirthdate, value);
        }

        public async void SetBirthdate()
        {
            var popup = new SettingsBirthdatePopup(null);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                CanSetBirthdate = false;

                ClientService.Send(new SetBirthdate(popup.Value));
                ToastPopup.Show(Strings.PrivacyBirthdaySetDone, new LocalFileSource("ms-appx:///Assets/Toasts/Success.tgs"));
            }
        }
    }
}
