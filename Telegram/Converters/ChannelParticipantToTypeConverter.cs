//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Converters
{
    public partial class ChannelParticipantToTypeConverter
    {
        public static string Convert(IClientService clientService, ChatMember member)
        {
            switch (member.Status)
            {
                case ChatMemberStatusCreator:
                    return Strings.ChannelCreator;
                case ChatMemberStatusAdministrator:
                    return string.Format(Strings.EditAdminPromotedBy, clientService.GetUser(member.InviterUserId).FullName());
                case ChatMemberStatusRestricted:
                    return string.Format(Strings.UserRestrictionsBy, clientService.GetUser(member.InviterUserId).FullName());
                case ChatMemberStatusBanned:
                    return string.Format(Strings.UserRestrictionsBy, clientService.GetUser(member.InviterUserId).FullName());
                default:
                    return LastSeenConverter.GetLabel(clientService.GetMessageSender(member.MemberId) as User, false);
            }
        }
    }
}
