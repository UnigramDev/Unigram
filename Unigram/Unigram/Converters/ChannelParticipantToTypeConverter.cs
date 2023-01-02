//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.Converters
{
    public class ChannelParticipantToTypeConverter
    {
        public static string Convert(IClientService clientService, ChatMember member)
        {
            switch (member.Status)
            {
                case ChatMemberStatusCreator:
                    return Strings.Resources.ChannelCreator;
                case ChatMemberStatusAdministrator:
                    return string.Format(Strings.Resources.EditAdminPromotedBy, clientService.GetUser(member.InviterUserId).FullName());
                case ChatMemberStatusRestricted:
                    return string.Format(Strings.Resources.UserRestrictionsBy, clientService.GetUser(member.InviterUserId).FullName());
                case ChatMemberStatusBanned:
                    return string.Format(Strings.Resources.UserRestrictionsBy, clientService.GetUser(member.InviterUserId).FullName());
                default:
                    return LastSeenConverter.GetLabel(clientService.GetMessageSender(member.MemberId) as User, false);
            }
        }
    }
}
