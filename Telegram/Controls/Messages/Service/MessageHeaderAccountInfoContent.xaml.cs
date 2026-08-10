//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class MessageHeaderAccountInfoContent : MessageService
    {
        public MessageHeaderAccountInfoContent()
        {
            InitializeComponent();
        }

        protected override void UpdateContent(MessageViewModel message)
        {
            if (message.Chat.ActionBar is ChatActionBarReportAddBlock reportAddBlock && reportAddBlock.AccountInfo != null)
            {
                if (message.ClientService.TryGetUser(message.Chat, out User user) && message.ClientService.TryGetUserFull(user.Id, out UserFullInfo fullInfo))
                {
                    AccountInfo.Update(message.ClientService, user, fullInfo, reportAddBlock.AccountInfo);
                }
            }
        }
    }
}
