using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLChatBase
    {
        #region Full chat information

        public TLChatParticipantsBase Participants { get; set; }

        public TLPhotoBase ChatPhoto { get; set; }

        public TLPeerNotifySettingsBase NotifySettings { get; set; }

        public int UsersOnline { get; set; }

        public TLExportedChatInviteBase ExportedInvite { get; set; }

        public TLVector<TLBotInfo> BotInfo { get; set; }

        #endregion

        public virtual void Update(TLChatBase chat)
        {
            Id = chat.Id;

            if (chat.Participants != null)
            {
                Participants = chat.Participants;
            }

            if (chat.ChatPhoto != null)
            {
                ChatPhoto = chat.ChatPhoto;
            }

            if (chat.NotifySettings != null)
            {
                NotifySettings = chat.NotifySettings;
            }
        }

        #region Add
        public virtual string FullName
        {
            get
            {
                return null;
            }
        }
        #endregion
    }

    public partial class TLChat
    {
        public override string FullName
        {
            get
            {
                return Title;
            }
        }
    }

    public partial class TLChatForbidden
    {
        public override string FullName
        {
            get
            {
                return Title;
            }
        }
    }

    public partial class TLChannel
    {
        public override string FullName
        {
            get
            {
                return Title;
            }
        }
    }

    public partial class TLChannelForbidden
    {
        public override string FullName
        {
            get
            {
                return Title;
            }
        }
    }
}
