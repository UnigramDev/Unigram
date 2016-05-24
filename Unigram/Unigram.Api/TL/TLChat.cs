using System;
using System.Collections.Generic;
using System.IO;
using Telegram.Api.Extensions;
using Telegram.Api.TL.Interfaces;

namespace Telegram.Api.TL
{
    [Flags]
    public enum ChatFlags
    {
        Creator = 0x1,
        Kicked = 0x2,
        Left = 0x4,
        AdminsEnabled = 0x8,
        Admin = 0x10,
        Deactivated = 0x20,
        MigratedTo = 0x40
    }

    [Flags]
    public enum ChannelFlags
    {
        Creator = 0x1,
        Kicked = 0x2,
        Left = 0x4,
        Editor = 0x8,
        Moderator = 0x10,
        Broadcast = 0x20,
        Public = 0x40,
        Verified = 0x80,
        MegaGroup = 0x100,
    }


    [Flags]
    public enum ChannelCustomFlags
    {
        MigratedFromChatId = 0x1,
        MigratedFromMaxId = 0x2,
    }

    public abstract class TLChatBase : TLObject, IInputPeer, IFullName, INotifySettings
    {
        public int Index
        {
            get { return Id.Value; }
            set { Id = new TLInt(value); }
        }

        public TLInt Id { get; set; }

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

        public abstract TLInputPeerBase ToInputPeer();

        public abstract string GetUnsendedTextFileName();

        #region Full chat information
        
        public TLChatParticipantsBase Participants { get; set; }

        public TLPhotoBase ChatPhoto { get; set; }

        public TLPeerNotifySettingsBase NotifySettings { get; set; }

        public int UsersOnline { get; set; }

        public TLExportedChatInvite ExportedInvite { get; set; }
        #endregion

        public TLInputNotifyPeerBase ToInputNotifyPeer()
        {
            return new TLInputNotifyPeer { Peer = ToInputPeer() };
        }

        public abstract string FullName { get; }

        public abstract bool IsForbidden { get; }

        #region Additional
        public IList<string> FullNameWords { get; set; }
        public TLVector<TLBotInfoBase> BotInfo { get; set; }

        #endregion
    }

    public class TLChatEmpty : TLChatBase
    {
        public const uint Signature = TLConstructors.TLChatEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            
            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
        }


        public override string FullName
        {
            get { return string.Empty; }
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerChat { ChatId = Id };
        }

        public override string GetUnsendedTextFileName()
        {
            return "c" + Id + ".dat";
        }

