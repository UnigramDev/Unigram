using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public class LogOutViewModel : TLViewModelBase
    {
        private readonly INotificationsService _pushService;
        private readonly IContactsService _contactsService;
        private readonly IPasscodeService _passcodeService;

        public LogOutViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, INotificationsService notificationsService, IContactsService contactsService, IPasscodeService passcodeService)
            : base(protoService, cacheService, settingsService, aggregator)
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
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.AskAQuestionInfo, Strings.Resources.AskAQuestion, Strings.Resources.AskButton, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ProtoService.SendAsync(new GetSupportUser());
                if (response is User user)
                {
                    response = await ProtoService.SendAsync(new CreatePrivateChat(user.Id, false));
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
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureLogout, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            Settings.Clear();
            Settings.PasscodeLock.Clear();

            await _pushService.UnregisterAsync();
            await _contactsService.RemoveAsync();

            var response = await ProtoService.SendAsync(new LogOut());
            if (response is Error error)
            {
                // TODO:
            }
        }
    }
}
