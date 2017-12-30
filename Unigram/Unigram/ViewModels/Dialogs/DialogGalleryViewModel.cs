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
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Telegram.Api.TL.Messages.Methods;
using Template10.Common;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Core.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Dialogs
{
    public class DialogGalleryViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly TLInputPeerBase _peer;

        private readonly MvxObservableCollection<GalleryItem> _group;
        private long _current;

        public DialogGalleryViewModel(IMTProtoService protoService, ICacheService cacheService, TLInputPeerBase peer, TLMessage selected)
            : base(protoService, cacheService, null)
        {
            _group = new MvxObservableCollection<GalleryItem>();
            _peer = peer;

            if (selected.Media is TLMessageMediaPhoto photoMedia || selected.IsVideo())
            {
                Items = new MvxObservableCollection<GalleryItem> { new GalleryMessageItem(selected) };
                SelectedItem = Items[0];
                FirstItem = Items[0];
            }
            else
            {
                Items = new MvxObservableCollection<GalleryItem>();
            }

            Initialize(selected.Id);
        }

        private async void Initialize(int offset)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var limit = 20;
                var req = new TLMessagesSearch
                {
                    Peer = _peer,
                    Filter = new TLInputMessagesFilterPhotoVideo(),
                    OffsetId = offset,
                    AddOffset = -limit / 2,
                    Limit = limit,
                };

                //var response = await ProtoService.SearchAsync(_peer, string.Empty, null, new TLInputMessagesFilterPhotoVideo(), 0, 0, 0, _lastMaxId, 15);
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
                        if (photo is TLMessage message && (message.Media is TLMessageMediaPhoto media || message.IsVideo()))
                        {
                            Items.Insert(0, new GalleryMessageItem(message));
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    foreach (var photo in result.Messages.Where(x => x.Id > offset).OrderBy(x => x.Id))
                    {
                        if (photo is TLMessage message && (message.Media is TLMessageMediaPhoto media || message.IsVideo()))
                        {
                            Items.Add(new GalleryMessageItem(message));
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

                var offset = item.Message.Id;

                var limit = 20;
                var req = new TLMessagesSearch
                {
                    Peer = _peer,
                    Filter = new TLInputMessagesFilterPhotoVideo(),
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
                        if (photo is TLMessage message && (message.Media is TLMessageMediaPhoto media || message.IsVideo()))
                        {
                            Items.Insert(0, new GalleryMessageItem(message));
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
                    Filter = new TLInputMessagesFilterPhotoVideo(),
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
                        if (photo is TLMessage message && (message.Media is TLMessageMediaPhoto media || message.IsVideo()))
                        {
                            Items.Add(new GalleryMessageItem(message));
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
            var messageItem = item as GalleryMessageItem;
            if (messageItem == null)
            {
                return;
            }

            var message = messageItem.Message as TLMessage;
            if (message == null)
            {
                return;
            }

            if (message.GroupedId is long group)
            {
                var all = Items.Where(x => x is GalleryMessageItem msg && msg.Message.GroupedId == group).ToList();
                if (all.Count == _group.Count && group == _current)
                {
                    return;
                }

                _current = group;
                _group.ReplaceWith(all);

                RaisePropertyChanged(() => SelectedItem);
            }
            else
            {
                _group.Clear();
            }
        }
    }
}
