using System.Linq;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Gallery;

namespace Unigram.ViewModels.Chats
{
    public class ChatPhotosViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly Chat _chat;

        public ChatPhotosViewModel(IProtoService protoService, IEventAggregator aggregator, Chat chat, ChatPhoto photo)
            : base(protoService, aggregator)
        {
            _chat = chat;
            Items = new MvxObservableCollection<GalleryContent> { new GalleryChatPhoto(protoService, chat, photo) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(photo.GetBig().Photo.Id, photo.GetSmall().Photo.Id);
        }

        //public ChatPhotosViewModel(IProtoService protoService, IEventAggregator aggregator, TLChatFullBase chatFull, TLChatBase chat, TLMessageService serviceMessage)
        //    : base(protoService, aggregator)
        //{
        //    _peer = chat.ToInputPeer();
        //    _lastMaxId = serviceMessage.Id;

        //    if (serviceMessage.Action is TLMessageActionChatEditPhoto editPhotoAction)
        //    {
        //        Items = new MvxObservableCollection<GalleryItem> { new GalleryPhotoItem(editPhotoAction.Photo as TLPhoto, chat) };
        //        SelectedItem = Items[0];
        //        FirstItem = Items[0];
        //    }

        //    Initialize(serviceMessage.Id);
        //}

        private async void Initialize(long bigId, long smallId)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var limit = 20;
                var offset = -limit / 2;

                var response = await ProtoService.SendAsync(new SearchChatMessages(_chat.Id, string.Empty, 0, 0, offset, limit, new SearchMessagesFilterChatPhoto(), 0));
                if (response is Telegram.Td.Api.Messages messages)
                {
                    TotalItems = messages.TotalCount;

                    var missing = true;

                    foreach (var message in messages.MessagesValue.OrderByDescending(x => x.Id))
                    {
                        if (message.Content is MessageChatChangePhoto chatChangePhoto)
                        {
                            if (chatChangePhoto.Photo.Sizes.Any(x => x.Photo.Id == bigId))
                            {
                                missing = false;
                                continue;
                            }

                            Items.Add(new GalleryChatPhoto(ProtoService, _chat, chatChangePhoto.Photo));
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    if (missing)
                    {
                        TotalItems++;
                    }

                    OnSelectedItemChanged(_selectedItem);
                }
            }
        }

        protected override async void LoadPrevious()
        {
            //using (await _loadMoreLock.WaitAsync())
            //{
            //    var item = Items.FirstOrDefault() as GalleryMessageItem;
            //    if (item == null)
            //    {
            //        return;
            //    }

            //    var fromMessageId = item.Id;

            //    var limit = 20;
            //    var offset = -limit / 2;

            //    var response = await ProtoService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, new SearchMessagesFilterChatPhoto()));
            //    if (response is Telegram.Td.Api.Messages messages)
            //    {
            //        TotalItems = messages.TotalCount;

            //        foreach (var message in messages.MessagesValue.Where(x => x.Id < fromMessageId))
            //        {
            //            if (message.Content is MessageChatChangePhoto)
            //            {
            //                Items.Insert(0, new GalleryMessageItem(ProtoService, message));
            //            }
            //            else
            //            {
            //                TotalItems--;
            //            }
            //        }

            //        OnSelectedItemChanged(_selectedItem);
            //    }
            //}
        }

        protected override async void LoadNext()
        {
            //using (await _loadMoreLock.WaitAsync())
            //{
            //    var item = Items.LastOrDefault() as GalleryMessageItem;
            //    if (item == null)
            //    {
            //        return;
            //    }

            //    var fromMessageId = item.Id;

            //    var limit = 20;
            //    var offset = -limit / 2;

            //    var response = await ProtoService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, new SearchMessagesFilterChatPhoto()));
            //    if (response is Telegram.Td.Api.Messages messages)
            //    {
            //        TotalItems = messages.TotalCount;

            //        foreach (var message in messages.MessagesValue.Where(x => x.Id > fromMessageId).OrderBy(x => x.Id))
            //        {
            //            if (message.Content is MessageChatChangePhoto)
            //            {
            //                Items.Add(new GalleryMessageItem(ProtoService, message));
            //            }
            //            else
            //            {
            //                TotalItems--;
            //            }
            //        }

            //        OnSelectedItemChanged(_selectedItem);
            //    }
            //}
        }

        public override int Position => TotalItems - (Items.Count - base.Position);

        public override MvxObservableCollection<GalleryContent> Group => this.Items;
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
