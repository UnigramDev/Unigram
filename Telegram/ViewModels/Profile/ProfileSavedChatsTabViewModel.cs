using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Profile
{
    public class ProfileSavedChatsTabViewModel : ViewModelBase, IHandle, IIncrementalCollectionOwner
    {
        private readonly HashSet<SavedMessagesTopic> _pinnedTopics = new();
        private string _nextOffset = string.Empty;

        public ProfileSavedChatsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<FoundSavedMessagesTopic>(this);
        }

        public IncrementalCollection<FoundSavedMessagesTopic> Items { get; private set; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var total = 0u;

            if (_nextOffset == string.Empty)
            {
                var response = await ClientService.SendAsync(new GetPinnedSavedMessagesTopics());
                if (response is FoundSavedMessagesTopics topics)
                {
                    foreach (var topic in topics.Topics)
                    {
                        _pinnedTopics.Add(topic.Topic);

                        Items.Add(topic);
                        total++;
                    }
                }
            }

            {
                var response = await ClientService.SendAsync(new GetSavedMessagesTopics(_nextOffset, 20));
                if (response is FoundSavedMessagesTopics topics)
                {
                    foreach (var topic in topics.Topics)
                    {
                        Items.Add(topic);
                        total++;
                    }

                    _nextOffset = topics.NextOffset;
                }
            }

            HasMoreItems = _nextOffset.Length > 0;

            return new LoadMoreItemsResult
            {
                Count = total
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        public bool IsPinned(FoundSavedMessagesTopic topic)
        {
            return _pinnedTopics.Contains(topic.Topic);
        }


        public void PinTopic(FoundSavedMessagesTopic topic)
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

        public void UnpinTopic(FoundSavedMessagesTopic topic)
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

        public async void DeleteTopic(FoundSavedMessagesTopic topic)
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
