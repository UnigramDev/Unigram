//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Controls.Messages
{
    public interface IContent
    {
        MessageViewModel Message { get; }

        void UpdateMessage(MessageViewModel message);

        void Recycle();

        bool IsValid(MessageContent content, bool primary);
    }

    public interface IContentWithFile : IContent
    {
        void UpdateMessageContentOpened(MessageViewModel message);
    }

    public interface IContentWithMask
    {
        CompositionBrush GetAlphaMask();
    }

    public interface IContentWithPlayback
    {
        IPlayerView GetPlaybackElement();
    }
}
