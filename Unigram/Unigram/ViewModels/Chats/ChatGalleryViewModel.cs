using System.Linq;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Gallery;

namespace Unigram.ViewModels.Chats
{
    public class ChatGalleryViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();

        private readonly long _chatId;
        private readonly long _threadId;

        private readonly SearchMessagesFilter _filter;

        private readonly bool _isMirrored;

        private readonly MvxObservableCollection<GalleryContent> _group;

        public ChatGalleryViewModel(IProtoService protoService, IEventAggregator aggregator, long chatId, long threadId, Message selected, bool mirrored = false)
            : base(protoService, aggregator)
        {
            _isMirrored = mirrored;

            _group = new MvxObservableCollection<GalleryContent>();

            _chatId = chatId;
            _threadId = threadId;

            if (selected.Content is MessageAnimation)
            {
                _filter = new SearchMessagesFilterAnimation();
            }
            else
            {
                _filter = new SearchMessagesFilterPhotoAndVideo();
            }

            //if (selected.Media is TLMessageMediaPhoto photoMedia || selected.IsVideo())
            //{
            //    Items = new MvxObservableCollection<GalleryItem> { new GalleryLegacyMessageItem(selected) };
            //    SelectedItem = Items[0];
            //    FirstItem = Items[0];
            //}
            //else
            //{
            //    Items = new MvxObservableCollection<GalleryItem>();
            //}

            //Initialize(selected.Id);

            Items = new MvxObservableCollection<GalleryContent> { new GalleryMessage(protoService, selected) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(selected.Id);
        }

        private async void Initialize(long fromMessageId)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var limit = 20;
                var offset = -limit / 2;

                var response = await ProtoService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, _filter, _threadId));
                if (response is Messages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in messages.MessagesValue.Where(x => x != null && x.Id < fromMessageId).OrderByDescending(x => x.Id))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo || message.Content is MessageAnimation)
                        {
                            Items.Put(!_isMirrored, new GalleryMessage(ProtoService, message));
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    foreach (var message in messages.MessagesValue.Where(x => x != null && x.Id > fromMessageId).OrderBy(x => x.Id))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo || message.Content is MessageAnimation)
                        {
                            Items.Put(_isMirrored, new GalleryMessage(ProtoService, message));
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

        protected override async void LoadPrevious()
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var item = Items.FirstOrDefault() as GalleryMessage;
                if (item == null)
                {
                    return;
                }

                var fromMessageId = item.Id;

                var limit = 20;
                var offset = _isMirrored ? -limit : 0;

                var response = await ProtoService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, _filter, _threadId));
                if (response is Messages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in _isMirrored ? messages.MessagesValue.Where(x => x != null && x.Id > fromMessageId).OrderBy(x => x.Id) : messages.MessagesValue.Where(x => x != null && x.Id < fromMessageId).OrderByDescending(x => x.Id))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo || message.Content is MessageAnimation)
                        {
                            Items.Insert(0, new GalleryMessage(ProtoService, message));
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
                var item = Items.LastOrDefault() as GalleryMessage;
                if (item == null)
                {
                    return;
                }

                var fromMessageId = item.Id;

                var limit = 20;
                var offset = _isMirrored ? 0 : -limit;

                var response = await ProtoService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, _filter, _threadId));
                if (response is Messages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in _isMirrored ? messages.MessagesValue.Where(x => x != null && x.Id < fromMessageId).OrderByDescending(x => x.Id) : messages.MessagesValue.Where(x => x != null && x.Id > fromMessageId).OrderBy(x => x.Id))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo || message.Content is MessageAnimation)
                        {
                            Items.Add(new GalleryMessage(ProtoService, message));
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

        public override int Position => _isMirrored ? base.Position : TotalItems - (Items.Count - base.Position);

        public override MvxObservableCollection<GalleryContent> Group => _group;
    }
}
