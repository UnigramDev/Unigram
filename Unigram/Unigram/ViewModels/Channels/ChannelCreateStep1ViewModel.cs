//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Channels;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.ViewModels.Channels
{
    public class ChannelCreateStep1ViewModel : TLViewModelBase
    {
        public ChannelCreateStep1ViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !string.IsNullOrWhiteSpace(Title));
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                Set(ref _title, value);
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        private string _about;
        public string About
        {
            get => _about;
            set => Set(ref _about, value);
        }

        private BitmapImage _preview;
        public BitmapImage Preview
        {
            get => _preview;
            set => Set(ref _preview, value);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var response = await ClientService.SendAsync(new CreateNewSupergroupChat(_title, false, true, _about ?? string.Empty, null, 0, false));
            if (response is Chat chat)
            {
                // TODO: photo

                NavigationService.Navigate(typeof(ChannelCreateStep2Page), chat.Id);
                NavigationService.GoBackAt(0, false);
            }
        }

        public RelayCommand<StorageFile> EditPhotoCommand { get; }
        private async void EditPhotoExecute(StorageFile file)
        {
        }
    }
}
