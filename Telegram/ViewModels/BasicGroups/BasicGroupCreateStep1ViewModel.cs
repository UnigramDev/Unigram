//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.ViewModels.BasicGroups
{
    public class BasicGroupCreateStep1ViewModel : ViewModelBase
    {
        private bool _uploadingPhoto;
        private readonly Action _uploadingCallback;

        public BasicGroupCreateStep1ViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Chat>();

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

        private BitmapImage _preview;
        public BitmapImage Preview
        {
            get => _preview;
            set => Set(ref _preview, value);
        }

        public MvxObservableCollection<Chat> Items { get; private set; }

        public async void AddMembers()
        {
            var chats = await ChooseChatsPopup.PickChatsAsync(Strings.SelectContacts, Items.Select(x => x.Id).ToArray(), ChooseChatsOptions.Users);
            if (chats != null)
            {
                Items.ReplaceWith(chats);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var maxSize = ClientService.Options.BasicGroupSizeMax;

            var peers = Items.Select(x => x.Type).OfType<ChatTypePrivate>().Select(x => x.UserId).ToArray();
            if (peers.Length <= maxSize)
            {
                // Classic chat
                var response = await ClientService.SendAsync(new CreateNewBasicGroupChat(peers, _title, 0));
                if (response is CreatedBasicGroupChat chat)
                {
                    // TODO: photo

                    NavigationService.NavigateToChat(chat.ChatId);
                    NavigationService.GoBackAt(0, false);

                    if (chat.FailedToAddMembers?.FailedToAddMembersValue.Count > 0)
                    {
                        var popup = new ChatInviteFallbackPopup(ClientService, chat.ChatId, chat.FailedToAddMembers.FailedToAddMembersValue);
                        await ShowPopupAsync(popup);
                    }
                }
                else if (response is Error error)
                {
                    AlertsService.ShowAddUserAlert(Dispatcher, error.Message, false);
                }
            }
            else
            {

            }
        }

        public RelayCommand<StorageFile> EditPhotoCommand { get; }
        private async void EditPhotoExecute(StorageFile file)
        {
            _uploadingPhoto = true;
        }

        private void ContinueUploadingPhoto()
        {
            //NavigationService.Navigate(typeof(BasicGroupCreateStep2Page), new ChatCreateStep2Tuple(_title, null));
        }
    }
}
