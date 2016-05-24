using System;

namespace Telegram.Api.TL
{
    public class TLChatFull : TLObject
    {
        public const uint Signature = TLConstructors.TLChatFull;

        public TLInt Id { get; set; }

        public TLChatParticipantsBase Participants { get; set; }

        public TLPhotoBase ChatPhoto { get; set; }

        public TLPeerNotifySettingsBase NotifySettings { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Participants = GetObject<TLChatParticipantsBase>(bytes, ref position);
            ChatPhoto = GetObject<TLPhotoBase>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);

            return this;
        }

        public virtual TLChatBase ToChat(TLChatBase chat)
        {
            chat.NotifySettings = NotifySettings;
            chat.Participants = Participants;
            chat.ChatPhoto = ChatPhoto;

            return chat;
        }
    }

    public class TLChatFull28 : TLChatFull
    {
        public new const uint Signature = TLConstructors.TLChatFull28;

        public TLExportedChatInvite ExportedInvite { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Participants = GetObject<TLChatParticipantsBase>(bytes, ref position);
            ChatPhoto = GetObject<TLPhotoBase>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);
            ExportedInvite = GetObject<TLExportedChatInvite>(bytes, ref position);

            return this;
        }
    }

    public class TLChatFull31 : TLChatFull28
    {
        public new const uint Signature = TLConstructors.TLChatFull31;

        public TLVector<TLBotInfoBase> BotInfo { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Participants = GetObject<TLChatParticipantsBase>(bytes, ref position);
            ChatPhoto = GetObject<TLPhotoBase>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);
            ExportedInvite = GetObject<TLExportedChatInvite>(bytes, ref position);
            BotInfo = GetObject<TLVector<TLBotInfoBase>>(bytes, ref position);

            return this;
        }
    }

    [Flags]
    public enum ChannelFullFlags
    {
        Participants = 0x1,
        Admins = 0x2,
        Kicked = 0x4,
        CanViewParticipants = 0x8,
        Migrated = 0x10
    }

    public class TLChannelFull : TLChatFull
    {
        public new const uint Signature = TLConstructors.TLChannelFull;

        public TLInt Flags { get; set; }

        public TLString About { get; set; }

        public TLInt ParticipantsCount { get; set; }

        public TLInt AdminsCount { get; set; }

        public TLInt KickedCount { get; set; }

        public TLInt ReadInboxMaxId { get; set; }

        public TLInt UnreadCount { get; set; }

        public TLInt UnreadImportantCount { get; set; }

        public TLExportedChatInvite ExportedInvite { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            //Participants = GetObject<TLChatParticipantsBase>(bytes, ref position);
            About = GetObject<TLString>(bytes, ref position);
            if (IsSet(Flags, (int)ChannelFullFlags.Participants))
            {
                ParticipantsCount = GetObject<TLInt>(bytes, ref position);
            } 
            if (IsSet(Flags, (int) ChannelFullFlags.Admins))
            {
                AdminsCount = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)ChannelFullFlags.Kicked))
            {
                KickedCount = GetObject<TLInt>(bytes, ref position);
            }
            ReadInboxMaxId = GetObject<TLInt>(bytes, ref position);
            UnreadCount = GetObject<TLInt>(bytes, ref position);
            UnreadImportantCount = GetObject<TLInt>(bytes, ref position);        
            ChatPhoto = GetObject<TLPhotoBase>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);
            ExportedInvite = GetObject<TLExportedChatInvite>(bytes, ref position);
            
            return this;
        }

        public override TLChatBase ToChat(TLChatBase chat)
        {
            chat.NotifySettings = NotifySettings;
            chat.Participants = Participants;
            chat.ChatPhoto = ChatPhoto;

            var channel = chat as TLChannel;
            if (channel != null)
            {
                channel.ExportedInvite = ExportedInvite;
                channel.About = About;
                channel.ParticipantsCount = ParticipantsCount;
                channel.AdminsCount = AdminsCount;
                channel.KickedCount = KickedCount;
                channel.ReadInboxMaxId = ReadInboxMaxId;
            }

            return chat;
        }
    }

    public class TLChannelFull41 : TLChannelFull
    {
        public new const uint Signature = TLConstructors.TLChannelFull41;

        public TLVector<TLBotInfoBase> BotInfo { get; set; }

        public TLInt MigratedFromChatId { get; set; }

        public TLInt MigratedFromMaxId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            //Participants = GetObject<TLChatParticipantsBase>(bytes, ref position);
            About = GetObject<TLString>(bytes, ref position);
            if (IsSet(Flags, (int)ChannelFullFlags.Participants))
            {
                ParticipantsCount = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)ChannelFullFlags.Admins))
            {
                AdminsCount = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)ChannelFullFlags.Kicked))
            {
                KickedCount = GetObject<TLInt>(bytes, ref position);
            }
            ReadInboxMaxId = GetObject<TLInt>(bytes, ref position);
            UnreadCount = GetObject<TLInt>(bytes, ref position);
            UnreadImportantCount = GetObject<TLInt>(bytes, ref position);
            ChatPhoto = GetObject<TLPhotoBase>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);
            ExportedInvite = GetObject<TLExportedChatInvite>(bytes, ref position);
            BotInfo = GetObject<TLVector<TLBotInfoBase>>(bytes, ref position);
            if (IsSet(Flags, (int)ChannelFullFlags.Migrated))
            {
                MigratedFromChatId = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)ChannelFullFlags.Migrated))
            {
                MigratedFromMaxId = GetObject<TLInt>(bytes, ref position);
            }

            return this;
        }

        public override TLChatBase ToChat(TLChatBase chat)
        {
            chat.NotifySettings = NotifySettings;
            chat.Participants = Participants;
            chat.ChatPhoto = ChatPhoto;

            var channel = chat as TLChannel;
            if (channel != null)
            {
                channel.ExportedInvite = ExportedInvite;
                channel.About = About;
                channel.ParticipantsCount = ParticipantsCount;
                channel.AdminsCount = AdminsCount;
                channel.KickedCount = KickedCount;
                channel.ReadInboxMaxId = ReadInboxMaxId;
            }

            return chat;
        }
    }
}
