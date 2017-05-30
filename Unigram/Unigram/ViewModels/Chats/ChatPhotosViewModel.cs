using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Common;
using Unigram.ViewModels.Users;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Chats
{
    public class ChatPhotosViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly TLInputPeerBase _peer;

        private int _lastMaxId;

        public ChatPhotosViewModel(IMTProtoService protoService, TLInputPeerBase peer)
            : base(protoService, null, null)
        {
            _peer = peer;
            _lastMaxId = int.MaxValue;

            Items = new MvxObservableCollection<GalleryItem>();
            Initialize();
        }

        private async void Initialize()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.SearchAsync(_peer, string.Empty, new TLInputMessagesFilterChatPhotos(), 0, 0, 0, _lastMaxId, 15);
                if (response.IsSucceeded)
                {
                    if (response.Result is TLMessagesMessagesSlice)
                    {
                        var slice = response.Result as TLMessagesMessagesSlice;
                        TotalItems = slice.Count;
                    }
                    else
                    {
                        TotalItems = response.Result.Messages.Count + Items.Count;
                    }

                    //Items.Clear();

                    var items = new List<GalleryItem>(response.Result.Messages.Count);

                    foreach (var photo in response.Result.Messages)
                    {
                        if (photo is TLMessageService message && message.Action is TLMessageActionChatEditPhoto)
                        {
                            items.Insert(0, new GalleryMessageServiceItem(message));
                            _lastMaxId = message.Id;
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    Items.ReplaceWith(items);
                    SelectedItem = Items.LastOrDefault();
                    FirstItem = Items.LastOrDefault();
                }
            }
        }

        protected override async void LoadNext()
        {
            if (TotalItems > Items.Count)
            {
                using (await _loadMoreLock.WaitAsync())
                {
                    var response = await ProtoService.SearchAsync(_peer, string.Empty, new TLInputMessagesFilterChatPhotos(), 0, 0, 0, _lastMaxId, 15);
                    if (response.IsSucceeded)
                    {
                        foreach (var photo in response.Result.Messages)
                        {
                            if (photo is TLMessageService message && message.Action is TLMessageActionChatEditPhoto)
                            {
                                Items.Insert(0, new GalleryMessageServiceItem(message));
                            }
                            else
                            {
                                TotalItems--;
                            }
                        }

                        SelectedItem = Items.LastOrDefault();
                        FirstItem = Items.LastOrDefault();
                    }
                }
            }
        }
    }

    public class GalleryMessageServiceItem : GalleryItem
    {
        private readonly TLMessageService _message;

        public GalleryMessageServiceItem(TLMessageService message)
        {
            _message = message;
        }

        public TLMessageService Message => _message;

        public override object Source
        {
            get
            {
                if (_message.Action is TLMessageActionChatEditPhoto chatEditPhotoAction && chatEditPhotoAction.Photo is TLPhoto photo)
                {
                    return photo;
                }

                return null;
            }
        }

        //public override ITLDialogWith From => _message.IsPost ? _message.Parent : _message.From;

        public override ITLDialogWith From
        {
            get
            {
                return _message.IsPost ? _message.Parent : _message.From;
            }
        }

        public override int Date => _message.Date;

        public override bool HasStickers
        {
            get
            {
                if (_message.Action is TLMessageActionChatEditPhoto chatEditPhotoAction && chatEditPhotoAction.Photo is TLPhoto photo)
                {
                    return photo.IsHasStickers;
                }

                return false;
            }
        }

        public override TLInputStickeredMediaBase ToInputStickeredMedia()
        {
            if (_message.Action is TLMessageActionChatEditPhoto chatEditPhotoAction && chatEditPhotoAction.Photo is TLPhoto photo)
            {
                return new TLInputStickeredMediaPhoto { Id = photo.ToInputPhoto() };
            }

            return null;
        }
    }
}
