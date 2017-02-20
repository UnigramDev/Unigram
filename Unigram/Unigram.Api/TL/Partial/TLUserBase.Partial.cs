using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLUserBase : ITLReadMaxId
    {
        public TLContact Contact { get; set; }

        public long? ClientId { get; set; }

        #region UserFull information

        public TLContactsLink Link { get; set; }

        public TLPhotoBase ProfilePhoto { get; set; }

        public TLPeerNotifySettingsBase NotifySettings { get; set; }

        public virtual bool Blocked { get; set; }

        public TLBotInfo BotInfo { get; set; }
        #endregion

        public virtual void Update(TLUserBase user)
        {
            try
            {
                if (user.Contact != null)
                {
                    Contact = user.Contact;
                }

                if (user.Link != null)
                {
                    Link = user.Link;
                }

                if (user.ProfilePhoto != null)
                {
                    ProfilePhoto = user.ProfilePhoto;
                }

                if (user.NotifySettings != null)
                {
                    NotifySettings = user.NotifySettings;
                }
                if (user.ReadInboxMaxId != 0 && (ReadInboxMaxId == 0 || ReadInboxMaxId < user.ReadInboxMaxId))
                {
                    ReadInboxMaxId = user.ReadInboxMaxId;
                }
                if (user.ReadOutboxMaxId != 0 && (ReadOutboxMaxId == 0 || ReadOutboxMaxId < user.ReadOutboxMaxId))
                {
                    ReadOutboxMaxId = user.ReadOutboxMaxId;
                }

                //if (user.ExtendedInfo != null)
                //{
                //    ExtendedInfo = user.ExtendedInfo;
                //}

                if (user.Blocked != null)
                {
                    Blocked = user.Blocked;
                }
            }
            catch (Exception e)
            {

            }
        }

        #region Add
        public virtual string FullName
        {
            get
            {
                if (this is TLUserEmpty)
                {
                    return "Empty user";
                }

                //if (this is TLUserDeleted)
                //{
                //    return "Deleted user";
                //}

                //if (ExtendedInfo != null)
                //{
                //    return string.Format("{0} {1}", ExtendedInfo.FirstName, ExtendedInfo.LastName);
                //}

                var user = this as TLUser;
                if (user != null)
                {
                    var firstName = user.FirstName ?? string.Empty;
                    var lastName = user.LastName ?? string.Empty;

                    //if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
                    //{
                    //    return Phone != null ? "+" + Phone : string.Empty;
                    //}

                    if (string.Equals(firstName, lastName, StringComparison.OrdinalIgnoreCase))
                    {
                        return firstName;
                    }

                    if (string.IsNullOrEmpty(firstName))
                    {
                        return lastName;
                    }

                    if (string.IsNullOrEmpty(lastName))
                    {
                        return firstName;
                    }

                    return string.Format("{0} {1}", firstName, lastName);
                }

                return Id.ToString();
            }
        }

        #endregion

        public int ReadInboxMaxId
        {
            get;
            set;
        }

        public int ReadOutboxMaxId
        {
            get;
            set;
        }

        //public override void ReadFromCache(TLBinaryReader from)
        //{
        //    ReadInboxMaxId = from.ReadInt32();
        //    ReadOutboxMaxId = from.ReadInt32();
        //}

        //public override void WriteToCache(TLBinaryWriter to)
        //{
        //    to.Write(ReadInboxMaxId);
        //    to.Write(ReadOutboxMaxId);
        //}
    }
}
