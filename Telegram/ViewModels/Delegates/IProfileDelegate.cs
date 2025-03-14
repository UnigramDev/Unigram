//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;

namespace Telegram.ViewModels.Delegates
{
    public interface IProfileDelegate : IChatDelegate, IUserDelegate, ISupergroupDelegate, IBasicGroupDelegate
    {
        void UpdateSecretChat(Chat chat, SecretChat secretChat);

        void UpdateChatNotificationSettings(Chat chat);

        void UpdateChatLastMessage(Chat chat);

        void UpdateChatEmojiStatus(Chat chat);
        void UpdateChatAccentColors(Chat chat);
        void UpdateChatActiveStories(Chat chat);
    }
}
