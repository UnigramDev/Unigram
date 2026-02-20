//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Delegates
{
    public interface IForumTopicDelegate
    {
        void UpdateForumTopicActions(ForumTopic topic, IDictionary<MessageSender, ChatAction> actions);

        void UpdateForumTopic(TopicListViewModel viewModel, ForumTopic topic);
        void UpdateForumTopicInfo(ForumTopic topic);
        void UpdateForumTopicLastMessage(ForumTopic topic);
        void UpdateForumTopicReadInbox(ForumTopic topic);
        void UpdateForumTopicReadOutbox(ForumTopic topic);
        void UpdateForumTopicUnreadMentionCount(ForumTopic topic);
        void UpdateForumTopicNotificationSettings(ForumTopic topic);

        void ShowPreview(Point? position);
    }
}
