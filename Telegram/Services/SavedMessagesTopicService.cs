//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Td.Api;

namespace Telegram.Services
{
    /// <summary>
    /// Every saved messages topic, in order.
    /// </summary>
    /// <remarks>
    /// Unlike the forum and direct messages lists there is one of these per session, since saved
    /// messages are a single list rather than one per chat.
    /// </remarks>
    public partial class SavedMessagesTopicService : OrderedSourceService<SavedMessagesTopic>
    {
        private readonly IClientService _clientService;

        private readonly ReaderWriterDictionary<long, SavedMessagesTopic> _topics = new(100);

        public SavedMessagesTopicService(IClientService clientService)
        {
            _clientService = clientService;
        }

        /// <summary>
        /// Topics held, whatever any view has paged in.
        /// </summary>
        public int TopicCount => _topics.Count;

        /// <summary>
        /// Merges the update into the topic already held, and answers the one to go on using -
        /// which is that topic, not the one the update arrived with.
        /// </summary>
        public SavedMessagesTopic UpdateSavedMessagesTopic(SavedMessagesTopic newTopic)
        {
            if (_topics.TryGetValue(newTopic.Id, out SavedMessagesTopic topic))
            {
                topic.DraftMessage = newTopic.DraftMessage;
                topic.LastMessage = newTopic.LastMessage;
                topic.IsPinned = newTopic.IsPinned;

                UpdateTopicOrder(topic, newTopic.Order);
                return topic;
            }

            _topics[newTopic.Id] = newTopic;

            UpdateTopicOrder(newTopic, newTopic.Order);
            return newTopic;
        }

        private void UpdateTopicOrder(SavedMessagesTopic topic, long order)
        {
            lock (SyncRoot)
            {
                topic.Order = order;
                SetOrder(topic.Id, order);
            }

            RaiseChanged(topic, order, true);
        }

        public bool TryGetTopic(long savedMessagesTopicId, out SavedMessagesTopic topic)
        {
            return _topics.TryGetValue(savedMessagesTopicId, out topic);
        }

        public SavedMessagesTopic GetTopic(long savedMessagesTopicId)
        {
            if (_topics.TryGetValue(savedMessagesTopicId, out SavedMessagesTopic value))
            {
                return value;
            }

            return null;
        }

        public IEnumerable<SavedMessagesTopic> GetTopics(IEnumerable<long> ids)
        {
            foreach (var id in ids)
            {
                var topic = GetTopic(id);
                if (topic != null)
                {
                    yield return topic;
                }
            }
        }

        public async Task<Topics> GetSavedMessagesTopicsAsync(int offset, int limit)
        {
            var page = await GetItemsAsync(offset, limit);
            return new Topics(page.HaveFullList ? -1 : 0, page.Ids);
        }

        protected override Task<Object> LoadMoreItemsAsync(int count)
        {
            return _clientService.SendAsync(new LoadSavedMessagesTopics(count));
        }

        public override void Clear()
        {
            _topics.Clear();

            base.Clear();
        }
    }
}
