//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        // One more than a page, so that the anchor the window is centred on is included and can
        // be told apart from the messages that follow it.
        private const int LoadLimit = 21;

        private readonly long _chatId;
        private readonly MessageTopic _topic;

        private readonly SearchMessagesFilter _filter;

        private readonly bool _isMirrored;

        private readonly RangeObservableCollection<GalleryMedia> _group;

        public ChatGalleryViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, long chatId, MessageTopic topic, MessageWithOwner selected, MessageProperties properties, bool mirrored = false, SearchMessagesFilter filter = null)
            : base(clientService, storageService, aggregator)
        {
            _isMirrored = mirrored;

            _group = new RangeObservableCollection<GalleryMedia>();

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

            Items = new RangeObservableCollection<GalleryMedia> { new GalleryMessage(clientService, selected, properties) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(selected.Id);
        }

        #region Paging

        private async void Initialize(long fromMessageId)
        {
            IsLoading = true;

            try
            {
                const int limit = 20;

                var messages = await SearchAsync(fromMessageId, -limit / 2, limit);
                if (messages == null)
                {
                    return;
                }

                var properties = await GetPropertiesAsync(messages);

                TotalItems = messages.TotalCount;

                Merge(messages, properties, fromMessageId, true);
                Merge(messages, properties, fromMessageId, false);

                if (_firstItem is GalleryMessage first)
                {
                    // getChatMessagePosition counts from the newest match, which is the order
                    // Items are in when mirrored and the reverse of it otherwise.
                    var response = await ClientService.SendAsync(new GetChatMessagePosition(first.ChatId, _topic, _filter, first.Id));
                    var position = response is Count count ? count.CountValue - 1 : 0;
                    var index = Items.IndexOf(first);

                    _offset = _isMirrored
                        ? position - index
                        : TotalItems - position - index - 1;
                }

                OnSelectedItemChanged(_selectedItem);
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected override Task<IncrementalLoadResult> LoadPreviousAsync()
        {
            return LoadAsync(true);
        }

        protected override Task<IncrementalLoadResult> LoadNextAsync()
        {
            return LoadAsync(false);
        }

        private async Task<IncrementalLoadResult> LoadAsync(bool prepend)
        {
            if ((prepend ? Items.FirstOrDefault() : Items.LastOrDefault()) is not GalleryMessage anchor)
            {
                return new IncrementalLoadResult(0, false);
            }

            // Newer messages precede the first item when mirrored and follow the last one
            // otherwise, so one flag picks both the side to ask for and the order to read it in.
            // A negative offset starts the window that many messages after the anchor, which is
            // only what the newer side needs.
            var ascending = prepend == _isMirrored;

            var messages = await SearchAsync(anchor.Id, ascending ? -LoadLimit + 1 : 0, LoadLimit);
            if (messages == null)
            {
                return new IncrementalLoadResult(0, false);
            }

            var properties = await GetPropertiesAsync(messages);

            TotalItems = messages.TotalCount;

            var count = Merge(messages, properties, anchor.Id, prepend);

            OnSelectedItemChanged(_selectedItem);

            // The anchor comes back with every response, so anything past it means more to come.
            // NextFromMessageId would only answer for the older side, and either side is asked here.
            return new IncrementalLoadResult((uint)count, messages.Messages.Count > 1);
        }

        private async Task<FoundChatMessages> SearchAsync(long fromMessageId, int offset, int limit)
        {
            var response = await ClientService.SendAsync(new SearchChatMessages(_chatId, _topic, string.Empty, null, fromMessageId, offset, limit, _filter));
            return response as FoundChatMessages;
        }

        private Task<IDictionary<MessageId, MessageProperties>> GetPropertiesAsync(FoundChatMessages messages)
        {
            return ClientService.GetMessagePropertiesAsync(messages.Messages.Select(x => new MessageId(x)));
        }

        /// <summary>
        /// Adds the messages that lie on one side of <paramref name="fromMessageId"/>, nearest
        /// first, and returns how many were added. The response is a window centred on the
        /// anchor, so each call keeps only the half it asked for.
        /// </summary>
        private int Merge(FoundChatMessages messages, IDictionary<MessageId, MessageProperties> properties, long fromMessageId, bool prepend)
        {
            var ascending = prepend == _isMirrored;

            var side = ascending
                ? messages.Messages.Where(x => x != null && x.Id > fromMessageId).OrderBy(x => x.Id)
                : messages.Messages.Where(x => x != null && x.Id < fromMessageId).OrderByDescending(x => x.Id);

            var count = 0;

            foreach (var message in side)
            {
                if (message.Content is MessagePhoto or MessageVideo or MessageAnimation or MessageDocument
                    && properties.TryGetValue(new MessageId(message), out MessageProperties props))
                {
                    Items.Put(prepend, new GalleryMessage(ClientService, message, props));
                    count++;
                }
                else
                {
                    TotalItems--;
                }
            }

            return count;
        }

        #endregion

        public override RangeObservableCollection<GalleryMedia> Group => _group;

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

        #region Advertisements

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

        #endregion
    }
}
