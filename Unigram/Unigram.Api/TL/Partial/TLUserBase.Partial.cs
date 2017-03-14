using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Helpers;

namespace Telegram.Api.TL
{
    public abstract partial class TLUserBase : ITLReadMaxId, ITLDialogWith
    {
        public TLContact Contact { get; set; }

        public long? ClientId { get; set; }

        #region UserFull information

        public TLContactsLink Link { get; set; }

        public TLPhotoBase ProfilePhoto { get; set; }

        public TLPeerNotifySettingsBase NotifySettings { get; set; }

        public virtual bool IsBlocked { get; set; }

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

                if (user.IsBlocked != null)
                {
                    IsBlocked = user.IsBlocked;
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

        public virtual object PhotoSelf
        {
            get
            {
                return this;
            }
        }

        public string DisplayName
        {
            get
            {
                var userBase = this as TLUserBase;
                if (userBase != null)
                {
                    var user = this as TLUser;
                    if (user != null && user.IsSelf)
                    {
                        return "You";
                    }

                    if (userBase.Id == 333000)
                    {
                        //return AppResources.AppName;
                        return "Telegram";
                    }

                    if (userBase.Id == 777000)
                    {
                        //return AppResources.TelegramNotifications;
                        return "Telegram";
                    }

                    if (user != null && user.HasPhone && !user.IsSelf && !user.IsContact && !user.IsMutualContact)
                    {
                        return PhoneNumber.Format(user.Phone);
                    }

                    //                    var userRequest = user as TLUserRequest;
                    //                    if (userRequest != null)
                    //                    {
                    //#if WP8
                    //                    //var phoneUtil = PhoneNumberUtil.GetInstance();
                    //                    //try
                    //                    //{
                    //                    //    return phoneUtil.Format(phoneUtil.Parse("+" + user.Phone.Value, ""), PhoneNumberFormat.INTERNATIONAL);
                    //                    //}
                    //                    //catch (Exception e)
                    //                    {
                    //                        return "+" + user.Phone.Value;
                    //                    }
                    //#else
                    //                        return "+" + user.Phone.Value;
                    //#endif

                    //                    }

                    if (userBase is TLUserEmpty /*|| user is TLUserDeleted*/)
                    {

                    }

                    return userBase.FullName.Trim();
                }

                //var encryptedChat = With as TLEncryptedChatCommon;
                //if (encryptedChat != null)
                //{
                //    var currentUserId = IoC.Get<IMTProtoService>().CurrentUserId;
                //    var cache = IoC.Get<ICacheService>();

                //    if (currentUserId.Value == encryptedChat.AdminId.Value)
                //    {
                //        var cachedParticipant = cache.GetUser(encryptedChat.ParticipantId);
                //        return cachedParticipant != null ? cachedParticipant.FullName.Trim() : string.Empty;
                //    }

                //    var cachedAdmin = cache.GetUser(encryptedChat.AdminId);
                //    return cachedAdmin != null ? cachedAdmin.FullName.Trim() : string.Empty;
                //}

                return ToString();
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

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged(string propertyName)
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }

        public virtual TLInputPeerBase ToInputPeer()
        {
            throw new NotImplementedException();
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
