using System;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.ViewModels.BasicGroups
{
    public class BasicGroupCreateStep1ViewModel : TLViewModelBase
    {
        private bool _uploadingPhoto;
        private Action _uploadingCallback;

        public BasicGroupCreateStep1ViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Chat>();

            AddCommand = new RelayCommand(AddExecute);
            SendCommand = new RelayCommand(SendExecute, () => !string.IsNullOrWhiteSpace(Title) && Items.Count > 0);
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
        }

        private string _title;
        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                Set(ref _title, value);
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        private BitmapImage _preview;
        public BitmapImage Preview
        {
            get
            {
                return _preview;
            }
            set
            {
                Set(ref _preview, value);
            }
        }

        public MvxObservableCollection<Chat> Items { get; private set; }

        public RelayCommand AddCommand { get; }
        private async void AddExecute()
        {
            var chats = await SharePopup.PickChatsAsync(Strings.Resources.SelectContacts, Items.Select(x => x.Id).ToArray());
            if (chats != null)
            {
                Items.ReplaceWith(chats);
            }

            SendCommand.RaiseCanExecuteChanged();
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var maxSize = CacheService.Options.BasicGroupSizeMax;

            var peers = Items.Select(x => x.Type).OfType<ChatTypePrivate>().Select(x => x.UserId).ToArray();
            if (peers.Length <= maxSize)
            {
                // Classic chat
                var response = await ProtoService.SendAsync(new CreateNewBasicGroupChat(peers, _title));
                if (response is Chat chat)
                {
                    // TODO: photo

                    NavigationService.NavigateToChat(chat);
                    NavigationService.GoBackAt(0, false);
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
