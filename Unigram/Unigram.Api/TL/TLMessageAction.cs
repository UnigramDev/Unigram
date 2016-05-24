using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLMessageActionBase : TLObject
    {
        public abstract void Update(TLMessageActionBase newAction);

        public TLPhotoBase Photo { get; set; }
    }

    public class TLMessageActionEmpty : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override void Update(TLMessageActionBase newAction)
        {
        }
    }

    public class TLMessageActionChatCreate : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChatCreate;

        public TLString Title { get; set; }

        public TLVector<TLInt> Users { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Title = GetObject<TLString>(bytes, ref position);
            Users = GetObject<TLVector<TLInt>>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Title = GetObject<TLString>(input);
            Users = GetObject<TLVector<TLInt>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Title.ToStream(output);
            Users.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
            var action = newAction as TLMessageActionChatCreate;
            if (action != null)
            {
                Title = action.Title;
                Users = action.Users;
            }
        }
    }

    public class TLMessageActionChannelCreate : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChannelCreate;

        public TLString Title { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Title = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Title = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Title.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
            var action = newAction as TLMessageActionChannelCreate;
            if (action != null)
            {
                Title = action.Title;
            }
        }
    }

    public class TLMessageActionToggleComments : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionToggleComments;

        public TLBool Enabled { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Enabled = GetObject<TLBool>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Enabled = GetObject<TLBool>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Enabled.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
            var action = newAction as TLMessageActionToggleComments;
            if (action != null)
            {
                Enabled = action.Enabled;
            }
        }
    }

    public class TLMessageActionChatEditTitle : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChatEditTitle;

        public TLString Title { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Title = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Title = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Title.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
            var action = newAction as TLMessageActionChatEditTitle;
            if (action != null)
            {
                Title = action.Title;
            }
        }
    }

    public class TLMessageActionChatEditPhoto : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChatEditPhoto;

        //public TLPhotoBase Photo { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Photo = GetObject<TLPhotoBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Photo = GetObject<TLPhotoBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Photo.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
            var action = newAction as TLMessageActionChatEditPhoto;
            if (action != null)
            {
                if (Photo != null)
                {
                    Photo.Update(action.Photo);
                }
                else
                {
                    Photo = action.Photo;
                }
            }
        }
    }

    public class TLMessageActionChatDeletePhoto : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChatDeletePhoto;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override void Update(TLMessageActionBase newAction)
        {
            
        }
    }

    public class TLMessageActionChannelJoined : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChannelJoined;

        public TLInt InviterId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            InviterId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            InviterId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            InviterId.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
            var action = newAction as TLMessageActionChannelJoined;
            if (action != null)
            {
                InviterId = action.InviterId;
            }
        }
    }

    public class TLMessageActionChatAddUser : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChatAddUser;

        public TLInt UserId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
            var action = newAction as TLMessageActionChatAddUser;
            if (action != null)
            {
                UserId = action.UserId;
            }
        }
    }

    public class TLMessageActionChatDeleteUser : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChatDeleteUser;

        public TLInt UserId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
            var action = newAction as TLMessageActionChatDeleteUser;
            if (action != null)
            {
                UserId = action.UserId;
            }
        }
    }

    public class TLMessageActionChatJoinedByLink : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChatJoinedByLink;

        public TLInt InviterId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            InviterId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            InviterId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            InviterId.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
        }
    }

    public class TLMessageActionUnreadMessages : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionUnreadMessages;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override void Update(TLMessageActionBase newAction)
        {
            
        }
    }

    public class TLMessageActionContactRegistered : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionContactRegistered;

        public TLInt UserId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
        }
    }

    public class TLMessageActionMessageGroup : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionMessageGroup;

        public TLMessageGroup Group { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Group = GetObject<TLMessageGroup>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Group = GetObject<TLMessageGroup>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Group.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
        }
    }

    public class TLMessageActionChatMigrateTo : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChatMigrateTo;

        public TLInt ChannelId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChannelId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            ChannelId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChannelId.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
        }
    }

    public class TLMessageActionChatDeactivate : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChatDeactivate;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override void Update(TLMessageActionBase newAction)
        {
        }
    }

    public class TLMessageActionChatActivate : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChatActivate;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override void Update(TLMessageActionBase newAction)
        {
        }
    }

    public class TLMessageActionChannelMigrateFrom : TLMessageActionBase
    {
        public const uint Signature = TLConstructors.TLMessageActionChannelMigrateFrom;

        public TLString Title { get; set; }

        public TLInt ChatId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Title = GetObject<TLString>(bytes, ref position);
            ChatId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Title = GetObject<TLString>(input);
            ChatId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Title.ToStream(output);
            ChatId.ToStream(output);
        }

        public override void Update(TLMessageActionBase newAction)
        {
        }
    }
}
