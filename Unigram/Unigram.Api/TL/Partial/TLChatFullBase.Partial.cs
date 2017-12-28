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
            this.BotInfo = chatFull.BotInfo;
            this.ChatPhoto = chatFull.ChatPhoto;
            this.ExportedInvite = chatFull.ExportedInvite;
            this.Id = chatFull.Id;
            this.NotifySettings = chatFull.NotifySettings;
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

        public virtual TLChatBase ToChat(TLChatBase chat)
        {
            //chat.NotifySettings = NotifySettings;
            //chat.Participants = Participants;
            //chat.ChatPhoto = ChatPhoto;

            //var channel = chat as TLChannel;
            //if (channel != null)
            //{
            //    channel.ExportedInvite = ExportedInvite;
            //    channel.About = About;
            //    channel.ParticipantsCount = ParticipantsCount;
            //    channel.AdminsCount = AdminsCount;
            //    channel.KickedCount = KickedCount;
            //    channel.ReadInboxMaxId = ReadInboxMaxId;
            //}

            return chat;
        }
    }

    public partial class TLChatFull
    {
        public override void Update(TLChatFullBase chatFull)
        {
            if (chatFull is TLChatFull chat)
            {
                this.Participants = chat.Participants;
            }

            base.Update(chatFull);
        }
    }

    public partial class TLChannelFull
    {
        public override void Update(TLChatFullBase chatFull)
        {
            if (chatFull is TLChannelFull channel)
            {
                this.Flags = channel.Flags;
                this.About = channel.About;
                this.ParticipantsCount = channel.ParticipantsCount;
                this.AdminsCount = channel.AdminsCount;
                this.KickedCount = channel.KickedCount;
                this.BannedCount = channel.BannedCount;
                this.ReadInboxMaxId = channel.ReadInboxMaxId;
                this.ReadOutboxMaxId = channel.ReadOutboxMaxId;
                this.UnreadCount = channel.UnreadCount;
                this.MigratedFromChatId = channel.MigratedFromChatId;
                this.MigratedFromMaxId = channel.MigratedFromMaxId;
                this.PinnedMsgId = channel.PinnedMsgId;
                this.AvailableMinId = channel.AvailableMinId;
            }

            base.Update(chatFull);
        }

        public override TLChatBase ToChat(TLChatBase chat)
        {
            if (chat is TLChannel channel)
            {
                channel.ReadInboxMaxId = this.ReadInboxMaxId;
                channel.ReadOutboxMaxId = this.ReadOutboxMaxId;
            }

            return base.ToChat(chat);
        }
    }
}
