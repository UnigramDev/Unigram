using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Views;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatDetailsViewModel : UnigramViewModelBase
    {
        private readonly IUploadFileManager _uploadFileManager;

        public ChatDetailsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager)
            : base(protoService, cacheService, aggregator)
        {
            _uploadFileManager = uploadFileManager;
        }

        private TLChat _item;
        public TLChat Item
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
            }
        }

        private TLChatFull _full;
        public TLChatFull Full
        {
            get
            {
                return _full;
            }
            set
            {
                Set(ref _full, value);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Item = null;
            Full = null;

            var chat = parameter as TLChat;
            var peer = parameter as TLPeerChat;
            if (peer != null)
            {
                chat = CacheService.GetChat(peer.ChatId) as TLChat;
            }

            if (chat != null)
            {
                Item = chat;

                var response = await ProtoService.GetFullChatAsync(chat.Id);
                if (response.IsSucceeded)
                {
                    var collection = new SortedObservableCollection<TLChatParticipantBase>(new TLChatParticipantBaseComparer(true));
                    Full = response.Result.FullChat as TLChatFull;
                    Participants = collection;

                    RaisePropertyChanged(() => Participants);

                    if (_full.Participants is TLChatParticipants participants)
                    {
                        collection.AddRange(participants.Participants, true);
                    }
                }
            }
        }

        public SortedObservableCollection<TLChatParticipantBase> Participants { get; private set; }

        #region Helper props

        public bool CanEditNameAndPhoto
        {
            get
            {
                return _item != null && (_item.IsCreator || _item.IsAdmin || !_item.IsAdminsEnabled);
            }
        }

        #endregion

        public RelayCommand<StorageFile> EditPhotoCommand => new RelayCommand<StorageFile>(EditPhotoExecute);
        private async void EditPhotoExecute(StorageFile file)
        {
            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.jpg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            //var fileScale = await ImageHelper.ScaleJpegAsync(file, fileCache, 640, 0.77);

            await file.CopyAndReplaceAsync(fileCache);
            var fileScale = fileCache;

            var basicProps = await fileScale.GetBasicPropertiesAsync();
            var imageProps = await fileScale.Properties.GetImagePropertiesAsync();

            var fileId = TLLong.Random();
            var upload = await _uploadFileManager.UploadFileAsync(fileId, fileCache.Name, false);
            if (upload != null)
            {
                var response = await ProtoService.EditChatPhotoAsync(_item.Id, new TLInputChatUploadedPhoto { File = upload.ToInputFile() });
                if (response.IsSucceeded)
                {
                    //var photo = response.Result.Photo as TLPhoto;
                }
            }
        }

        public RelayCommand MediaCommand => new RelayCommand(MediaExecute);
        private void MediaExecute()
        {
            var chat = Item as TLChat;
            if (chat != null)
            {
                NavigationService.Navigate(typeof(DialogSharedMediaPage), new TLInputPeerChat { ChatId = chat.Id });
            }
        }

        public RelayCommand<TLChatParticipantBase> ParticipantRemoveCommand => new RelayCommand<TLChatParticipantBase>(ParticipantRemoveExecute);
        private async void ParticipantRemoveExecute(TLChatParticipantBase participant)
        {
            if (participant == null || participant.User == null)
            {
                return;
            }

            var confirm = await TLMessageDialog.ShowAsync(string.Format("Do you want to remove {0} from the group {1}?", participant.User.FullName, _item.DisplayName), "Remove", "OK", "Cancel");
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ProtoService.DeleteChatUserAsync(_item.Id, participant.User.ToInputUser());
                if (response.IsSucceeded)
                {
                    if (response.Result is TLUpdates updates)
                    {
                        var newMessage = updates.Updates.OfType<TLUpdateNewMessage>().FirstOrDefault();
                        if (newMessage != null)
                        {
                            Aggregator.Publish(newMessage);
                        }
                    }
                }
            }
        }
    }

    public class TLChatParticipantBaseComparer : IComparer<TLChatParticipantBase>
    {
        private bool _epoch;

        public TLChatParticipantBaseComparer(bool epoch)
        {
            _epoch = epoch;
        }

        public int Compare(TLChatParticipantBase x, TLChatParticipantBase y)
        {
            var xUser = x.User;
            var yUser = y.User;

            if (xUser == null || yUser == null)
            {
                return -1;
            }

            if (_epoch)
            {
                var epoch = LastSeenConverter.GetIndex(yUser).CompareTo(LastSeenConverter.GetIndex(xUser));
                if (epoch == 0)
                {
                    var fullName = xUser.FullName.CompareTo(yUser.FullName);
                    if (fullName == 0)
                    {
                        return yUser.Id.CompareTo(xUser.Id);
                    }

                    return fullName;
                }

                return epoch;
            }
            else
            {
                var fullName = xUser.FullName.CompareTo(yUser.FullName);
                if (fullName == 0)
                {
                    return yUser.Id.CompareTo(xUser.Id);
                }

                return fullName;
            }
        }
    }
}
