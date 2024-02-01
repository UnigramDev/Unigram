using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Profile
{
    public class ProfileSavedChatsTabViewModel : ViewModelBase, IHandle, IIncrementalCollectionOwner, IDelegable<ISavedMessagesChatsDelegate>
    {
        private readonly HashSet<SavedMessagesTopic> _pinnedTopics = new();
        private readonly HashSet<long> _topics = new();

        private string _nextOffset = string.Empty;

        private readonly DisposableMutex _loadMoreLock = new();
        private long _lastTopicId;
        private long _lastOrder;

        public ISavedMessagesChatsDelegate Delegate { get; set; }

        public ProfileSavedChatsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<SavedMessagesChat>(this);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateSavedMessagesChatOrder>(this, Handle)
                .Subscribe<UpdateSavedMessagesChatLastMessage>(Handle);
        }

        private void Handle(UpdateSavedMessagesChatOrder update)
        {
            BeginOnUIThread(() => UpdateChatOrder(update.Topic, update.Order, true));
        }

        private void Handle(UpdateSavedMessagesChatLastMessage update)
        {
            BeginOnUIThread(() => UpdateChatOrder(update.Topic, update.Order, true));
        }

        public IncrementalCollection<SavedMessagesChat> Items { get; private set; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ClientService.GetSavedMessagesChatsAsync(Items.Count, 20);
                if (response is IList<SavedMessagesChat> topics)
                {
                    foreach (var topic in topics)
                    {
                        var order = topic.Order;
                        if (order != 0)
                        {
                            // TODO: is this redundant?
                            var next = NextIndexOf(topic, order);
                            if (next >= 0)
                            {
                                if (_topics.Contains(topic.Id))
                                {
                                    Items.Remove(topic);
                                }

                                _topics.Add(topic.Id);
                                Items.Insert(Math.Min(Items.Count, next), topic);

                                totalCount++;
                            }

                            _lastTopicId = topic.Id;
                            _lastOrder = order;
                        }
                    }

                    HasMoreItems = topics.Count > 0;
                    Subscribe();
                }

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }
        }

        private async void UpdateChatOrder(SavedMessagesChat topic, long order, bool lastMessage)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                if (order > 0 && (order > _lastOrder || (order == _lastOrder && topic.Id >= _lastTopicId)))
                {
                    var next = NextIndexOf(topic, order);
                    if (next >= 0)
                    {
                        if (_topics.Contains(topic.Id))
                        {
                            Items.Remove(topic);
                        }

                        _topics.Add(topic.Id);
                        Items.Insert(Math.Min(Items.Count, next), topic);

                        if (next == Items.Count - 1)
                        {
                            _lastTopicId = topic.Id;
                            _lastOrder = order;
                        }
                    }
                    else if (lastMessage)
                    {
                        //_viewModel.Delegate?.UpdateChatLastMessage(topic);
                        Delegate?.UpdateSavedMessagesTopicLastMessage(topic);
                    }
                }
                else if (_topics.Contains(topic.Id))
                {
                    _topics.Remove(topic.Id);
                    Items.Remove(topic);
                }
            }
        }

        private int NextIndexOf(SavedMessagesChat topic, long order)
        {
            var prev = -1;
            var next = 0;

            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                if (item.Id == topic.Id)
                {
                    prev = i;
                    continue;
                }

                if (order > item.Order || order == item.Order && topic.Id >= item.Id)
                {
                    return next == prev ? -1 : next;
                }

                next++;
            }

            return Items.Count;
        }

        public bool HasMoreItems { get; private set; } = true;

        public bool IsPinned(SavedMessagesChat topic)
        {
            return _pinnedTopics.Contains(topic.Topic);
        }


        public void PinTopic(SavedMessagesChat topic)
        {
            if (_pinnedTopics.Count < ClientService.Options.PinnedSavedMessagesTopicCountMax)
            {
                _pinnedTopics.Add(topic.Topic);
                Items.Remove(topic);
                Items.Insert(0, topic);

                ClientService.Send(new ToggleSavedMessagesTopicIsPinned(topic.Topic, true));
            }
            else
            {
                NavigationService.ShowLimitReached(new PremiumLimitTypePinnedSavedMessagesTopicCount());
            }
        }

        public void UnpinTopic(SavedMessagesChat topic)
        {
            _pinnedTopics.Remove(topic.Topic);
            Items.Remove(topic);

            var index = Items.BinarySearch(topic.LastMessage.Date, (x, y) => y.LastMessage.Date.CompareTo(x));
            if (index < 0 && (index < Items.Count - 1 || !HasMoreItems))
            {
                Items.Insert(~index, topic);
            }

            ClientService.Send(new ToggleSavedMessagesTopicIsPinned(topic.Topic, false));
        }

        public async void DeleteTopic(SavedMessagesChat topic)
        {
            string message;
            string title;
            string primary;

            if (topic.Topic is SavedMessagesTopicMyNotes)
            {
                message = Strings.ClearHistoryMyNotesMessage;
                title = Strings.ClearHistoryMyNotesTitle;
                primary = Strings.Delete;
            }
            else
            {
                var chatTitle = ClientService.GetTitle(topic.Topic);

                message = string.Format(Strings.ClearHistoryMessageSingle, chatTitle);
                title = string.Format(Strings.ClearHistoryTitleSingle, chatTitle);
                primary = Strings.Remove;
            }

            var confirm = await ShowPopupAsync(message, title, primary, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                Items.Remove(topic);
                ClientService.Send(new DeleteSavedMessagesTopicHistory(topic.Topic));
            }
        }
    }
}
