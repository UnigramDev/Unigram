//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public class LogOutViewModel : TLViewModelBase
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

            AskCommand = new RelayCommand(AskExecute);
            LogoutCommand = new RelayCommand(LogoutExecute);
        }

        public bool IsPasscodeEnabled
        {
            get { return _passcodeService.IsEnabled; }
        }

        public RelayCommand AskCommand { get; }
        private async void AskExecute()
        {
            var confirm = await ShowPopupAsync(Strings.Resources.AskAQuestionInfo, Strings.Resources.AskAQuestion, Strings.Resources.AskButton, Strings.Resources.Cancel);
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

        public RelayCommand LogoutCommand { get; }
        private async void LogoutExecute()
        {
            var confirm = await ShowPopupAsync(Strings.Resources.AreYouSureLogout, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            Settings.Clear();
            Settings.PasscodeLock.Clear();

            await _contactsService.RemoveAsync();

            var response = await ClientService.SendAsync(new LogOut());
            if (response is Error error)
            {
                // TODO:
            }
        }
    }
}
