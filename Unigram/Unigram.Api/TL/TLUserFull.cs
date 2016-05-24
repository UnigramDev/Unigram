namespace Telegram.Api.TL
{
    public class TLUserFull : TLObject
    {
        public const uint Signature = TLConstructors.TLUserFull;

        public TLUserBase User { get; set; }

        public TLLinkBase Link { get; set; }

        public TLPhotoBase ProfilePhoto { get; set; }

        public TLPeerNotifySettingsBase NotifySettings { get; set; }

        public TLBool Blocked { get; set; }

        public TLString RealFirstName { get; set; }

        public TLString RealLastName { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            User = GetObject<TLUserBase>(bytes, ref position);
            Link = GetObject<TLLinkBase>(bytes, ref position);
            ProfilePhoto = GetObject<TLPhotoBase>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);
            Blocked = GetObject<TLBool>(bytes, ref position);
            RealFirstName = GetObject<TLString>(bytes, ref position);
            RealLastName = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public virtual TLUserBase ToUser()
        {
            User.Link = Link;
            User.ProfilePhoto = ProfilePhoto;
            User.NotifySettings = NotifySettings;
            User.Blocked = Blocked;

            return User;
        }
    }

    public class TLUserFull31 : TLUserFull
    {
        public new const uint Signature = TLConstructors.TLUserFull31;

        public TLBotInfoBase BotInfo { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            User = GetObject<TLUserBase>(bytes, ref position);
            Link = GetObject<TLLinkBase>(bytes, ref position);
            ProfilePhoto = GetObject<TLPhotoBase>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);
            Blocked = GetObject<TLBool>(bytes, ref position);
            BotInfo = GetObject<TLBotInfoBase>(bytes, ref position);

            return this;
        }

        public override TLUserBase ToUser()
        {
            User.Link = Link;
            User.ProfilePhoto = ProfilePhoto;
            User.NotifySettings = NotifySettings;
            User.Blocked = Blocked;
            User.BotInfo = BotInfo;

            return User;
        }
    }
}
