//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Supergroups;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.ViewModels.Create
{
    public partial class NewChannelViewModel : ViewModelBase
    {
        private readonly IProfilePhotoService _profilePhotoService;

        public NewChannelViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IProfilePhotoService profilePhotoService)
            : base(clientService, settingsService, aggregator)
        {
            _profilePhotoService = profilePhotoService;
        }

        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                if (Set(ref _title, value))
                {
                    RaisePropertyChanged(nameof(CanCreate));
                }
            }
        }

        private string _about;
        public string About
        {
            get => _about;
            set => Set(ref _about, value);
        }

        private InputChatPhoto _inputPhoto;

        private BitmapImage _preview;
        public BitmapImage Preview
        {
            get => _preview;
            set => Set(ref _preview, value);
        }

        public bool CanCreate => !string.IsNullOrWhiteSpace(Title);

        public async void Create()
        {
            var response = await ClientService.SendAsync(new CreateNewSupergroupChat(_title, false, true, _about ?? string.Empty, null, 0, false));
            if (response is Chat chat)
            {
                if (_inputPhoto != null)
                {
                    ClientService.Send(new SetChatPhoto(chat.Id, _inputPhoto));
                }

                NavigationService.Navigate(typeof(SupergroupEditTypePage), new SupergroupEditTypeArgs(chat.Id, true));
                NavigationService.GoBackAt(0, false);
            }
            else if (response is Error error)
            {
                if (error.MessageEquals(ErrorType.CHANNELS_TOO_MUCH))
                {
                    NavigationService.ShowLimitReached(new PremiumLimitTypeSupergroupCount());
                }
                else
                {
                    ShowToast(error);
                }
            }
        }

        public async void ChoosePhoto()
        {
            _inputPhoto = await _profilePhotoService.PreviewSetPhotoAsync(NavigationService);
        }
    }
}
