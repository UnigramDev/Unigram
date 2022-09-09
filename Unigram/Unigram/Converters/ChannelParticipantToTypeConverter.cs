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
                    return string.Format(Strings.Resources.EditAdminPromotedBy, clientService.GetUser(member.InviterUserId).GetFullName());
                case ChatMemberStatusRestricted:
                    return string.Format(Strings.Resources.UserRestrictionsBy, clientService.GetUser(member.InviterUserId).GetFullName());
                case ChatMemberStatusBanned:
                    return string.Format(Strings.Resources.UserRestrictionsBy, clientService.GetUser(member.InviterUserId).GetFullName());
                default:
                    return LastSeenConverter.GetLabel(clientService.GetMessageSender(member.MemberId) as User, false);
            }
        }
    }
}
