using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
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

        public ChatPhotosViewModel(IMTProtoService protoService, TLChatFullBase chatFull, TLChatBase chat)
            : base(protoService, null, null)
        {
            _peer = chat.ToInputPeer();
            _lastMaxId = int.MaxValue;

            Items = new MvxObservableCollection<GalleryItem> { new GalleryPhotoItem(chatFull.ChatPhoto as TLPhoto, chat) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize();
        }

        private async void Initialize()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.SearchAsync(_peer, string.Empty, null, new TLInputMessagesFilterChatPhotos(), 0, 0, 0, _lastMaxId, 15);
                if (response.IsSucceeded)
                {
                    if (response.Result.Messages.Count > 0)
                    {
                        if (response.Result is TLMessagesMessagesSlice slice)
                        {
                            TotalItems = slice.Count;
                        }
                        else if (response.Result is TLMessagesChannelMessages channelMessages)
                        {
                            TotalItems = channelMessages.Count;
                        }
                        else
                        {
                            TotalItems = response.Result.Messages.Count;
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
                    else
                    {
                        TotalItems = 1;
                    }
                }
            }
        }

        protected override async void LoadNext()
        {
            if (TotalItems > Items.Count)
            {
                using (await _loadMoreLock.WaitAsync())
                {
                    var response = await ProtoService.SearchAsync(_peer, string.Empty, null, new TLInputMessagesFilterChatPhotos(), 0, 0, 0, _lastMaxId, 15);
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

                        //SelectedItem = Items.LastOrDefault();
                        //FirstItem = Items.LastOrDefault();
                    }
                }
            }
        }

        public override bool CanGoto => true;
    }

    //public class GalleryChatPhotoItem : GalleryItem
    //{
    //    private readonly TLChatPhoto _photo;
    //    private readonly ITLDialogWith _from;
    //    private readonly string _caption;

    //    public GalleryChatPhotoItem(TLChatPhoto photo, ITLDialogWith from)
    //    {
    //        _photo = photo;
    //        _from = from;
    //    }

    //    public override object Source => _photo;

    //    public override string Caption => _caption;

    //    public override ITLDialogWith From => _from;

    //    public override int Date => _photo.Date;

    //    public override bool HasStickers => _photo.IsHasStickers;

    //    public override TLInputStickeredMediaBase ToInputStickeredMedia()
    //    {
    //        return new TLInputStickeredMediaPhoto { Id = _photo.ToInputPhoto() };
    //    }
    //}
}
