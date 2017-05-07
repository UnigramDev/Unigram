using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLChatFullBase
    {
        public virtual void Update(TLChatFullBase chatFull)
        {
            // TODO: 06/05/2017
        }

        //public TLChatBase ToChat(TLChatBase chat)
        //{
        //    //chat.NotifySettings = NotifySettings;
        //    //chat.Participants = Participants;
        //    //chat.ChatPhoto = ChatPhoto;

        //    //var channel = chat as TLChannel;
        //    //if (channel != null)
        //    //{
        //    //    channel.ExportedInvite = ExportedInvite;
        //    //    channel.About = About;
        //    //    channel.ParticipantsCount = ParticipantsCount;
        //    //    channel.AdminsCount = AdminsCount;
        //    //    channel.KickedCount = KickedCount;
        //    //    channel.ReadInboxMaxId = ReadInboxMaxId;
        //    //}

        //    return chat;
        //}
    }
}
