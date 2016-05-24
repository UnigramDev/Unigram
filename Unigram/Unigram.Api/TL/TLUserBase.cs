using System;
using System.Collections.Generic;
using System.IO;
#if WINDOWS_PHONE
using System.Windows;
using Microsoft.Phone.UserData;
#elif WIN_RT
using Windows.UI.Xaml;
#endif
using Telegram.Api.TL.Interfaces;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [Flags]
    public enum UserFlags
    {
        AccessHash = 0x1,       // 0
        FirstName = 0x2,        // 1
        LastName = 0x4,
        UserName = 0x8,
        Phone = 0x10,
        Photo = 0x20,
        Status = 0x40,          // 6
        // = 0x80,              // 7
        // = 0x100,             // 8
        // = 0x200,             // 9
        Self = 0x400,           // 10
        Contact = 0x800,
        ContactMutual = 0x1000,
        Deleted = 0x2000,
        Bot = 0x4000,
        BotAllHistory = 0x8000,
        BotGroupsBlocked = 0x10000,
    }

    [Flags]
    public enum UserCustomFlags
    {
        Blocked = 0x1,       // 0
    }

    public interface IUserName
    {
        TLString UserName { get; set; }
    }

    public interface INotifySettings
    {
        TLPeerNotifySettingsBase NotifySettings { get; set; }
    }

    public class TLUserExtendedInfo : TLObject
    {
        public const uint Signature = TLConstructors.TLUserExtendedInfo;

        public TLString FirstName { get; set; }

        public TLString LastName { get; set; }

        public override TLObject FromStream(Stream input)
        {
            FirstName = GetObject<TLString>(input);
            LastName = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
        }
    }

    public class TLUserNotRegistered : TLUserBase
    {
#if WINDOWS_PHONE
        public IEnumerable<ContactPhoneNumber> PhoneNumbers { get; set; } 
#endif

        public override TLInputUserBase ToInputUser()
        {
            return null;
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return null;
        }

        public override string GetUnsendedTextFileName()
        {
            return "u" + Id + ".dat";
        }
    }

    public abstract class TLUserBase : TLObject, IInputPeer, ISelectable, IFullName, INotifySettings, IVIsibility
    {
        public string AccessToken { get; set; }

        public TLUserBase Self
        {
            get { return this; }
        }

        public int Index
        {
            get { return Id.Value; }
            set { Id = new TLInt(value); }
        }

        public TLInt Id { get; set; }

        public TLString _firstName;

        public TLString FirstName
        {
            get { return _firstName; }
            set
            {
                SetField(ref _firstName, value, () => FirstName);
                NotifyOfPropertyChange(() => FullName);
            }
        }

        public TLString _lastName;

        public TLString LastName
        {
            get { return _lastName; }
            set
            {
                SetField(ref _lastName, value, () => LastName);
                NotifyOfPropertyChange(() => FullName);
            }
        }

        public TLString Phone { get; set; }

        public TLPhotoBase _photo;

        public TLPhotoBase Photo
        {
            get { return _photo; }
            set { SetField(ref _photo, value, () => Photo); }
        }

        public TLUserStatus _status;

        public TLUserStatus Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    NotifyOfPropertyChange(() => Status);
                    NotifyOfPropertyChange(() => StatusCommon);
                }
            }
        }

        public TLUserBase StatusCommon
        {
            get { return this; }
        }

        public int StatusValue
        {
            get
            {
                if (Status is TLUserStatusOnline)
                {
                    return int.MaxValue;
                }
                var offline = Status as TLUserStatusOffline;
                if (offline != null)
                {
                    return offline.WasOnline.Value;
                }
                
                return int.MinValue;
            }
        }

        #region Additional

        public bool RemoveUserAction { get; set; }

        public TLContact Contact { get; set; }

        public override string ToString()
        {
            return string.Format("{0} {1} {2}", GetType().Name, Index, FullName);
        }

        public virtual string FullName
        {
            get
            {
                if (this is TLUserEmpty)
                {
                    return "Empty user";
                }

                if (this is TLUserDeleted)
                {
                    return "Deleted user";
                }

                if (ExtendedInfo != null)
                {
                    return string.Format("{0} {1}", ExtendedInfo.FirstName, ExtendedInfo.LastName);
                }

                var firstName = FirstName != null ? FirstName.ToString() : string.Empty;
                var lastName = LastName != null ? LastName.ToString() : string.Empty;
                
                if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
                {
                    return Phone != null ? "+" + Phone : string.Empty;
                }
                
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
        }

        public virtual bool HasPhone { get { return Phone != null && !string.IsNullOrEmpty(Phone.ToString()); } }

        public abstract TLInputUserBase ToInputUser();

        public virtual void Update(TLUserBase user)
        {
            try
            {
                _firstName = user.FirstName;
                _lastName = user.LastName;
                Phone = user.Phone;

                if (Photo != null
                    && user.Photo != null
                    && Photo.GetType() != user.Photo.GetType())
                {
                    Photo.Update(user.Photo);
                }
                else
                {
                    Photo = user.Photo;
                }

                _status = user.Status;

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

                if (user.ExtendedInfo != null)
                {
                    ExtendedInfo = user.ExtendedInfo;
                }

                if (user.Blocked != null)
                {
                    Blocked = user.Blocked;
                }
            }
            catch (Exception e)
            {
                
            }

        }

        public abstract TLInputPeerBase ToInputPeer();

        public virtual string GetUnsendedTextFileName()
        {
            return "u" + Id + ".dat";
        }

        public TLUserExtendedInfo ExtendedInfo { get; set; }

        #region UserFull information

        public TLLinkBase Link { get; set; }

        public TLPhotoBase ProfilePhoto { get; set; }

        public TLPeerNotifySettingsBase NotifySettings { get; set; }

        public virtual TLBool Blocked { get; set; }

        public TLBotInfoBase BotInfo { get; set; }
        #endregion

        public Visibility DeleteActionVisibility { get; set; }

        #endregion

        public TLInputNotifyPeerBase ToInputNotifyPeer()
        {
            return new TLInputNotifyPeer{Peer = ToInputPeer()};
        }

        private bool _isVisible;

        public bool IsVisible
        {
            get { return _isVisible; }
            set { SetField(ref _isVisible, value, () => IsVisible); }
        }

        public bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetField(ref _isSelected, value, () => IsSelected); }
        }
        
        private string _selectedText;

        public string SelectedText
        {
            get { return _selectedText; }
            set { SetField(ref _selectedText, value, () => SelectedText); }
        }

        #region PhoneBook
        public TLLong ClientId { get; set; }

        public string Mobile { get; set; }

        public string Mobile2 { get; set; }


        public string Home { get; set; }

        public string Home2 { get; set; }


        public string Work { get; set; }

        public string Work2 { get; set; }


        public string Company { get; set; }

        public string Pager { get; set; }

        public string HomeFax { get; set; }

        public string WorkFax { get; set; }

        #endregion

        public static string GetLastNameKey(TLUserBase person)
        {
            if (person.LastName == null) return ('#').ToString();

            char key = char.ToLower(person.LastName.Value[0]);

            if (key < 'a' || key > 'z')
            {
                if (key < 'а' || key > 'я')
                {
                    key = '#';
                }
            }

            return key.ToString();
        }

        public static int CompareByLastName(object obj1, object obj2)
        {
            var p1 = (TLUserBase)obj1;
            var p2 = (TLUserBase)obj2;

            if (p1.LastName == null && p2.LastName != null)
            {
                return -1;
            }

            if (p1.LastName != null && p2.LastName == null)
            {
                return 1;
            }

            if (p1.LastName == null && p2.LastName == null)
            {
                return 0;
            }


            int result = String.Compare(p1.LastName.Value, p2.LastName.Value, StringComparison.Ordinal);
            //if (result == 0)
            //{
            //    result = String.Compare(p1.FirstName.Value, p2.FirstName.Value, StringComparison.Ordinal);
            //}

            return result;
        }
    }

    public class TLUser : TLUserBase, IUserName
    {
        public const uint Signature = TLConstructors.TLUser;

        public TLInt Flags { get; set; }

        public TLLong AccessHash { get; set; }

        public TLString UserName { get; set; }

        public TLInt BotInfoVersion { get; set; }

        private TLLong _customFlags;

        public TLLong CustomFlags
        {
            get { return _customFlags; }
            set { _customFlags = value; }
        }

        private TLBool _blocked;

        public override TLBool Blocked
        {
            get { return _blocked; }
            set
            {
                if (value != null)
                {
                    Set(ref _customFlags, (int)UserCustomFlags.Blocked);
                    _blocked = value;
                }
            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            AccessHash = GetObject(Flags, UserFlags.AccessHash, new TLLong(0), bytes, ref position);
            _firstName = GetObject(Flags, UserFlags.FirstName, TLString.Empty, bytes, ref position);
            _lastName = GetObject(Flags, UserFlags.LastName, TLString.Empty, bytes, ref position);
            UserName = GetObject(Flags, UserFlags.UserName, TLString.Empty, bytes, ref position);
            Phone = GetObject(Flags, UserFlags.Phone, TLString.Empty, bytes, ref position);
            _photo = GetObject<TLPhotoBase>(Flags, UserFlags.Photo, new TLUserProfilePhotoEmpty(), bytes, ref position);
            _status = GetObject<TLUserStatus>(Flags, UserFlags.Status, new TLUserStatusEmpty(), bytes, ref position);
            BotInfoVersion = GetObject(Flags, UserFlags.Bot, new TLInt(0), bytes, ref position);

            if (IsSet(Flags, (int)UserFlags.Bot)
                || IsSet(Flags, (int)UserFlags.BotAllHistory))
            {
                return this;
            }

            if (IsSet(Flags, (int) UserFlags.AccessHash))
            {
                if (IsSet(Flags, (int)UserFlags.Contact)
                    || IsSet(Flags, (int)UserFlags.ContactMutual))
                {
                    var userContact = new TLUserContact18
                    {
                        Id = Id,
                        AccessHash = AccessHash,
                        _firstName = _firstName,
                        _lastName = _lastName,
                        UserName = UserName,
                        Phone = Phone,
                        _photo = _photo,
                        _status = _status,
                    };

                    return userContact;
                }

                if (IsSet(Flags, (int) UserFlags.Phone))
                {
                    var userRequest = new TLUserRequest18
                    {
                        Id = Id,
                        AccessHash = AccessHash,
                        _firstName = _firstName,
                        _lastName = _lastName,
                        UserName = UserName,
                        Phone = Phone,
                        _photo = _photo,
                        _status = _status,
                    };

                    return userRequest;
                }

                var userForeign = new TLUserForeign18
                {
                    Id = Id,
                    AccessHash = AccessHash,
                    _firstName = _firstName,
                    _lastName = _lastName,
                    UserName = UserName,
                    _photo = _photo,
                    _status = _status,
                };

                return userForeign;
            }

            if (IsSet(Flags, (int)UserFlags.Deleted))
            {
                var userDeleted = new TLUserDeleted18
                {
                    Id = Id,
                    _firstName = _firstName,
                    _lastName = _lastName,
                    UserName = UserName
                };

                return userDeleted;
            }

            if (IsSet(Flags, (int)UserFlags.Self))
            {
                var userSelf = new TLUserSelf24
                {
                    Id = Id,
                    _firstName = _firstName,
                    _lastName = _lastName,
                    UserName = UserName,
                    Phone = Phone,
                    _photo = _photo,
                    _status = _status,
                };

                return userSelf;
            }

            Helpers.Execute.ShowDebugMessage("TLUser unknown " + FullName);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Id.ToBytes(),
                ToBytes(AccessHash, Flags, (int)UserFlags.AccessHash),
                ToBytes(FirstName, Flags, (int)UserFlags.FirstName),
                ToBytes(LastName, Flags, (int)UserFlags.LastName),
                ToBytes(UserName, Flags, (int)UserFlags.UserName),
                ToBytes(Phone, Flags, (int)UserFlags.Phone),
                ToBytes(Photo, Flags, (int)UserFlags.Photo),
                ToBytes(Status, Flags, (int)UserFlags.Status),
                ToBytes(BotInfoVersion, Flags, (int)UserFlags.Bot));
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Id = GetObject<TLInt>(input);
            AccessHash = GetObject(Flags, UserFlags.AccessHash, new TLLong(0), input);
            _firstName = GetObject(Flags, UserFlags.FirstName, TLString.Empty, input);
            _lastName = GetObject(Flags, UserFlags.LastName, TLString.Empty, input);
            UserName = GetObject(Flags, UserFlags.UserName, TLString.Empty, input);
            Phone = GetObject(Flags, UserFlags.Phone, TLString.Empty, input);
            _photo = GetObject<TLPhotoBase>(Flags, UserFlags.Photo, new TLUserProfilePhotoEmpty(), input);
            _status = GetObject<TLUserStatus>(Flags, UserFlags.Status, new TLUserStatusEmpty(), input);
            BotInfoVersion = GetObject(Flags, UserFlags.Bot, new TLInt(0), input);

            CustomFlags = GetNullableObject<TLLong>(input);

            NotifySettings = GetNullableObject<TLPeerNotifySettingsBase>(input);
            ExtendedInfo = GetNullableObject<TLUserExtendedInfo>(input);
            BotInfo = GetNullableObject<TLBotInfoBase>(input);

            // as bit
            _blocked = GetObject<TLBool>(CustomFlags, UserCustomFlags.Blocked, null, input);

            return this;
        }

        public override void Update(TLUserBase userBase)
        {
            base.Update(userBase);

            var user = userBase as TLUser;
            if (user != null)
            {
                Flags = user.Flags;
                Id = user.Id;
                AccessHash = user.AccessHash;
                UserName = user.UserName;
                BotInfoVersion = user.BotInfoVersion;

                if (user.CustomFlags != null)
                {
                    CustomFlags = user.CustomFlags;
                }

                if (user.BotInfo != null)
                {
                    BotInfo = user.BotInfo;
                }
            }
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Flags.ToStream(output);
            Id.ToStream(output);
            ToStream(output, AccessHash, Flags, (int)UserFlags.AccessHash);
            ToStream(output, FirstName, Flags, (int)UserFlags.FirstName);
            ToStream(output, LastName, Flags, (int)UserFlags.LastName);
            ToStream(output, UserName, Flags, (int)UserFlags.UserName);
            ToStream(output, Phone, Flags, (int)UserFlags.Phone);
            ToStream(output, Photo, Flags, (int)UserFlags.Photo);
            ToStream(output, Status, Flags, (int)UserFlags.Status);
            ToStream(output, BotInfoVersion, Flags, (int)UserFlags.Bot);

            CustomFlags.NullableToStream(output);

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            BotInfo.NullableToStream(output);

            // as bit
            ToStream(output, Blocked, CustomFlags, (int)UserCustomFlags.Blocked);
        }

        public static T GetObject<T>(TLInt flags, UserFlags flag, T defaultValue, byte[] bytes, ref int position) where T : TLObject
        {
            return IsSet(flags, (int)flag) ? GetObject<T>(bytes, ref position) : defaultValue;
        }

        public static T GetObject<T>(TLInt flags, UserFlags flag, T defaultValue, Stream inputStream) where T : TLObject
        {
            return IsSet(flags, (int)flag) ? GetObject<T>(inputStream) : defaultValue;
        }

        public static T GetObject<T>(TLLong customFlags, UserCustomFlags flag, T defaultValue, Stream inputStream) where T : TLObject
        {
            return IsSet(customFlags, (int)flag) ? GetObject<T>(inputStream) : defaultValue;
        }

        public override TLInputUserBase ToInputUser()
        {
            if (IsSet(Flags, (int)UserFlags.AccessHash))
            {
                if (IsSet(Flags, (int)UserFlags.Contact)
                    || IsSet(Flags, (int)UserFlags.ContactMutual))
                {
                    var userContact = new TLInputUserContact
                    {
                        UserId = Id
                    };

                    return userContact;
                }

                var userForeign = new TLInputUserForeign
                {
                    UserId = Id,
                    AccessHash = AccessHash
                };

                return userForeign;
            }

            if (IsSet(Flags, (int)UserFlags.Deleted))
            {
                var userDeleted = new TLInputUserContact { UserId = Id };

                return userDeleted;
            }

            if (IsSet(Flags, (int)UserFlags.Self))
            {
                var userSelf = new TLInputUserSelf();

                return userSelf;
            }

            Helpers.Execute.ShowDebugMessage("TLUser.ToInputUser unknown " + FullName);

            return null;
        }

        public override TLInputPeerBase ToInputPeer()
        {
            if (IsSet(Flags, (int)UserFlags.AccessHash))
            {
                if (IsSet(Flags, (int)UserFlags.Contact)
                    || IsSet(Flags, (int)UserFlags.ContactMutual))
                {
                    var userContact = new TLInputPeerContact
                    {
                        UserId = Id
                    };

                    return userContact;
                }

                var userForeign = new TLInputPeerForeign
                {
                    UserId = Id,
                    AccessHash = AccessHash
                };

                return userForeign;
            }

            if (IsSet(Flags, (int)UserFlags.Deleted))
            {
                var userDeleted = new TLInputPeerContact { UserId = Id };

                return userDeleted;
            }

            if (IsSet(Flags, (int)UserFlags.Self))
            {
                var userSelf = new TLInputPeerSelf();

                return userSelf;
            }

            Helpers.Execute.ShowDebugMessage("TLUser.ToInputPeer unknown " + FullName);

            return null;
        }

        public bool IsBot
        {
            get { return IsSet(Flags, (int)UserFlags.Bot); }
        }

        public bool IsBotAllHistory
        {
            get { return IsSet(Flags, (int)UserFlags.BotAllHistory); }
        }

        public bool IsBotGroupsBlocked
        {
            get { return IsSet(Flags, (int)UserFlags.BotGroupsBlocked); }
        }

        //public string AccessToken { get; set; }
    }

    public class TLUserEmpty : TLUserBase
    {
        public const uint Signature = TLConstructors.TLUserEmpty;

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

        public override void Update(TLUserBase user)
        {
            return;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override TLInputUserBase ToInputUser()
        {
            return new TLInputUserContact { UserId = Id };
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerContact{ UserId = Id };
        }
    }

    public abstract class TLUserSelfBase : TLUserBase
    {
        public override bool HasPhone { get { return true; } }

        public override TLInputUserBase ToInputUser()
        {
            return new TLInputUserSelf();
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerSelf();
        }
    }

    public class TLUserSelf : TLUserSelfBase
    {
        public const uint Signature = TLConstructors.TLUserSelf;

        public TLBool Inactive { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            Phone = GetObject<TLString>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            _status = GetObject<TLUserStatus>(bytes, ref position);
            Inactive = GetObject<TLBool>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes(),
                Phone.ToBytes(),
                Photo.ToBytes(),
                Status.ToBytes(),
                Inactive.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);
            Phone = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            _status = GetObject<TLUserStatus>(input);
            Inactive = GetObject<TLBool>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
            output.Write(Phone.ToBytes());
            Photo.ToStream(output);
            Status.ToStream(output);
            output.Write(Inactive.ToBytes());

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override void Update(TLUserBase user)
        {
            base.Update(user);

            Inactive = ((TLUserSelf)user).Inactive;
        }
    }

    public class TLUserSelf18 : TLUserSelfBase, IUserName
    {
        public const uint Signature = TLConstructors.TLUserSelf18;

        public TLString UserName { get; set; }
        public TLBool Inactive { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            UserName = GetObject<TLString>(bytes, ref position);
            Phone = GetObject<TLString>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            _status = GetObject<TLUserStatus>(bytes, ref position);
            Inactive = GetObject<TLBool>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes(),
                UserName.ToBytes(),
                Phone.ToBytes(),
                Photo.ToBytes(),
                Status.ToBytes(),
                Inactive.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);
            UserName = GetObject<TLString>(input);
            Phone = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            _status = GetObject<TLUserStatus>(input);
            Inactive = GetObject<TLBool>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
            output.Write(UserName.ToBytes());
            output.Write(Phone.ToBytes());
            Photo.ToStream(output);
            Status.ToStream(output);
            output.Write(Inactive.ToBytes());

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override void Update(TLUserBase user)
        {
            base.Update(user);

            var user18 = user as TLUserSelf18;
            if (user18 != null)
            {
                UserName = user18.UserName;
            }
        }

        public override string ToString()
        {
            var userNameString = UserName != null ? " @" + UserName : string.Empty;
            return base.ToString() + userNameString;
        }
    }

    public class TLUserSelf24 : TLUserSelfBase, IUserName
    {
        public const uint Signature = TLConstructors.TLUserSelf24;

        public TLString UserName { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            UserName = GetObject<TLString>(bytes, ref position);
            Phone = GetObject<TLString>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            _status = GetObject<TLUserStatus>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes(),
                UserName.ToBytes(),
                Phone.ToBytes(),
                Photo.ToBytes(),
                Status.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);
            UserName = GetObject<TLString>(input);
            Phone = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            _status = GetObject<TLUserStatus>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
            output.Write(UserName.ToBytes());
            output.Write(Phone.ToBytes());
            Photo.ToStream(output);
            Status.ToStream(output);

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override void Update(TLUserBase user)
        {
            base.Update(user);

            var userName = user as IUserName;
            if (userName != null)
            {
                UserName = userName.UserName;
            }
        }

        public override string ToString()
        {
            var userNameString = UserName != null ? " @" + UserName : string.Empty;
            return base.ToString() + userNameString;
        }
    }

    public class TLUserContact18 : TLUserContact, IUserName
    {
        public new const uint Signature = TLConstructors.TLUserContact18;

        public TLString UserName { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            UserName = GetObject<TLString>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            Phone = GetObject<TLString>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            _status = GetObject<TLUserStatus>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes(),
                UserName.ToBytes(),
                AccessHash.ToBytes(),
                Phone.ToBytes(),
                Photo.ToBytes(),
                Status.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);
            UserName = GetObject<TLString>(input);
            AccessHash = GetObject<TLLong>(input);
            Phone = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            _status = GetObject<TLUserStatus>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
            output.Write(UserName.ToBytes());
            output.Write(AccessHash.ToBytes());
            output.Write(Phone.ToBytes());
            Photo.ToStream(output);
            Status.ToStream(output);

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override void Update(TLUserBase user)
        {
            base.Update(user);

            var user18 = user as TLUserContact18;
            if (user18 != null)
            {
                UserName = user18.UserName;
            }
        }

        public override string ToString()
        {
            var userNameString = !TLString.IsNullOrEmpty(UserName) ? " @" + UserName : string.Empty;
            return base.ToString() + userNameString;
        }
    }

    public class TLUserContact : TLUserBase
    {
        public const uint Signature = TLConstructors.TLUserContact;

        public TLLong AccessHash { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            Phone = GetObject<TLString>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            _status = GetObject<TLUserStatus>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes(),
                AccessHash.ToBytes(),
                Phone.ToBytes(),
                Photo.ToBytes(),
                Status.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);
            AccessHash = GetObject<TLLong>(input);
            Phone = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            _status = GetObject<TLUserStatus>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
            output.Write(AccessHash.ToBytes());
            output.Write(Phone.ToBytes());
            Photo.ToStream(output);
            Status.ToStream(output);

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override void Update(TLUserBase user)
        {
            base.Update(user);

            AccessHash = ((TLUserContact) user).AccessHash;
        }

        public override TLInputUserBase ToInputUser()
        {
            return new TLInputUserContact{ UserId = Id };
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerContact { UserId = Id };
        }
    }

    public class TLUserRequest18 : TLUserRequest, IUserName
    {
        public new const uint Signature = TLConstructors.TLUserRequest18;

        public TLString UserName { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            UserName = GetObject<TLString>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            Phone = GetObject<TLString>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            _status = GetObject<TLUserStatus>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes(),
                UserName.ToBytes(),
                AccessHash.ToBytes(),
                Phone.ToBytes(),
                Photo.ToBytes(),
                Status.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);
            UserName = GetObject<TLString>(input);
            AccessHash = GetObject<TLLong>(input);
            Phone = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            _status = GetObject<TLUserStatus>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
            output.Write(UserName.ToBytes());
            output.Write(AccessHash.ToBytes());
            output.Write(Phone.ToBytes());
            Photo.ToStream(output);
            Status.ToStream(output);

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override void Update(TLUserBase user)
        {
            base.Update(user);

            var user18 = user as TLUserRequest18;
            if (user18 != null)
            {
                UserName = user18.UserName;
            }
        }

        public override string ToString()
        {
            var userNameString = UserName != null ? " @" + UserName : string.Empty;
            return base.ToString() + userNameString;
        }
    }

    public class TLUserRequest : TLUserBase
    {
        public const uint Signature = TLConstructors.TLUserRequest;

        public TLLong AccessHash { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            
            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            Phone = GetObject<TLString>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            _status = GetObject<TLUserStatus>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes(),
                AccessHash.ToBytes(),
                Phone.ToBytes(),
                Photo.ToBytes(),
                Status.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);
            AccessHash = GetObject<TLLong>(input);
            Phone = GetObject<TLString>(input);
            _photo = GetObject<TLPhotoBase>(input);
            _status = GetObject<TLUserStatus>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
            output.Write(AccessHash.ToBytes());
            output.Write(Phone.ToBytes());
            Photo.ToStream(output);
            Status.ToStream(output);

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override TLInputUserBase ToInputUser()
        {
            return new TLInputUserForeign { UserId = Id, AccessHash = AccessHash };
        }

        public override void Update(TLUserBase user)
        {
            base.Update(user);

            AccessHash = ((TLUserRequest)user).AccessHash;
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerForeign{ UserId = Id, AccessHash = AccessHash };
        }
    }

    public class TLUserForeign18 : TLUserForeign, IUserName
    {
        public new const uint Signature = TLConstructors.TLUserForeign18;

        public TLString UserName { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            UserName = GetObject<TLString>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            _status = GetObject<TLUserStatus>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes(),
                UserName.ToBytes(),
                AccessHash.ToBytes(),
                Photo.ToBytes(),
                Status.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);
            UserName = GetObject<TLString>(input);
            AccessHash = GetObject<TLLong>(input);
            _photo = GetObject<TLPhotoBase>(input);
            _status = GetObject<TLUserStatus>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
            output.Write(UserName.ToBytes());
            output.Write(AccessHash.ToBytes());
            Photo.ToStream(output);
            Status.ToStream(output);

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override void Update(TLUserBase user)
        {
            base.Update(user);

            var user18 = user as TLUserForeign18;
            if (user18 != null)
            {
                UserName = user18.UserName;
            }
        }

        public override string ToString()
        {
            var userNameString = UserName != null ? " @" + UserName : string.Empty;
            return base.ToString() + userNameString;
        }
    }

    public class TLUserForeign : TLUserBase
    {
        public const uint Signature = TLConstructors.TLUserForeign;

        public TLLong AccessHash { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            _photo = GetObject<TLPhotoBase>(bytes, ref position);
            _status = GetObject<TLUserStatus>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes(),
                AccessHash.ToBytes(),
                Photo.ToBytes(),
                Status.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);
            AccessHash = GetObject<TLLong>(input);
            _photo = GetObject<TLPhotoBase>(input);
            _status = GetObject<TLUserStatus>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
            output.Write(AccessHash.ToBytes());
            Photo.ToStream(output);
            Status.ToStream(output);

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override TLInputUserBase ToInputUser()
        {
            return new TLInputUserForeign { UserId = Id, AccessHash = AccessHash };
        }

        public override void Update(TLUserBase user)
        {
            base.Update(user);

            AccessHash = ((TLUserForeign)user).AccessHash;
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerForeign{ UserId = Id, AccessHash = AccessHash };
        }
    }

    public class TLUserDeleted18 : TLUserDeleted, IUserName
    {
        public new const uint Signature = TLConstructors.TLUserDeleted18;

        public TLString UserName { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            UserName = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes(),
                UserName.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);
            UserName = GetObject<TLString>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());
            output.Write(UserName.ToBytes());

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override void Update(TLUserBase user)
        {
            base.Update(user);

            var user18 = user as TLUserDeleted18;
            if (user18 != null)
            {
                UserName = user18.UserName;
            }
        }

        public override string ToString()
        {
            var userNameString = UserName != null ? " @" + UserName : string.Empty;
            return base.ToString() + userNameString;
        }
    }

    public class TLUserDeleted : TLUserBase
    {
        public const uint Signature = TLConstructors.TLUserDeleted;
         
        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            _firstName = GetObject<TLString>(bytes, ref position);
            _lastName = GetObject<TLString>(bytes, ref position);
            
            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                FirstName.ToBytes(),
                LastName.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            _firstName = GetObject<TLString>(input);
            _lastName = GetObject<TLString>(input);

            NotifySettings = GetObject<TLObject>(input) as TLPeerNotifySettingsBase;
            ExtendedInfo = GetObject<TLObject>(input) as TLUserExtendedInfo;
            Contact = GetObject<TLObject>(input) as TLContact;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
            output.Write(FirstName.ToBytes());
            output.Write(LastName.ToBytes());

            NotifySettings.NullableToStream(output);
            ExtendedInfo.NullableToStream(output);
            Contact.NullableToStream(output);
        }

        public override TLInputUserBase ToInputUser()
        {
            return new TLInputUserContact{ UserId = Id };
        }

        public override TLInputPeerBase ToInputPeer()
        {
            return new TLInputPeerContact{ UserId = Id };
        }
    }
}
