//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Td.Api;

namespace Telegram.ViewModels.Delegates
{
    public interface IDirectMessagesTopicDelegate
    {
        void UpdateDirectMessagesChatTopic(TopicListViewModel viewModel, DirectMessagesChatTopic topic);
        void UpdateDirectMessagesChatTopicLastMessage(DirectMessagesChatTopic topic);
        void UpdateDirectMessagesChatTopicReadInbox(DirectMessagesChatTopic topic);
        void UpdateDirectMessagesChatTopicReadOutbox(DirectMessagesChatTopic topic);
        void UpdateDirectMessagesChatTopicUnreadMentionCount(DirectMessagesChatTopic topic);
    }
}
