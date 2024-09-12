//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Controls.Messages
{
    public class ReactionAsPaidButton : ReactionButton
    {
        public ReactionAsPaidButton()
        {
            DefaultStyleKey = typeof(ReactionAsPaidButton);
        }

        protected override async void OnClick(MessageViewModel message, MessageReaction chosen)
        {
            var added = await PaidReactionService.AddPendingAsync(XamlRoot, message, 1, true, true);
            if (added is Ok)
            {
                Animate();
            }
        }
    }
}
