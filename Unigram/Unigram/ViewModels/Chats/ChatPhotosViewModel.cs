using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Telegram.Api.TL.Messages.Methods;
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

        public ChatPhotosViewModel(IMTProtoService protoService, ICacheService cacheService, TLChatFullBase chatFull, TLChatBase chat)
            : base(protoService, cacheService, null)
        {
            _peer = chat.ToInputPeer();
            _lastMaxId = int.MaxValue;

            Items = new MvxObservableCollection<GalleryItem> { new GalleryPhotoItem(chatFull.ChatPhoto as TLPhoto, chat) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(int.MaxValue);
        }

        public ChatPhotosViewModel(IMTProtoService protoService, ICacheService cacheService, TLChatFullBase chatFull, TLChatBase chat, TLMessageService serviceMessage)
            : base(protoService, cacheService, null)
        {
            _peer = chat.ToInputPeer();
            _lastMaxId = serviceMessage.Id;

            if (serviceMessage.Action is TLMessageActionChatEditPhoto editPhotoAction)
            {
                Items = new MvxObservableCollection<GalleryItem> { new GalleryPhotoItem(editPhotoAction.Photo as TLPhoto, chat) };
                SelectedItem = Items[0];
                FirstItem = Items[0];
            }

            Initialize(serviceMessage.Id);
        }

        private async void Initialize(int offset)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var limit = 20;
                var req = new TLMessagesSearch
                {
                    Peer = _peer,
                    Filter = new TLInputMessagesFilterChatPhotos(),
                    FromId = null,
                    OffsetId = offset,
                    AddOffset = -limit / 2,
                    Limit = limit,
                };

                //var response = await ProtoService.SearchAsync(_peer, string.Empty, null, new TLInputMessagesFilterChatPhotos(), 0, 0, 0, _lastMaxId, 15);
                var response = await ProtoService.SendRequestAsync<TLMessagesMessagesBase>("messages.search", req);
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    CacheService.SyncUsersAndChats(result.Users, result.Chats, tuple => { });

                    var current = 1;
                    if (response.Result is TLMessagesMessagesSlice slice)
                    {
                        TotalItems = slice.Count + current;
                    }
                    else if (response.Result is TLMessagesChannelMessages channelMessages)
                    {
                        TotalItems = channelMessages.Count + current;
                    }
                    else
                    {
                        TotalItems = result.Messages.Count + current;
                    }

                    foreach (var photo in result.Messages.Where(x => x.Id < offset))
                    {
                        if (photo is TLMessageService message && message.Action is TLMessageActionChatEditPhoto)
                        {
                            Items.Insert(0, new GalleryMessageServiceItem(message));
                            _lastMaxId = message.Id;
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    foreach (var photo in result.Messages.Where(x => x.Id > offset).OrderBy(x => x.Id))
                    {
                        if (photo is TLMessageService message && message.Action is TLMessageActionChatEditPhoto)
                        {
                            Items.Add(new GalleryMessageServiceItem(message));
                            _lastMaxId = message.Id;
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }
                }
            }
        }

        protected override async void LoadPrevious()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var item = Items.FirstOrDefault() as GalleryMessageItem;
                if (item == null)
                {
                    return;
                }

                var offset = item.Message.Id;

                var limit = 20;
                var req = new TLMessagesSearch
                {
                    Peer = _peer,
                    Filter = new TLInputMessagesFilterChatPhotos(),
                    OffsetId = offset,
                    AddOffset = 0,
                    Limit = limit,
                };

                //var response = await ProtoService.SearchAsync(_peer, string.Empty, null, new TLInputMessagesFilterPhotoVideo(), 0, 0, 0, _lastMaxId, 15);
                var response = await ProtoService.SendRequestAsync<TLMessagesMessagesBase>("messages.search", req);
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    CacheService.SyncUsersAndChats(result.Users, result.Chats, tuple => { });

                    foreach (var photo in result.Messages.Where(x => x.Id < offset))
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

                    OnSelectedItemChanged(_selectedItem);
                }
            }
        }

        protected override async void LoadNext()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var item = Items.LastOrDefault() as GalleryMessageItem;
                if (item == null)
                {
                    return;
                }

                var offset = item.Message.Id;

                var limit = 20;
                var req = new TLMessagesSearch
                {
                    Peer = _peer,
                    Filter = new TLInputMessagesFilterChatPhotos(),
                    OffsetId = offset + 1,
                    AddOffset = -limit,
                    Limit = limit,
                };

                //var response = await ProtoService.SearchAsync(_peer, string.Empty, null, new TLInputMessagesFilterPhotoVideo(), 0, 0, 0, _lastMaxId, 15);
                var response = await ProtoService.SendRequestAsync<TLMessagesMessagesBase>("messages.search", req);
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    CacheService.SyncUsersAndChats(result.Users, result.Chats, tuple => { });

                    foreach (var photo in result.Messages.Where(x => x.Id > offset).OrderBy(x => x.Id))
                    {
                        if (photo is TLMessageService message && message.Action is TLMessageActionChatEditPhoto)
                        {
                            Items.Add(new GalleryMessageServiceItem(message));
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    OnSelectedItemChanged(_selectedItem);
                }
            }
        }

        public override int Position => TotalItems - (Items.Count - base.Position);

        public override MvxObservableCollection<GalleryItem> Group => this.Items;
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
