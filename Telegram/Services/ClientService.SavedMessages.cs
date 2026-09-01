//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial interface ICacheService
    {
        int SavedMessagesTopicCount { get; }

        SavedMessagesTopicService SavedMessagesTopics { get; }

        bool TryGetSavedMessagesTopic(long savedMessagesTopicId, out SavedMessagesTopic topic);

        IEnumerable<SavedMessagesTopic> GetSavedMessagesTopics(IEnumerable<long> ids);
        SavedMessagesTopic GetSavedMessagesTopic(long savedMessagesTopicId);

        string GetTitle(SavedMessagesTopic topic);
    }

    public partial class ClientService
    {
        private readonly SavedMessagesTopicService _savedMessages;

        public SavedMessagesTopicService SavedMessagesTopics => _savedMessages;

        public int SavedMessagesTopicCount { get; private set; }

        public bool TryGetSavedMessagesTopic(long savedMessagesTopicId, out SavedMessagesTopic topic)
        {
            return _savedMessages.TryGetTopic(savedMessagesTopicId, out topic);
        }

        public IEnumerable<SavedMessagesTopic> GetSavedMessagesTopics(IEnumerable<long> ids)
        {
            return _savedMessages.GetTopics(ids);
        }

        public SavedMessagesTopic GetSavedMessagesTopic(long savedMessagesTopicId)
        {
            return _savedMessages.GetTopic(savedMessagesTopicId);
        }

        public string GetTitle(SavedMessagesTopic topic)
        {
            if (topic?.Type is SavedMessagesTopicTypeMyNotes)
            {
                return Strings.MyNotes;
            }
            else if (topic?.Type is SavedMessagesTopicTypeAuthorHidden)
            {
                return Strings.AnonymousForward;
            }
            else if (topic?.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && TryGetChat(savedFromChat.ChatId, out Chat chat))
            {
                return GetTitle(chat);
            }

            return Strings.AnonymousForward;
        }
    }
}
