//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
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

        public ChatGalleryViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, long chatId, long threadId, Message selected, bool mirrored = false)
            : base(clientService, storageService, aggregator)
        {
            _isMirrored = mirrored;

            _group = new MvxObservableCollection<GalleryContent>();

            _chatId = chatId;
            _threadId = threadId;

            if (selected.Content is MessageAnimation)
            {
                _filter = new SearchMessagesFilterAnimation();
            }
            else if (selected.Content is MessageVideoNote)
            {
                _filter = new SearchMessagesFilterVideoNote();
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

            Items = new MvxObservableCollection<GalleryContent> { new GalleryMessage(clientService, selected) };
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

                var response = await ClientService.SendAsync(new SearchChatMessages(_chatId, string.Empty, null, fromMessageId, offset, limit, _filter, _threadId));
                if (response is FoundChatMessages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in messages.Messages.Where(x => x != null && x.Id < fromMessageId).OrderByDescending(x => x.Id))
                    {
                        if (message.Content is MessagePhoto or MessageVideo or MessageAnimation)
                        {
                            Items.Put(!_isMirrored, new GalleryMessage(ClientService, message));
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    foreach (var message in messages.Messages.Where(x => x != null && x.Id > fromMessageId).OrderBy(x => x.Id))
                    {
                        if (message.Content is MessagePhoto or MessageVideo or MessageAnimation)
                        {
                            Items.Put(_isMirrored, new GalleryMessage(ClientService, message));
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    OnSelectedItemChanged(_selectedItem);
                }
            }

            if (_firstItem is GalleryMessage first)
            {
                var response = await ClientService.SendAsync(new GetChatMessagePosition(first.ChatId, first.Id, _filter, _threadId));
                if (response is Count count)
                {
                    _firstPosition = count.CountValue;
                }
                else
                {
                    _firstPosition = 0;
                }

                RaisePropertyChanged(nameof(Position));
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

                var limit = 21;
                var offset = _isMirrored ? -limit + 1 : 0;

                var response = await ClientService.SendAsync(new SearchChatMessages(_chatId, string.Empty, null, fromMessageId, offset, limit, _filter, _threadId));
                if (response is FoundChatMessages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in _isMirrored ? messages.Messages.Where(x => x != null && x.Id > fromMessageId).OrderBy(x => x.Id) : messages.Messages.Where(x => x != null && x.Id < fromMessageId).OrderByDescending(x => x.Id))
                    {
                        if (message.Content is MessagePhoto or MessageVideo or MessageAnimation)
                        {
                            Items.Insert(0, new GalleryMessage(ClientService, message));
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

                var limit = 21;
                var offset = _isMirrored ? 0 : -limit + 1;

                var response = await ClientService.SendAsync(new SearchChatMessages(_chatId, string.Empty, null, fromMessageId, offset, limit, _filter, _threadId));
                if (response is FoundChatMessages messages)
                {
                    TotalItems = messages.TotalCount;

                    foreach (var message in _isMirrored ? messages.Messages.Where(x => x != null && x.Id < fromMessageId).OrderByDescending(x => x.Id) : messages.Messages.Where(x => x != null && x.Id > fromMessageId).OrderBy(x => x.Id))
                    {
                        if (message.Content is MessagePhoto or MessageVideo or MessageAnimation)
                        {
                            Items.Add(new GalleryMessage(ClientService, message));
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

        private int _firstPosition;
        public override int Position
        {
            get
            {
                var firstIndex = Items.IndexOf(_firstItem);
                var currentIndex = Items.IndexOf(_selectedItem);

                var position = _firstPosition + (firstIndex - currentIndex);
                return _isMirrored ? position : TotalItems - position;
            }
        }

        public override MvxObservableCollection<GalleryContent> Group => _group;
    }
}
