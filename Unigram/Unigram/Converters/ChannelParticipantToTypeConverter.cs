using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class ChannelParticipantToTypeConverter
    {
        public static string Convert(IProtoService protoService, ChatMember member)
        {
            switch (member.Status)
            {
                case ChatMemberStatusAdministrator administrator:
                    return string.Format(Strings.Resources.EditAdminPromotedBy, protoService.GetUser(member.InviterUserId).GetFullName());
                case ChatMemberStatusRestricted restricted:
                    return string.Format(Strings.Resources.UserRestrictionsBy, protoService.GetUser(member.InviterUserId).GetFullName());
                case ChatMemberStatusBanned banned:
                    return string.Format(Strings.Resources.UserRestrictionsBy, protoService.GetUser(member.InviterUserId).GetFullName());
                default:
                    return LastSeenConverter.GetLabel(protoService.GetUser(member.UserId), false);
            }
        }
    }
}
