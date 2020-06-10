using System.Text;
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
                case ChatMemberStatusCreator creator:
                    return Strings.Resources.ChannelCreator;
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

        private static string formatUserPermissions(ChatPermissions rights, ChatPermissions defaultBannedRights)
        {
            if (rights == null)
            {
                return "";
            }
            StringBuilder builder = new StringBuilder();
            //if (rights.view_messages && defaultBannedRights.view_messages != rights.view_messages)
            //{
            //    builder.append(Strings.Resources.UserRestrictionsNoRead);
            //}
            if (rights.CanSendMessages && defaultBannedRights.CanSendMessages != rights.CanSendMessages)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Strings.Resources.UserRestrictionsNoSend);
            }
            if (rights.CanSendMediaMessages && defaultBannedRights.CanSendMediaMessages != rights.CanSendMediaMessages)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Strings.Resources.UserRestrictionsNoSendMedia);
            }
            if (rights.CanSendOtherMessages && defaultBannedRights.CanSendOtherMessages != rights.CanSendOtherMessages)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Strings.Resources.UserRestrictionsNoSendStickers);
            }
            if (rights.CanSendPolls && defaultBannedRights.CanSendPolls != rights.CanSendPolls)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Strings.Resources.UserRestrictionsNoSendPolls);
            }
            if (rights.CanAddWebPagePreviews && defaultBannedRights.CanAddWebPagePreviews != rights.CanAddWebPagePreviews)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Strings.Resources.UserRestrictionsNoEmbedLinks);
            }
            if (rights.CanInviteUsers && defaultBannedRights.CanInviteUsers != rights.CanInviteUsers)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Strings.Resources.UserRestrictionsNoInviteUsers);
            }
            if (rights.CanPinMessages && defaultBannedRights.CanPinMessages != rights.CanPinMessages)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Strings.Resources.UserRestrictionsNoPinMessages);
            }
            if (rights.CanChangeInfo && defaultBannedRights.CanChangeInfo != rights.CanChangeInfo)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Strings.Resources.UserRestrictionsNoChangeInfo);
            }
            if (builder.Length > 0)
            {
                //builder.Replace(0, 1, builder.ToString().Substring(0, 1).ToUpper());
                //builder.Append('.');
            }

            return builder.ToString();
        }
    }
}
