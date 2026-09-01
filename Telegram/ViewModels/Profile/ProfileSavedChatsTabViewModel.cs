//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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

namespace Telegram.ViewModels.Profile
{
    public partial class ProfileSavedChatsTabViewModel : ViewModelBase, IHandle, IDelegable<ISavedMessagesChatsDelegate>
    {
        private readonly HashSet<long> _pinnedTopics = new();

        public ISavedMessagesChatsDelegate Delegate { get; set; }

        public ProfileSavedChatsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new SavedMessagesTopicsCollection(this);
            Items.TotalCount = clientService.SavedMessagesTopicCount;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateSavedMessagesTopicCount>(this, Handle);
        }

        private void Handle(UpdateSavedMessagesTopicCount update)
        {
            BeginOnUIThread(() => Items.TotalCount = update.TopicCount);
        }

        public SavedMessagesTopicsCollection Items { get; private set; }

        public bool IsPinned(SavedMessagesTopic topic)
        {
            return _pinnedTopics.Contains(topic.Id);
        }

        private void SetPinned(SavedMessagesTopic topic)
        {
            if (topic.IsPinned)
            {
                _pinnedTopics.Add(topic.Id);
            }
            else
            {
                _pinnedTopics.Remove(topic.Id);
            }
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

        public partial class SavedMessagesTopicsCollection : WindowedCollection<SavedMessagesTopic, long, long, OrderChangedEventArgs<SavedMessagesTopic>>
        {
            private readonly ProfileSavedChatsTabViewModel _viewModel;

            // The list this is a window over. Ordering comes from it rather than from the raw
            // updates, so the order a topic is placed with is the one the model decided under
            // its lock, not one read back from the topic afterwards.
            private readonly SavedMessagesTopicService _service;

            public SavedMessagesTopicsCollection(ProfileSavedChatsTabViewModel viewModel)
                : base(viewModel.BeginOnUIThread)
            {
                _viewModel = viewModel;

                _service = viewModel.ClientService.SavedMessagesTopics;
            }

            // Called by every page, and the first one is the one that takes. Nothing is
            // watched until then: with no page in, an update for a topic sorting far down the
            // list would become the whole window - which is the offset the next page asks for.
            private void Subscribe()
            {
                _service.Changed -= OnChanged;
                _service.Changed += OnChanged;
            }

            public override void Dispose()
            {
                base.Dispose();

                _service.Changed -= OnChanged;
            }

            private void OnChanged(OrderedSourceService<SavedMessagesTopic> sender, OrderChangedEventArgs<SavedMessagesTopic> args)
            {
                Enqueue(args);
            }

            protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
            {
                Logger.Info();

                var totalCount = 0u;

                var response = await _service.GetSavedMessagesTopicsAsync(Count, 20);
                if (response is Topics topics)
                {
                    foreach (var topic in _service.GetTopics(topics.TopicIds))
                    {
                        _viewModel.SetPinned(topic);

                        var order = topic.Order;
                        if (order == 0)
                        {
                            continue;
                        }

                        // An update can have inserted it already while the page was in
                        // flight, and can have moved it since: place it where it belongs.
                        var next = NextIndexOf(topic, topic.Id, order, out int prev);
                        if (next == prev)
                        {
                            continue;
                        }

                        if (prev >= 0)
                        {
                            RemoveAt(prev);
                        }
                        else
                        {
                            totalCount++;
                        }

                        SetOrder(topic.Id, order);
                        Insert(Math.Min(Count, next), topic);
                    }

                    // Before Subscribe, so the first update drains against a settled window.
                    UpdateWindow(topics.TotalCount >= 0);

                    Subscribe();

                    // The cache wrapper reuses Topics and answers -1 in TotalCount once it
                    // holds the whole list, so this is the has-more test, not a count.
                    return new IncrementalLoadResult(totalCount, topics.TotalCount >= 0);
                }

                return default;
            }

            protected override long GetKey(SavedMessagesTopic item)
            {
                return item.Id;
            }

            protected override SavedMessagesTopic GetItem(OrderChangedEventArgs<SavedMessagesTopic> args)
            {
                return args.Item;
            }

            protected override long GetOrder(OrderChangedEventArgs<SavedMessagesTopic> args)
            {
                return args.Order;
            }

            protected override bool IsPlaced(long order)
            {
                return order != 0;
            }

            protected override int Compare(long order, long topicId, long otherOrder, long otherTopicId)
            {
                if (order != otherOrder)
                {
                    return order > otherOrder ? 1 : -1;
                }

                return topicId.CompareTo(otherTopicId);
            }

            protected override void OnApplied(OrderChangedEventArgs<SavedMessagesTopic> args)
            {
                // Whether or not it moved a row, and whether or not it is in the window: the
                // pin limit counts every topic this list knows about.
                _viewModel.SetPinned(args.Item);
            }

            protected override void OnUnchanged(OrderChangedEventArgs<SavedMessagesTopic> args, int index)
            {
                _viewModel.Delegate?.UpdateSavedMessagesTopicLastMessage(args.Item);
            }
        }
    }
}