        public override bool IsForbidden
        {
            get { return true; }
        }
    }

    public class TLChat : TLChatBase
    {
        public const uint Signature = TLConstructors.TLChat;

        protected TLString _title;

        public TLString Title
        {
            get { return _title; }
            set
            {
                SetField(ref _title, value, () => Title);
                NotifyOfPropertyChange(() => FullName);
            }
        }

        protected TLPhotoBase _photo;

        public TLPhotoBase Photo
        {
            get { return _photo; }
            set { SetField(ref _photo, value, () => Photo); }
        }

        public TLInt ParticipantsCount { get; set; }

        public TLInt Date { get; set; }

        public virtual TLBool Left { get; set; }

        public TLInt Version { get; set; }

        public override string ToString()
        {
            return Title.ToString();
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _title = GetObject<TLString>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            ParticipantsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Left = GetObject<TLBool>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);
            
            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _title = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            ParticipantsCount = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Left = GetObject<TLBool>(input);
            Version = GetObject<TLInt>(input);

            Participants = GetObject<TLObject>(input) as TLChatParticipantsBase;
            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(Title.ToBytes());
            Photo.ToStream(output);
            output.Write(ParticipantsCount.ToBytes());
            output.Write(Date.ToBytes());
            output.Write(Left.ToBytes());
            output.Write(Version.ToBytes());

            Participants.NullableToStream(output);
            NotifySettings.NullableToStream(output);
        }

        public override string FullName
        {
            get { return Title != null ? Title.ToString() : string.Empty; }
        }

        public override void Update(TLChatBase chat)
        {
            base.Update(chat);
            var c = chat as TLChat;
            if (c != null)
            {
                _title = c.Title;
                if (Photo.GetType() != c.Photo.GetType())
                {
                    _photo = c.Photo;    // при удалении фото чата не обновляется UI при _photo = c.Photo
                }
                else
                {
                    Photo.Update(c.Photo);
                }
                ParticipantsCount = c.ParticipantsCount;
                Date = c.Date;
                Left = c.Left;
                Version = c.Version;
            }
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerChat { ChatId = Id };
        }

        public override string GetUnsendedTextFileName()
        {
            return "c" + Id + ".dat";
        }

        public override bool IsForbidden
        {
            get { return Left.Value; }
        }
    }

    public class TLChat40 : TLChat
    {
        public new const uint Signature = TLConstructors.TLChat40;

        protected TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public override TLBool Left
        {
            get { return new TLBool(IsSet(_flags, (int)ChatFlags.Left)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)ChatFlags.Left);
                }
            }
        }

        public bool Creator
        {
            get { return IsSet(_flags, (int)ChatFlags.Creator); }
        }

        public TLBool Admin
        {
            get { return new TLBool(IsSet(_flags, (int)ChatFlags.Admin)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)ChatFlags.Admin);
                }
            }
        }

        public TLBool AdminsEnabled
        {
            get { return new TLBool(IsSet(_flags, (int)ChatFlags.AdminsEnabled)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)ChatFlags.AdminsEnabled);
                }
            }
        }

        public bool Deactivated
        {
            get { return IsSet(_flags, (int)ChatFlags.Deactivated); }
        }

        public TLLong CustomFlags { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            _title = GetObject<TLString>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            ParticipantsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            //Left = GetObject<TLBool>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _title = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            ParticipantsCount = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            //Left = GetObject<TLBool>(input);
            Version = GetObject<TLInt>(input);

            Participants = GetObject<TLObject>(input) as TLChatParticipantsBase;
            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;

            CustomFlags = GetNullableObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(Title.ToBytes());
            Photo.ToStream(output);
            output.Write(ParticipantsCount.ToBytes());
            output.Write(Date.ToBytes());
            //output.Write(Left.ToBytes());
            output.Write(Version.ToBytes());

            Participants.NullableToStream(output);
            NotifySettings.NullableToStream(output);

            CustomFlags.NullableToStream(output);
        }

        public override void Update(TLChatBase chat)
        {
            base.Update(chat);
            var c = chat as TLChat40;
            if (c != null)
            {
                Flags = c.Flags;
                if (c.CustomFlags != null)
                {
                    CustomFlags = c.CustomFlags;
                }
            }
        }
    }

    public class TLChat41 : TLChat40
    {
        public new const uint Signature = TLConstructors.TLChat41;

        public TLInputChannelBase MigratedTo { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            _title = GetObject<TLString>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            ParticipantsCount = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            //Left = GetObject<TLBool>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);
            if (IsSet(Flags, (int) ChatFlags.MigratedTo))
            {
                MigratedTo = GetObject<TLInputChannelBase>(bytes, ref position);
            }

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Id = GetObject<TLInt>(input);
            _title = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            ParticipantsCount = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            //Left = GetObject<TLBool>(input);
            Version = GetObject<TLInt>(input);
            if (IsSet(Flags, (int)ChatFlags.MigratedTo))
            {
                MigratedTo = GetObject<TLInputChannelBase>(input);
            }

            Participants = GetObject<TLObject>(input) as TLChatParticipantsBase;
            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;

            CustomFlags = GetNullableObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Flags.ToBytes());
            output.Write(Id.ToBytes());
            output.Write(Title.ToBytes());
            Photo.ToStream(output);
            output.Write(ParticipantsCount.ToBytes());
            output.Write(Date.ToBytes());
            //output.Write(Left.ToBytes());
            output.Write(Version.ToBytes());
            if (IsSet(Flags, (int)ChatFlags.MigratedTo))
            {
                MigratedTo.ToStream(output);
            }

            Participants.NullableToStream(output);
            NotifySettings.NullableToStream(output);

            CustomFlags.NullableToStream(output);
        }

        public override void Update(TLChatBase chat)
        {
            base.Update(chat);
            var c = chat as TLChat41;
            if (c != null)
            {
                if (c.MigratedTo != null)
                {
                    MigratedTo = c.MigratedTo;
                }
            }
        }
    }

    public class TLChatForbidden : TLChatBase
    {
        public const uint Signature = TLConstructors.TLChatForbidden;

        public TLString Title { get; set; }

        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Title = GetObject<TLString>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            
            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            Title = GetObject<TLString>(input);
            Date = GetObject<TLInt>(input);

            Participants = GetObject<TLObject>(input) as TLChatParticipantsBase;
            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(Title.ToBytes());
            output.Write(Date.ToBytes());

            Participants.NullableToStream(output);
            NotifySettings.NullableToStream(output);
        }

        public override void Update(TLChatBase chat)
        {
            base.Update(chat);
            var c = (TLChatForbidden)chat;
            Title = c.Title;
            Date = c.Date;
        }

        public override string FullName
        {
            get
            {
                return Title != null ? Title.ToString() : string.Empty;
            }
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerChat { ChatId = Id };
        }

        public override string GetUnsendedTextFileName()
        {
            return "c" + Id + ".dat";
        }

        public override bool IsForbidden
        {
            get { return true; }
        }
    }

    public class TLChatForbidden40 : TLChatForbidden
    {
        public new const uint Signature = TLConstructors.TLChatForbidden40;

        public TLLong CustomFlags { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Title = GetObject<TLString>(bytes, ref position);
            //Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            Title = GetObject<TLString>(input);
            //Date = GetObject<TLInt>(input);

            Participants = GetObject<TLObject>(input) as TLChatParticipantsBase;
            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;

            CustomFlags = GetNullableObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(Title.ToBytes());
            //output.Write(Date.ToBytes());

            Participants.NullableToStream(output);
            NotifySettings.NullableToStream(output);

            CustomFlags.NullableToStream(output);
        }

        public override void Update(TLChatBase chat)
        {
            base.Update(chat);
            var c = chat as TLChatForbidden40;
            if (c != null)
            {
                if (c.CustomFlags != null)
                {
                    CustomFlags = c.CustomFlags;
                }
            }
        }
    }

    public class TLBroadcastChat : TLChatBase
    {
        public const uint Signature = TLConstructors.TLBroadcastChat;

        public TLVector<TLInt> ParticipantIds { get; set; }

        protected TLString _title;

        public TLString Title
        {
            get { return _title; }
            set
            {
                SetField(ref _title, value, () => Title);
                NotifyOfPropertyChange(() => FullName);
            }
        }

        protected TLPhotoBase _photo;

        public TLPhotoBase Photo
        {
            get { return _photo; }
            set { SetField(ref _photo, value, () => Photo); }
        }

        public override string FullName
        {
            get { return Title != null ? Title.ToString() : string.Empty; }
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _title = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            ParticipantIds = GetObject<TLVector<TLInt>>(input);

            Participants = GetObject<TLObject>(input) as TLChatParticipantsBase;
            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(Title.ToBytes());
            Photo.ToStream(output);
            ParticipantIds.ToStream(output);

            Participants.NullableToStream(output);
            NotifySettings.NullableToStream(output);
        }

        public override void Update(TLChatBase chat)
        {
            base.Update(chat);
            var c = chat as TLBroadcastChat;
            if (c != null)
            {
                _title = c.Title;
                if (Photo.GetType() != c.Photo.GetType())
                {
                    _photo = c.Photo;
                }
                else
                {
                    Photo.Update(c.Photo);
                }
                ParticipantIds = c.ParticipantIds;
            }
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerBroadcast { ChatId = Id };
        }

        public override string GetUnsendedTextFileName()
        {
            return "b" + Id + ".dat";
        }

        public override bool IsForbidden
        {
            get { return false; }
        }
    } 

    public class TLChannel : TLBroadcastChat, IUserName
    {
        public new const uint Signature = TLConstructors.TLChannel;

        private TLLong _customFlags;

        public TLLong CustomFlags
        {
            get { return _customFlags; }
            set { _customFlags = value; }
        }

        protected TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; }
            set
            {
                _flags = value;
            }
        }

        public virtual TLBool Left
        {
            get { return new TLBool(IsSet(_flags, (int)ChatFlags.Left)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)ChatFlags.Left);
                }
            }
        }

        public TLLong AccessHash { get; set; }

        private TLString _userName;

        public TLString UserName
        {
            get { return _userName; }
            set
            {
                if (value != null)
                {
                    Set(ref _flags, (int)ChannelFlags.Public);
                    _userName = value;
                }
            }
        }

        public TLInt Date { get; set; }

        public TLInt Version { get; set; }

        public TLString About { get; set; }

        public TLInt ParticipantsCount { get; set; }

        public TLInt AdminsCount { get; set; }

        public TLInt KickedCount { get; set; }

        public TLInt ReadInboxMaxId { get; set; }

        public TLInt Pts { get; set; }

        public override string FullName
        {
            get { return Title != null ? Title.ToString() : string.Empty; }
        }

        public bool Creator { get { return IsSet(Flags, (int)ChannelFlags.Creator); } }

        public bool IsEditor { get { return IsSet(Flags, (int)ChannelFlags.Editor); } }

        public bool IsModerator { get { return IsSet(Flags, (int)ChannelFlags.Moderator); } }

        public bool IsBroadcast { get { return IsSet(Flags, (int) ChannelFlags.Broadcast); } }

        public bool IsPublic { get { return IsSet(Flags, (int)ChannelFlags.Public); } }

        public bool IsKicked { get { return IsSet(Flags, (int)ChannelFlags.Kicked); } }
        
        public bool IsVerified { get { return IsSet(Flags, (int)ChannelFlags.Verified); } }

        public bool IsMegaGroup { get { return IsSet(Flags, (int)ChannelFlags.MegaGroup); } }

        #region Additional

        private TLInt _migratedFromChatId;

        public TLInt MigratedFromChatId
        {
            get { return _migratedFromChatId; }
            set
            {
                if (value != null)
                {
                    _migratedFromChatId = value;
                    Set(ref _customFlags, (int) ChannelCustomFlags.MigratedFromChatId);
                }
                else
                {
                    _migratedFromChatId = null;
                    Unset(ref _customFlags, (int)ChannelCustomFlags.MigratedFromChatId);
                }
            }
        }

        private TLInt _migratedFromMaxId;

        public TLInt MigratedFromMaxId
        {
            get { return _migratedFromMaxId; }
            set
            {
                if (value != null)
                {
                    _migratedFromMaxId = value;
                    Set(ref _customFlags, (int)ChannelCustomFlags.MigratedFromMaxId);
                }
                else
                {
                    _migratedFromMaxId = null;
                    Unset(ref _customFlags, (int)ChannelCustomFlags.MigratedFromMaxId);
                }
            }
        }
        #endregion

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            _title = GetObject<TLString>(bytes, ref position);
            if (IsSet(Flags, (int) ChannelFlags.Public))
            {
                UserName = GetObject<TLString>(bytes, ref position);
            }
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Id = GetObject<TLInt>(input);
            AccessHash = GetObject<TLLong>(input);
            _title = GetObject<TLString>(input);
            if (IsSet(Flags, (int)ChannelFlags.Public))
            {
                UserName = GetObject<TLString>(input);
            }
            _photo = GetObject<TLPhotoBase>(input);
            Date = GetObject<TLInt>(input);
            Version = GetObject<TLInt>(input);

            CustomFlags = GetNullableObject<TLLong>(input);

            ParticipantIds = GetNullableObject<TLVector<TLInt>>(input);
            About = GetNullableObject<TLString>(input);
            ParticipantsCount = GetNullableObject<TLInt>(input);
            AdminsCount = GetNullableObject<TLInt>(input);
            KickedCount = GetNullableObject<TLInt>(input);
            ReadInboxMaxId = GetNullableObject<TLInt>(input);
            Pts = GetNullableObject<TLInt>(input);
            Participants = GetNullableObject<TLChatParticipantsBase>(input);
            NotifySettings = GetNullableObject<TLPeerNotifySettingsBase>(input);

            if (IsSet(CustomFlags, (int) ChannelCustomFlags.MigratedFromChatId))
            {
                _migratedFromChatId = GetObject<TLInt>(input);
            }
            if (IsSet(CustomFlags, (int)ChannelCustomFlags.MigratedFromMaxId))
            {
                _migratedFromMaxId = GetObject<TLInt>(input);
            }

            return this;
        }

        public override void ToStream(Stream output)
        {
            try
            {
                output.Write(TLUtils.SignatureToBytes(Signature));

                output.Write(Flags.ToBytes());
                output.Write(Id.ToBytes());
                output.Write(AccessHash.ToBytes());
                output.Write(Title.ToBytes());
                if (IsSet(Flags, (int)ChannelFlags.Public))
                {
                    UserName.ToStream(output);
                }
                Photo.ToStream(output);
                Date.ToStream(output);
                Version.ToStream(output);

                CustomFlags.NullableToStream(output);

                ParticipantIds.NullableToStream(output);
                About.NullableToStream(output);
                ParticipantsCount.NullableToStream(output);
                AdminsCount.NullableToStream(output);
                KickedCount.NullableToStream(output);
                ReadInboxMaxId.NullableToStream(output);
                Pts.NullableToStream(output);
                Participants.NullableToStream(output);
                NotifySettings.NullableToStream(output);

                if (IsSet(CustomFlags, (int)ChannelCustomFlags.MigratedFromChatId))
                {
                    _migratedFromChatId.ToStream(output);
                }
                if (IsSet(CustomFlags, (int)ChannelCustomFlags.MigratedFromMaxId))
                {
                    _migratedFromMaxId.ToStream(output);
                }
            }
            catch (Exception ex)
            {
                
            }

        }

        public override void Update(TLChatBase chat)
        {
            base.Update(chat);
            var c = chat as TLChannel;
            if (c != null)
            {
                if (c.Flags != null) Flags = c.Flags;

                if (c.CustomFlags != null) CustomFlags = c.CustomFlags;

                if (c.ParticipantIds != null) ParticipantIds = c.ParticipantIds;
                if (c.About != null) About = c.About;
                if (c.ParticipantsCount != null) ParticipantsCount = c.ParticipantsCount;
                if (c.AdminsCount != null) AdminsCount = c.AdminsCount;
                if (c.KickedCount != null) KickedCount = c.KickedCount;
                if (c.ReadInboxMaxId != null) ReadInboxMaxId = c.ReadInboxMaxId;
                if (c.Participants != null) Participants = c.Participants;
                if (c.NotifySettings != null) NotifySettings = c.NotifySettings;
            }
        }

        public override string ToString()
        {
            return Title.ToString();
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerChannel { ChatId = Id, AccessHash = AccessHash };
        }

        public  TLInputChannelBase ToInputChannel()
        {
            return new TLInputChannel { ChannelId = Id, AccessHash = AccessHash };
        }

        public override string GetUnsendedTextFileName()
        {
            return "ch" + Id + ".dat";
        }

        public override bool IsForbidden
        {
            get { return Left.Value; }
        }
    }

    public class TLChannelForbidden : TLChatBase
    {
        public const uint Signature = TLConstructors.TLChannelForbidden;

        public TLLong CustomFlags { get; set; }

        public TLLong AccessHash { get; set; }

        public TLString Title { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            Title = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            AccessHash = GetObject<TLLong>(input);
            Title = GetObject<TLString>(input);

            Participants = GetObject<TLObject>(input) as TLChatParticipantsBase;
            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(AccessHash.ToBytes());
            output.Write(Title.ToBytes());

            Participants.NullableToStream(output);
            NotifySettings.NullableToStream(output);
        }

        public override void Update(TLChatBase chat)
        {
            base.Update(chat);
            var c = chat as TLChannelForbidden;
            if (c != null)
            {
                Id = c.Id;
                AccessHash = c.AccessHash;
                Title = c.Title;

                if (c.CustomFlags != null)
                {
                    CustomFlags = c.CustomFlags;
                }
            }
        }

        public override string FullName
        {
            get
            {
                return Title != null ? Title.ToString() : string.Empty;
            }
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerChannel { ChatId = Id, AccessHash = AccessHash };
        }

        public TLInputChannelBase ToInputChannel()
        {
            return new TLInputChannel { ChannelId = Id, AccessHash = AccessHash };
        }

        public override string GetUnsendedTextFileName()
        {
            return "ch" + Id + ".dat";
        }

        public override bool IsForbidden
        {
            get { return true; }
        }
    }

}
