using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLUser : ITLInputPeer
    {
        public override string FullName
        {
            get
            {
                if (IsDeleted)
                {
                    return LocaleHelper.GetString("HiddenName");
                }

                //if (this is TLUserEmpty)
                //{
                //    return "Empty user";
                //}

                //if (this is TLUserDeleted)
                //{
                //    return "Deleted user";
                //}

                //if (ExtendedInfo != null)
                //{
                //    return string.Format("{0} {1}", ExtendedInfo.FirstName, ExtendedInfo.LastName);
                //}

                var firstName = FirstName ?? string.Empty;
                var lastName = LastName ?? string.Empty;

                if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
                {
                    return Phone != null ? "+" + Phone : string.Empty;
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
        }

        public override void Update(TLUserBase userBase)
        {
            base.Update(userBase);

            try
            {
                var user = userBase as TLUser;
                if (user != null)
                {
                    if (user.IsMin)
                    {
                        // TODO: ???
                        return;
                    }

                    // TODO: check if dirty?

                    this.Flags = user.Flags;
                    this.AccessHash = user.AccessHash;
                    this.FirstName = user.FirstName;
                    this.LastName = user.LastName;
                    this.Username = user.Username;
                    this.Phone = user.Phone;
                    this.Photo = user.Photo;
                    this.Status = user.Status;
                    this.BotInfoVersion = user.BotInfoVersion;
                    this.RestrictionReason = user.RestrictionReason;
                    this.BotInlinePlaceholder = user.BotInlinePlaceholder;
                    this.LangCode = user.LangCode;

                    //FirstName = user.FirstName;
                    //LastName = user.LastName;
                    //Phone = user.Phone;

                    //if (Photo != null && user.Photo != null && Photo.GetType() != user.Photo.GetType())
                    //{
                    //    Photo.Update(user.Photo);
                    //}
                    //else
                    //{
                    //    Photo = user.Photo;
                    //}

                    //Status = user.Status;
                }
            }
            catch (Exception e)
            {

            }
        }

        // TODO
        public override TLInputPeerBase ToInputPeer()
        {
            if (HasAccessHash)
            {
                //if (IsContact || IsMutualContact)
                //{
                //    var userContact = new TLInputPeerContact
                //    {
                //        UserId = Id
                //    };

                //    return userContact;
                //}

                //var userForeign = new TLInputPeerForeign
                //{
                //    UserId = Id,
                //    AccessHash = AccessHash
                //};

                //return userForeign;

                return new TLInputPeerUser { UserId = Id, AccessHash = AccessHash.Value };
            }

            //if (IsDeleted)
            //{
            //    var userDeleted = new TLInputPeerContact { UserId = Id };

            //    return userDeleted;
            //}

            if (IsSelf)
            {
                return new TLInputPeerSelf();
            }

            Helpers.Execute.ShowDebugMessage("TLUser.ToInputPeer unknown " + FirstName);

            return new TLInputPeerUser { UserId = Id };
            //return null;
        }

        public TLInputUserBase ToInputUser()
        {
            if (HasAccessHash)
            {
                //if (IsSet(Flags, (int)UserFlags.Contact)
                //    || IsSet(Flags, (int)UserFlags.ContactMutual))
                //{
                //    var userContact = new TLInputUserContact
                //    {
                //        UserId = Id
                //    };

                //    return userContact;
                //}

                //var userForeign = new TLInputUserForeign
                //{
                //    UserId = Id,
                //    AccessHash = AccessHash
                //};

                //return userForeign;

                return new TLInputUser { AccessHash = AccessHash.Value, UserId = Id };
            }

            //if (IsSet(Flags, (int)UserFlags.Deleted))
            //{
            //    var userDeleted = new TLInputUserContact { UserId = Id };

            //    return userDeleted;
            //}

            if (IsSelf)
            {
                return new TLInputUserSelf();
            }

            Helpers.Execute.ShowDebugMessage("TLUser.ToInputUser unknown " + FullName);

            return new TLInputUser { UserId = Id };
            //return null;
        }

        public override TLPeerBase ToPeer()
        {
            return new TLPeerUser { UserId = Id };
        }

        public string ExtractRestrictionReason()
        {
            if (HasRestrictionReason)
            {
                var fullTypeEnd = RestrictionReason.IndexOf(':');
                if (fullTypeEnd <= 0)
                {
                    return null;
                }

                // {fulltype} is in "{type}-{tag}-{tag}-{tag}" format
                // if we find "all" tag we return the restriction string
                var typeTags = RestrictionReason.Substring(0, fullTypeEnd).Split('-')[1];
#if STORE_RESTRICTIVE
                var restrictionApplies = typeTags.Contains("all") || typeTags.Contains("ios");
#else
                var restrictionApplies = typeTags.Contains("all");
#endif
                if (restrictionApplies)
                {
                    return RestrictionReason.Substring(fullTypeEnd + 1).Trim();
                }
            }

            return null;
        }
    }
}
