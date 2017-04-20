using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Views;
using Unigram.Views.Channels;
using Windows.Storage;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelDetailsViewModel : ChannelParticipantsViewModelBase
    {
        private readonly IUploadFileManager _uploadFileManager;

        public ChannelDetailsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager)
            : base(protoService, cacheService, aggregator, null)
        {
            _uploadFileManager = uploadFileManager;
        }

        private TLChannelFull _full;
        public TLChannelFull Full
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
            // SHOULD NOT CALL BASE!

            Item = null;
            Full = null;

            var channel = parameter as TLChannel;
            var peer = parameter as TLPeerChannel;
            if (peer != null)
            {
                channel = CacheService.GetChat(peer.ChannelId) as TLChannel;
            }

            if (channel != null)
            {
                Item = channel;

                var response = await ProtoService.GetFullChannelAsync(channel.ToInputChannel());
                if (response.IsSucceeded)
                {
                    Full = response.Result.FullChat as TLChannelFull;
                    Participants = new ItemsCollection(ProtoService, channel.ToInputChannel(), null);

                    RaisePropertyChanged(() => Participants);
                }
            }
        }

        public bool CanEditNameAndPhoto
        {
            get
            {
                return _item != null && (_item.IsCreator || _item.IsEditor || _item.IsModerator);
            }
        }

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

            await file.CopyAndReplaceAsync(fileCache);
            var fileScale = fileCache;

            var basicProps = await fileScale.GetBasicPropertiesAsync();
            var imageProps = await fileScale.Properties.GetImagePropertiesAsync();

            var fileId = TLLong.Random();
            var upload = await _uploadFileManager.UploadFileAsync(fileId, fileCache.Name, false);
            if (upload != null)
            {
                var response = await ProtoService.EditPhotoAsync(_item, new TLInputChatUploadedPhoto { File = upload.ToInputFile() });
                if (response.IsSucceeded)
                {

                }
            }
        }

        public RelayCommand MediaCommand => new RelayCommand(MediaExecute);
        private void MediaExecute()
        {
            NavigationService.Navigate(typeof(DialogSharedMediaPage), _item.ToInputPeer());
        }

        public RelayCommand AdminsCommand => new RelayCommand(AdminsExecute);
        private void AdminsExecute()
        {
            NavigationService.Navigate(typeof(ChannelAdminsPage), _item.ToPeer());
        }

        public RelayCommand KickedCommand => new RelayCommand(KickedExecute);
        private void KickedExecute()
        {
            NavigationService.Navigate(typeof(ChannelKickedPage), _item.ToPeer());
        }

        public RelayCommand ParticipantsCommand => new RelayCommand(ParticipantsExecute);
        private void ParticipantsExecute()
        {
            NavigationService.Navigate(typeof(ChannelParticipantsPage), _item.ToPeer());
        }
    }

    public class TLChannelParticipantBaseComparer : IComparer<TLChannelParticipantBase>
    {
        private bool _epoch;

        public TLChannelParticipantBaseComparer(bool epoch)
        {
            _epoch = epoch;
        }

        public int Compare(TLChannelParticipantBase x, TLChannelParticipantBase y)
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
