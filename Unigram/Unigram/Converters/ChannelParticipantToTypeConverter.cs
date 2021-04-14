using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.Converters
{
    public class ChannelParticipantToTypeConverter
    {
        public static string Convert(IProtoService protoService, ChatMember member)
        {
            switch (member.Status)
            {
                case ChatMemberStatusCreator:
                    return Strings.Resources.ChannelCreator;
                case ChatMemberStatusAdministrator:
                    return string.Format(Strings.Resources.EditAdminPromotedBy, protoService.GetUser(member.InviterUserId).GetFullName());
                case ChatMemberStatusRestricted:
                    return string.Format(Strings.Resources.UserRestrictionsBy, protoService.GetUser(member.InviterUserId).GetFullName());
                case ChatMemberStatusBanned:
                    return string.Format(Strings.Resources.UserRestrictionsBy, protoService.GetUser(member.InviterUserId).GetFullName());
                default:
                    return LastSeenConverter.GetLabel(protoService.GetMessageSender(member.MemberId) as User, false);
            }
        }
    }
}
