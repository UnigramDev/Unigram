//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Stars.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Messages
{
    public partial class ReactionAsPaidButton : ReactionButton
    {
        public ReactionAsPaidButton()
        {
            DefaultStyleKey = typeof(ReactionAsPaidButton);
        }

        protected override async void OnClick(MessageViewModel message, MessageReaction chosen)
        {
            var added = await PaidReactionService.AddPendingAsync(XamlRoot, message, 1, null);
            if (added is Ok)
            {
                Animate();
            }
        }

        public override async void OnContextRequested(ContextRequestedEventArgs args)
        {
            var popup = new ReactPopup(_message.ClientService, _message);

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            _message.ClientService.Send(new SetPaidMessageReactionType(_message.ChatId, _message.Id, popup.Type));

            var added = await PaidReactionService.AddPendingAsync(XamlRoot, _message, popup.StarCount, popup.Type);
            if (added is Ok)
            {
                Animate();
            }
        }
    }
}
