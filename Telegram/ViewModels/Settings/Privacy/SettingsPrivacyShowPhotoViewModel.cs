//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyShowPhotoViewModel : SettingsPrivacyViewModelBase, IDelegable<IUserDelegate>, IHandle
    {
        public IUserDelegate Delegate { get; set; }

        private readonly IProfilePhotoService _profilePhotoService;

        public SettingsPrivacyShowPhotoViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IProfilePhotoService profilePhotoService)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingShowProfilePhoto())
        {
            _profilePhotoService = profilePhotoService;
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (ClientService.TryGetUserFull(ClientService.Options.MyId, out UserFullInfo userFull))
            {
                Delegate?.UpdateUserFullInfo(null, null, userFull, false, false);
            }
            else
            {
                ClientService.Send(new GetUserFullInfo(ClientService.Options.MyId));
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateUserFullInfo>(this, Handle);
        }

        public void Handle(UpdateUserFullInfo update)
        {
            if (update.UserId == ClientService.Options.MyId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(null, null, update.UserFullInfo, false, false));
            }
        }

        public async void SetPhoto()
        {
            await _profilePhotoService.SetPhotoAsync(null, true);
        }

        public async void CreatePhoto()
        {
            await _profilePhotoService.CreatePhotoAsync(NavigationService, null, true);
        }

        public async void RemovePhoto()
        {
            var confirm = await ShowPopupAsync(Strings.RemovePhotoForRestDescription, Strings.RemovePhotoForRestDescription, Strings.Remove, Strings.Cancel, true);
            if (confirm == ContentDialogResult.Primary)
            {
                if (ClientService.TryGetUserFull(ClientService.Options.MyId, out UserFullInfo userFull))
                {
                    if (userFull.PublicPhoto == null)
                    {
                        return;
                    }

                    ClientService.Send(new DeleteProfilePhoto(userFull.PublicPhoto.Id));
                }
            }
        }
    }
}
