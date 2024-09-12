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
    public partial class ProfileSavedChatsTabViewModel : ViewModelBase, IHandle, IIncrementalCollectionOwner, IDelegable<ISavedMessagesChatsDelegate>
    {
        private readonly HashSet<long> _pinnedTopics = new();
        private readonly HashSet<long> _topics = new();

        private readonly DisposableMutex _loadMoreLock = new();
        private long _lastTopicId;
        private long _lastOrder;

        public ISavedMessagesChatsDelegate Delegate { get; set; }

        public ProfileSavedChatsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<SavedMessagesTopic>(this);
        }

        public override void Subscribe()
        {
        }

        private void Handle(UpdateSavedMessagesTopic update)
        {
            BeginOnUIThread(() => UpdateChatOrder(update.Topic, update.Topic.Order, true));
        }

        public IncrementalCollection<SavedMessagesTopic> Items { get; private set; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            using var guard = await _loadMoreLock.WaitAsync();

            var totalCount = 0u;

            var response = await ClientService.GetSavedMessagesChatsAsync(Items.Count, 20);
            if (response is IList<SavedMessagesTopic> topics)
            {
                foreach (var topic in topics)
                {
                    if (topic.IsPinned)
                    {
                        _pinnedTopics.Add(topic.Id);
                    }
                    else
                    {
                        _pinnedTopics.Remove(topic.Id);
                    }

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
                Aggregator.Subscribe<UpdateSavedMessagesTopic>(this, Handle);
            }

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        private void UpdateChatOrder(SavedMessagesTopic topic, long order, bool lastMessage)
        {
            if (order > 0 && (order > _lastOrder || (order == _lastOrder && topic.Id >= _lastTopicId)))
            {
                if (topic.IsPinned)
                {
                    _pinnedTopics.Add(topic.Id);
                }
                else
                {
                    _pinnedTopics.Remove(topic.Id);
                }

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

        private int NextIndexOf(SavedMessagesTopic topic, long order)
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

        public bool IsPinned(SavedMessagesTopic topic)
        {
            return _pinnedTopics.Contains(topic.Id);
        }


        public void PinTopic(SavedMessagesTopic topic)
        {
            if (_pinnedTopics.Count < ClientService.Options.PinnedSavedMessagesTopicCountMax)
            {
                ClientService.Send(new ToggleSavedMessagesTopicIsPinned(topic.Id, true));
            }
            else
            {
                NavigationService.ShowLimitReached(new PremiumLimitTypePinnedSavedMessagesTopicCount());
            }
        }

        public void UnpinTopic(SavedMessagesTopic topic)
        {
            ClientService.Send(new ToggleSavedMessagesTopicIsPinned(topic.Id, false));
        }

        public async void DeleteTopic(SavedMessagesTopic topic)
        {
            string message;
            string title;
            string primary;

            if (topic.Type is SavedMessagesTopicTypeMyNotes)
            {
                message = Strings.ClearHistoryMyNotesMessage;
                title = Strings.ClearHistoryMyNotesTitle;
                primary = Strings.Delete;
            }
            else
            {
                var chatTitle = ClientService.GetTitle(topic);

                message = string.Format(Strings.ClearHistoryMessageSingle, chatTitle);
                title = string.Format(Strings.ClearHistoryTitleSingle, chatTitle);
                primary = Strings.Remove;
            }

            var confirm = await ShowPopupAsync(message, title, primary, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                Items.Remove(topic);
                ClientService.Send(new DeleteSavedMessagesTopicHistory(topic.Id));
            }
        }
    }
}
