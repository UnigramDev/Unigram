using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLPrivacyRuleBase : TLObject
    {
        public string Label { get; set; }

        public override string ToString()
        {
            return Label;
        }

        public abstract TLInputPrivacyRuleBase ToInputRule();
    }

    public class TLPrivacyValueAllowContacts : TLPrivacyRuleBase
    {
        public const uint Signature = TLConstructors.TLPrivacyValueAllowContacts;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override TLInputPrivacyRuleBase ToInputRule()
        {
            return new TLInputPrivacyValueAllowContacts();
        }
    }

    public class TLPrivacyValueAllowAll : TLPrivacyRuleBase
    {
        public const uint Signature = TLConstructors.TLPrivacyValueAllowAll;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override TLInputPrivacyRuleBase ToInputRule()
        {
            return new TLInputPrivacyValueAllowAll();
        }
    }

    public class TLPrivacyValueAllowUsers : TLPrivacyRuleBase, IPrivacyValueUsersRule
    {
        public const uint Signature = TLConstructors.TLPrivacyValueAllowUsers;

        public TLVector<TLInt> Users { get; set; } 

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Users = GetObject<TLVector<TLInt>>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Users.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Users = GetObject<TLVector<TLInt>>(input);

            return this;
        }

        public override TLInputPrivacyRuleBase ToInputRule()
        {
            return new TLInputPrivacyValueAllowUsers{Users = new TLVector<TLInputUserBase>()};
        }
    }

    public class TLPrivacyValueDisallowContacts : TLPrivacyRuleBase
    {
        public const uint Signature = TLConstructors.TLPrivacyValueDisallowContacts;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override TLInputPrivacyRuleBase ToInputRule()
        {
            return new TLInputPrivacyValueDisallowContacts();
        }
    }

    public class TLPrivacyValueDisallowAll : TLPrivacyRuleBase
    {
        public const uint Signature = TLConstructors.TLPrivacyValueDisallowAll;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override TLInputPrivacyRuleBase ToInputRule()
        {
            return new TLInputPrivacyValueDisallowAll();
        }
    }

    public class TLPrivacyValueDisallowUsers : TLPrivacyRuleBase, IPrivacyValueUsersRule
    {
        public const uint Signature = TLConstructors.TLPrivacyValueDisallowUsers;

        public TLVector<TLInt> Users { get; set; } 

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Users = GetObject<TLVector<TLInt>>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Users.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Users = GetObject<TLVector<TLInt>>(input);

            return this;
        }

        public override TLInputPrivacyRuleBase ToInputRule()
        {
            return new TLInputPrivacyValueDisallowUsers{Users = new TLVector<TLInputUserBase>()};
        }
    }

    public interface IPrivacyValueUsersRule
    {
        TLVector<TLInt> Users { get; set; }
    }
}
