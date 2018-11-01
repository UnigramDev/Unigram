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

        private readonly bool _isMirrored;

        private readonly MvxObservableCollection<GalleryContent> _group;
        private long _current;

        public ChatGalleryViewModel(IProtoService protoService, IEventAggregator aggregator, long chatId, Message selected, bool mirrored = false)
            : base(protoService, aggregator)
        {
            _isMirrored = mirrored;

            _group = new MvxObservableCollection<GalleryContent>();
            _chatId = chatId;

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

                var response = await ProtoService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, new SearchMessagesFilterPhotoAndVideo()));
                if (response is Messages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in messages.MessagesValue.Where(x => x.Id < fromMessageId).OrderByDescending(x => x.Id))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo)
                        {
                            Items.Put(!_isMirrored, new GalleryMessage(ProtoService, message));
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    foreach (var message in messages.MessagesValue.Where(x => x.Id > fromMessageId).OrderBy(x => x.Id))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo)
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

                var response = await ProtoService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, new SearchMessagesFilterPhotoAndVideo()));
                if (response is Messages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in _isMirrored ? messages.MessagesValue.Where(x => x.Id > fromMessageId).OrderBy(x => x.Id) : messages.MessagesValue.Where(x => x.Id < fromMessageId).OrderByDescending(x => x.Id))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo)
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

                var response = await ProtoService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, new SearchMessagesFilterPhotoAndVideo()));
                if (response is Messages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in _isMirrored ? messages.MessagesValue.Where(x => x.Id < fromMessageId).OrderByDescending(x => x.Id) : messages.MessagesValue.Where(x => x.Id > fromMessageId).OrderBy(x => x.Id))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo)
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

        protected override void OnSelectedItemChanged(GalleryContent item)
        {
            //var messageItem = item as GalleryLegacyMessageItem;
            //if (messageItem == null)
            //{
            //    return;
            //}

            //var message = messageItem.Message as TLMessage;
            //if (message == null)
            //{
            //    return;
            //}

            //if (message.GroupedId is long group)
            //{
            //    var all = Items.Where(x => x is GalleryLegacyMessageItem msg && msg.Message.GroupedId == group).ToList();
            //    if (all.Count == _group.Count && group == _current)
            //    {
            //        return;
            //    }

            //    _current = group;
            //    _group.ReplaceWith(all);

            //    RaisePropertyChanged(() => SelectedItem);
            //}
            //else
            //{
            //    _group.Clear();
            //}
        }
    }
}
