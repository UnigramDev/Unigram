//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.UI.Xaml;

namespace Telegram.ViewModels.Chats
{
    public partial class ChatGalleryViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new();

        private readonly long _chatId;
        private readonly MessageTopic _topic;

        private readonly SearchMessagesFilter _filter;

        private readonly bool _isMirrored;

        private readonly MvxObservableCollection<GalleryMedia> _group;

        public ChatGalleryViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, long chatId, MessageTopic topic, MessageWithOwner selected, MessageProperties properties, bool mirrored = false, SearchMessagesFilter filter = null)
            : base(clientService, storageService, aggregator)
        {
            _isMirrored = mirrored;

            _group = new MvxObservableCollection<GalleryMedia>();

            _chatId = chatId;
            _topic = topic;

            if (filter != null)
            {
                _filter = filter;
            }
            else if (selected.Content is MessageAnimation)
            {
                _filter = new SearchMessagesFilterAnimation();
            }
            else if (selected.Content is MessageVideoNote)
            {
                _filter = new SearchMessagesFilterVideoNote();
            }
            else if (selected.Content is MessageDocument)
            {
                _filter = new SearchMessagesFilterDocument();
            }
            else
            {
                _filter = new SearchMessagesFilterPhotoAndVideo();
            }

            Items = new MvxObservableCollection<GalleryMedia> { new GalleryMessage(clientService, selected, properties) };
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

                var response = await ClientService.SendAsync(new SearchChatMessages(_chatId, _topic, string.Empty, null, fromMessageId, offset, limit, _filter));
                if (response is FoundChatMessages messages)
                {
                    var properties = await ClientService.GetMessagePropertiesAsync(messages.Messages.Select(x => new MessageId(x)));

                    TotalItems = messages.TotalCount;

                    foreach (var message in messages.Messages.Where(x => x != null && x.Id < fromMessageId).OrderByDescending(x => x.Id))
                    {
                        if (message.Content is MessagePhoto or MessageVideo or MessageAnimation or MessageDocument && properties.TryGetValue(new MessageId(message), out MessageProperties props))
                        {
                            Items.Put(!_isMirrored, new GalleryMessage(ClientService, message, props));
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    foreach (var message in messages.Messages.Where(x => x != null && x.Id > fromMessageId).OrderBy(x => x.Id))
                    {
                        if (message.Content is MessagePhoto or MessageVideo or MessageAnimation or MessageDocument && properties.TryGetValue(new MessageId(message), out MessageProperties props))
                        {
                            Items.Put(_isMirrored, new GalleryMessage(ClientService, message, props));
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
                var response = await ClientService.SendAsync(new GetChatMessagePosition(first.ChatId, _topic, _filter, first.Id));
                if (response is Count count)
                {
                    _firstPosition = count.CountValue - 1;
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

                var response = await ClientService.SendAsync(new SearchChatMessages(_chatId, _topic, string.Empty, null, fromMessageId, offset, limit, _filter));
                if (response is FoundChatMessages messages)
                {
                    var properties = await ClientService.GetMessagePropertiesAsync(messages.Messages.Select(x => new MessageId(x)));

                    TotalItems = messages.TotalCount;

                    foreach (var message in _isMirrored ? messages.Messages.Where(x => x != null && x.Id > fromMessageId).OrderBy(x => x.Id) : messages.Messages.Where(x => x != null && x.Id < fromMessageId).OrderByDescending(x => x.Id))
                    {
                        if (message.Content is MessagePhoto or MessageVideo or MessageAnimation or MessageDocument && properties.TryGetValue(new MessageId(message), out MessageProperties props))
                        {
                            Items.Insert(0, new GalleryMessage(ClientService, message, props));
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

                var response = await ClientService.SendAsync(new SearchChatMessages(_chatId, _topic, string.Empty, null, fromMessageId, offset, limit, _filter));
                if (response is FoundChatMessages messages)
                {
                    var properties = await ClientService.GetMessagePropertiesAsync(messages.Messages.Select(x => new MessageId(x)));

                    TotalItems = messages.TotalCount;

                    foreach (var message in _isMirrored ? messages.Messages.Where(x => x != null && x.Id < fromMessageId).OrderByDescending(x => x.Id) : messages.Messages.Where(x => x != null && x.Id > fromMessageId).OrderBy(x => x.Id))
                    {
                        if (message.Content is MessagePhoto or MessageVideo or MessageAnimation or MessageDocument && properties.TryGetValue(new MessageId(message), out MessageProperties props))
                        {
                            Items.Add(new GalleryMessage(ClientService, message, props));
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
                return _isMirrored ? position + 1 : TotalItems - position;
            }
        }

        public override MvxObservableCollection<GalleryMedia> Group => _group;

        public override void View()
        {
            FirstItem = null;

            var message = _selectedItem as GalleryMessage;
            if (message == null || !message.CanBeViewed)
            {
                return;
            }

            NavigationService.NavigateToChat(message.ChatId, message.Id, _topic);
        }

        private DispatcherTimer _advertisementsTimer;
        private GalleryMessage _advertisementsSource;

        private bool _advertisementsHidden;

        public override async void PlaybackStarted(GalleryMedia item)
        {
            if (_advertisementsHidden || item is not GalleryMessage { CanGetVideoAdvertisements: true } message)
            {
                return;
            }

            if (_advertisementsSource?.ChatId == message.ChatId && _advertisementsSource?.Id == message.Id)
            {
                return;
            }

            _advertisementsSource = message;

            if (message.Advertisements == null)
            {
                message.Advertisements = new VideoMessageAdvertisements(Array.Empty<VideoMessageAdvertisement>(), -1, -1);

                var response = await ClientService.SendAsync(new GetVideoMessageAdvertisements(message.ChatId, message.Id));
                if (response is VideoMessageAdvertisements advertisements)
                {
                    message.Advertisements = advertisements;
                }

                if (_advertisementsSource != null && (_advertisementsSource?.ChatId != message.ChatId || _advertisementsSource?.Id != message.Id))
                {
                    return;
                }
            }

            if (message.Advertisements.Advertisements.Empty())
            {
                return;
            }

            if (_advertisementsTimer == null)
            {
                _advertisementsTimer = new DispatcherTimer();
                _advertisementsTimer.Tick += AdvertisementsTimer_Tick;
            }
            else
            {
                _advertisementsTimer.Stop();
            }

            message.AdvertisementsSelectedIndex = 0;

            _advertisementsTimer.Interval = TimeSpan.FromSeconds(message.Advertisements.StartDelay);
            _advertisementsTimer.Start();
        }

        private void AdvertisementsTimer_Tick(object sender, object e)
        {
            _advertisementsTimer.Stop();

            if (SelectedItem is not GalleryMessage message)
            {
                return;
            }

            if (_advertisementsSource == null || (_advertisementsSource.ChatId != message.ChatId || _advertisementsSource.Id != message.Id))
            {
                _advertisementsSource = null;
                return;
            }

            Delegate?.UpdateAdvertisement(_advertisementsSource.GetNextAdvertisement());
        }

        public override void PlaybackStopped()
        {
            _advertisementsTimer?.Stop();
            _advertisementsSource = null;

            Delegate?.UpdateAdvertisement(null);
        }

        public override void AdvertisementDisplayed()
        {
            if (SelectedItem is not GalleryMessage message)
            {
                return;
            }

            if (_advertisementsSource == null || (_advertisementsSource.ChatId != message.ChatId || _advertisementsSource.Id != message.Id))
            {
                _advertisementsSource = null;
                return;
            }

            _advertisementsTimer.Interval = TimeSpan.FromSeconds(message.Advertisements.BetweenDelay);
            _advertisementsTimer.Start();
        }

        public override void HideAdvertisement()
        {
            if (IsPremium)
            {
                _advertisementsHidden = true;
                ClientService.Send(new ToggleHasSponsoredMessagesEnabled(false));

                ToastPopup.Show(XamlRoot, Strings.AdHidden, ToastPopupIcon.AntiSpam);
            }
            else if (IsPremiumAvailable)
            {
                NavigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureDisabledAds()));
            }
        }
    }
}
