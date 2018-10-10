using System.Linq;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Gallery;

namespace Unigram.ViewModels.Dialogs
{
    public class DialogGalleryViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly long _chatId;

        private readonly MvxObservableCollection<GalleryItem> _group;
        private long _current;

        public DialogGalleryViewModel(IProtoService protoService, IEventAggregator aggregator, long chatId, Message selected)
            : base(protoService, aggregator)
        {
            _group = new MvxObservableCollection<GalleryItem>();
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

            Items = new MvxObservableCollection<GalleryItem> { new GalleryMessageItem(protoService, selected) };
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
                if (response is Telegram.Td.Api.Messages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in messages.MessagesValue.Where(x => x.Id < fromMessageId))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo)
                        {
                            Items.Insert(0, new GalleryMessageItem(ProtoService, message));
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
                            Items.Add(new GalleryMessageItem(ProtoService, message));
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
                var item = Items.FirstOrDefault() as GalleryMessageItem;
                if (item == null)
                {
                    return;
                }

                var fromMessageId = item.Id;

                var limit = 20;
                var offset = -limit / 2;

                var response = await ProtoService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, new SearchMessagesFilterPhotoAndVideo()));
                if (response is Telegram.Td.Api.Messages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in messages.MessagesValue.Where(x => x.Id < fromMessageId))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo)
                        {
                            Items.Insert(0, new GalleryMessageItem(ProtoService, message));
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    OnSelectedItemChanged(_selectedItem);
                }
            }

            //var offset = item.Message.Id;

            //var limit = 20;
            //var req = new TLMessagesSearch
            //{
            //    Peer = _peer,
            //    Filter = new TLInputMessagesFilterPhotoVideo(),
            //    OffsetId = offset,
            //    AddOffset = 0,
            //    Limit = limit,
            //};

            ////var response = await ProtoService.SearchAsync(_peer, string.Empty, null, new TLInputMessagesFilterPhotoVideo(), 0, 0, 0, _lastMaxId, 15);
            //var response = await LegacyService.SendRequestAsync<TLMessagesMessagesBase>("messages.search", req);
            //if (response.IsSucceeded && response.Result is ITLMessages result)
            //{
            //    CacheService.SyncUsersAndChats(result.Users, result.Chats, tuple => { });

            //    foreach (var photo in result.Messages.Where(x => x.Id < offset))
            //    {
            //        if (photo is TLMessage message && (message.Media is TLMessageMediaPhoto media || message.IsVideo()))
            //        {
            //            Items.Insert(0, new GalleryMessageItem(message));
            //        }
            //        else
            //        {
            //            TotalItems--;
            //        }
            //    }

            //    OnSelectedItemChanged(_selectedItem);
            //}
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

                var fromMessageId = item.Id;

                var limit = 20;
                var offset = -limit / 2;

                var response = await ProtoService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, new SearchMessagesFilterPhotoAndVideo()));
                if (response is Telegram.Td.Api.Messages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in messages.MessagesValue.Where(x => x.Id > fromMessageId).OrderBy(x => x.Id))
                    {
                        if (message.Content is MessagePhoto || message.Content is MessageVideo)
                        {
                            Items.Add(new GalleryMessageItem(ProtoService, message));
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

        public override MvxObservableCollection<GalleryItem> Group => _group;

        protected override void OnSelectedItemChanged(GalleryItem item)
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
