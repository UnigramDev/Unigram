//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Delegates
{
    public interface ITopicListDelegate : IViewModelDelegate
    {
        void SetSelectedItem(object topic);
        void SetSelectedItems(IList<object> topics);

        void UpdateForumTopicLastMessage(ForumTopic topic);
        void UpdateDirectMessagesChatTopicLastMessage(DirectMessagesChatTopic topic);

        void HandleForumTopic(int forumTopicId, Action<IForumTopicDelegate, ForumTopic> action);
        void HandleForumTopic(ForumTopic topic, Action<IForumTopicDelegate, ForumTopic> action);

        void HandleDirectMessagesChatTopic(long topicId, Action<IDirectMessagesTopicDelegate, DirectMessagesChatTopic> action);
        void HandleDirectMessagesChatTopic(DirectMessagesChatTopic topic, Action<IDirectMessagesTopicDelegate, DirectMessagesChatTopic> action);
    }
}
