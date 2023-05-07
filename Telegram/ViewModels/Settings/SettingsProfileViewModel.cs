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
using Telegram.Views.Settings;
using Telegram.Views.Settings.Popups;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsProfileViewModel : TLViewModelBase
        , IDelegable<IUserDelegate>
        , IHandle
    //, IHandle<UpdateUser>
    //, IHandle<UpdateUserFullInfo>
    {
        public IUserDelegate Delegate { get; set; }

        private readonly IProfilePhotoService _profilePhotoService;

        public SettingsProfileViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IProfilePhotoService profilePhotoService)
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

        private string _bio;
        public string Bio
        {
            get => _bio;
            set
            {
                if (Set(ref _bio, value))
                {
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public int BioLengthMax => (int)ClientService.Options.BioLengthMax;

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (ClientService.TryGetUser(ClientService.Options.MyId, out User user))
            {
                FirstName = user.FirstName;
                LastName = user.LastName;

                Delegate?.UpdateUser(null, user, false);

                if (ClientService.TryGetUserFull(user.Id, out UserFullInfo userFull))
                {
                    Bio = userFull.Bio.Text;

                    Delegate?.UpdateUserFullInfo(null, user, userFull, false, false);
                }
                else
                {
                    ClientService.Send(new GetUserFullInfo(user.Id));
                }
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateUser>(this, Handle)
                .Subscribe<UpdateUserFullInfo>(Handle);
        }

        public void Handle(UpdateUser update)
        {
            if (update.User.Id == ClientService.Options.MyId)
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
            if (update.UserId == ClientService.Options.MyId && ClientService.TryGetUser(ClientService.Options.MyId, out User user))
            {
                BeginOnUIThread(() =>
                {
                    Bio = update.UserFullInfo.Bio.Text;

                    Delegate?.UpdateUserFullInfo(null, user, update.UserFullInfo, false, false);
                });
            }
        }

        public RelayCommand SendCommand { get; }
        private async void Send()
        {
            if (ClientService.TryGetUser(ClientService.Options.MyId, out User user) && ClientService.TryGetUserFull(user.Id, out UserFullInfo userFull))
            {
                if (string.IsNullOrEmpty(_firstName))
                {
                    _firstName = _lastName;
                }

                if (string.IsNullOrEmpty(_firstName))
                {
                    return;
                }

                if (!string.Equals(_firstName, user.FirstName) || !string.Equals(_lastName, user.LastName))
                {
                    var response = await ClientService.SendAsync(new SetName(_firstName, _lastName));
                    if (response is Error error)
                    {
                        // TODO:
                        return;
                    }
                }

                if (!string.Equals(_bio, userFull.Bio.Text))
                {
                    var response = await ClientService.SendAsync(new SetBio(_bio));
                    if (response is Error error)
                    {
                        // TODO:
                        return;
                    }
                }

                NavigationService.GoBack();
            }
        }

        private bool CanSend()
        {
            return _firstName.Length > 0
                && _firstName.Length <= 64
                && _lastName.Length <= 64
                && _bio.Length <= ClientService.Options.BioLengthMax;
        }

        public async void SetPhoto()
        {
            await _profilePhotoService.SetPhotoAsync(null);
        }

        public async void CreatePhoto()
        {
            await _profilePhotoService.CreatePhotoAsync(NavigationService, null);
        }

        public async void ChangePhoneNumber()
        {
            await ShowPopupAsync(new ChangePhoneNumberPopup());
        }

        public async void ChangeUsername()
        {
            await ShowPopupAsync(typeof(SettingsUsernamePopup));
        }
    }
}
