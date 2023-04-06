//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Users
{
    public class UserEditViewModel : TLViewModelBase
        , IDelegable<IUserDelegate>
        , IHandle
    //, IHandle<UpdateUser>
    //, IHandle<UpdateUserFullInfo>
    {
        public IUserDelegate Delegate { get; set; }

        private readonly IProfilePhotoService _profilePhotoService;

        private bool _discardChanges;

        public UserEditViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IProfilePhotoService profilePhotoService)
            : base(clientService, settingsService, aggregator)
        {
            _profilePhotoService = profilePhotoService;

            SendCommand = new RelayCommand(Send, CanSend);
        }

        private string _firstName;
        public string FirstName
        {
            get => _firstName;
            set
            {
                if (Set(ref _firstName, value))
                {
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _lastName;
        public string LastName
        {
            get => _lastName;
            set
            {
                if (Set(ref _lastName, value))
                {
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set => Set(ref _description, value);
        }

        private bool _sharePhoneNumber;
        public bool SharePhoneNumber
        {
            get => _sharePhoneNumber;
            set
            {
                if (Set(ref _sharePhoneNumber, value))
                {
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private long _userId;

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long userId && ClientService.TryGetUser(userId, out User user))
            {
                _userId = userId;

                FirstName = user.FirstName;
                LastName = user.LastName;

                Delegate?.UpdateUser(null, user, false);

                if (ClientService.TryGetUserFull(user.Id, out UserFullInfo userFull))
                {
                    Description = userFull.BotInfo?.ShortDescription ?? string.Empty;

                    Delegate?.UpdateUserFullInfo(null, user, userFull, false, false);
                }
                else
                {
                    ClientService.Send(new GetUserFullInfo(user.Id));
                }
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override async void NavigatingFrom(NavigatingEventArgs args)
        {
            if (_discardChanges || args.NavigationMode != NavigationMode.Back)
            {
                return;
            }

            if (ClientService.TryGetUser(_userId, out User user) && ClientService.TryGetUserFull(user.Id, out UserFullInfo userFull))
            {
                if (user.Type is UserTypeBot userTypeBot && userTypeBot.CanBeEdited)
                {
                    if (user.FirstName != _firstName || userFull.BotInfo?.ShortDescription != _description)
                    {
                        args.Cancel = true;

                        var confirm = await ShowPopupAsync(Strings.BotSettingsChangedAlert, Strings.UnsavedChanges, Strings.ApplyTheme, Strings.Discard);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            Send();
                        }
                        else
                        {
                            _discardChanges = true;
                            NavigationService.GoBack();
                        }
                    }
                }
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateUser>(this, Handle)
                .Subscribe<UpdateUserFullInfo>(Handle);
        }

        public void Handle(UpdateUser update)
        {
            if (update.User.Id == _userId)
            {
                BeginOnUIThread(() =>
                {
                    FirstName = update.User.FirstName;
                    LastName = update.User.LastName;

                    Delegate?.UpdateUser(null, update.User, false);
                });
            }
        }

        public void Handle(UpdateUserFullInfo update)
        {
            if (update.UserId == _userId && ClientService.TryGetUser(_userId, out User user))
            {
                BeginOnUIThread(() =>
                {
                    Description = update.UserFullInfo.BotInfo?.ShortDescription ?? string.Empty;

                    Delegate?.UpdateUserFullInfo(null, user, update.UserFullInfo, false, false);
                });
            }
        }

        public RelayCommand SendCommand { get; }
        private void Send()
        {
            _discardChanges = true;

            if (ClientService.TryGetUser(_userId, out User user) && ClientService.TryGetUserFull(user.Id, out UserFullInfo userFull))
            {
                if (user.Type is UserTypeBot userTypeBot && userTypeBot.CanBeEdited)
                {
                    if (user.FirstName != _firstName)
                    {
                        ClientService.Send(new SetBotName(user.Id, string.Empty, _firstName));
                    }

                    if (userFull.BotInfo?.ShortDescription != _description)
                    {
                        ClientService.Send(new SetBotInfoShortDescription(user.Id, string.Empty, _description));
                    }
                }
                else
                {
                    ClientService.Send(new AddContact(new Contact(user.PhoneNumber, _firstName, _lastName, string.Empty, user.Id),
                        !userFull.NeedPhoneNumberPrivacyException || SharePhoneNumber));
                }

                NavigationService.GoBack();
            }
        }

        private bool CanSend()
        {
            return _firstName.Length > 0
                && _firstName.Length <= 64
                && _lastName.Length <= 64;
        }

        public async void SetPhoto()
        {
            var success = await _profilePhotoService.SetPhotoAsync(_userId, isPersonal: false);
            if (success)
            {
                NavigationService.NavigateToChat(_userId);
            }
        }

        public async void CreatePhoto()
        {
            var success = await _profilePhotoService.CreatePhotoAsync(NavigationService, _userId, isPersonal: false);
            if (success)
            {
                NavigationService.NavigateToChat(_userId);
            }
        }

        public async void SetPersonalPhoto()
        {
            await _profilePhotoService.SetPhotoAsync(_userId, isPersonal: true);
        }

        public async void CreatePersonalPhoto()
        {
            await _profilePhotoService.CreatePhotoAsync(NavigationService, _userId, isPersonal: true);
        }

        public async void ResetPhoto()
        {
            if (ClientService.TryGetUser(_userId, out User user))
            {
                var confirm = await ShowPopupAsync(string.Format(Strings.ResetToOriginalPhotoMessage, user.FirstName), Strings.ResetToOriginalPhotoTitle, Strings.Reset, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    ClientService.Send(new SetUserPersonalProfilePhoto(user.Id, null));
                }
            }
        }

        public void ChangeUsername()
        {
            // TODO: edit bot usernames
        }

        public void EditCommands()
        {
            if (ClientService.TryGetUserFull(_userId, out UserFullInfo fullInfo) && fullInfo.BotInfo != null)
            {
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, fullInfo.BotInfo.EditCommandsLink);
            }
        }

        public void EditDescription()
        {
            if (ClientService.TryGetUserFull(_userId, out UserFullInfo fullInfo) && fullInfo.BotInfo != null)
            {
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, fullInfo.BotInfo.EditDescriptionLink);
            }
        }

        public void EditSettings()
        {
            if (ClientService.TryGetUserFull(_userId, out UserFullInfo fullInfo) && fullInfo.BotInfo != null)
            {
                MessageHelper.OpenTelegramUrl(ClientService, NavigationService, fullInfo.BotInfo.EditSettingsLink);
            }
        }
    }
}
