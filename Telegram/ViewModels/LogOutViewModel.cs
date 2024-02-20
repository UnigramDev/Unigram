//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public class LogOutViewModel : ViewModelBase
    {
        private readonly INotificationsService _pushService;
        private readonly IContactsService _contactsService;
        private readonly IPasscodeService _passcodeService;

        public LogOutViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService notificationsService, IContactsService contactsService, IPasscodeService passcodeService)
            : base(clientService, settingsService, aggregator)
        {
            _pushService = notificationsService;
            _contactsService = contactsService;
            _passcodeService = passcodeService;
        }

        public bool IsPasscodeEnabled => _passcodeService.IsEnabled;

        public async void ChangePhoneNumber()
        {
            await ShowPopupAsync(new ChangePhoneNumberPopup());
        }

        public async void Ask()
        {
            var confirm = await ShowPopupAsync(Strings.AskAQuestionInfo, Strings.AskAQuestion, Strings.AskButton, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new GetSupportUser());
                if (response is User user)
                {
                    response = await ClientService.SendAsync(new CreatePrivateChat(user.Id, false));
                    if (response is Chat chat)
                    {
                        NavigationService.NavigateToChat(chat);
                    }
                }
            }
        }

        public async void Logout()
        {
            var confirm = await ShowPopupAsync(Strings.AreYouSureLogout, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ContentPopup.Block();

            Settings.Clear();
            Settings.PasscodeLock.Clear();

            await _contactsService.RemoveAsync();

            var response = await ClientService.SendAsync(new LogOut());
            if (response is Error error)
            {
                // TODO:
            }
        }
        public void OpenPasscode()
        {
            NavigationService.Navigate(typeof(SettingsPasscodePage));
        }

        public void OpenStorage()
        {
            NavigationService.Navigate(typeof(SettingsStoragePage));
        }
    }
}
