using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLInputUserBase : TLObject { }

    public class TLInputUserEmpty : TLInputUserBase
    {
        public const uint Signature = TLConstructors.TLInputUserEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLInputUserSelf : TLInputUserBase
    {
        public const uint Signature = TLConstructors.TLInputUserSelf;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLInputUserContact : TLInputUserBase
    {
        public const uint Signature = TLConstructors.TLInputUserContact;

        public TLInt UserId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(TLInputUser.Signature),
                UserId.ToBytes(),
                new TLLong(0).ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            UserId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLInputUserForeign : TLInputUserBase
    {
        public const uint Signature = TLConstructors.TLInputUserForeign;

        public TLInt UserId { get; set; }

        public TLLong AccessHash { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(TLInputUser.Signature),
                UserId.ToBytes(),
                AccessHash.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            UserId.ToStream(output);
            AccessHash.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            AccessHash = GetObject<TLLong>(input);

            return this;
        }
    }

    public class TLInputUser : TLInputPeerBase
    {
        public const uint Signature = TLConstructors.TLInputUser;

        public TLInt UserId { get; set; }

        public TLLong AccessHash { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                UserId.ToBytes(),
                AccessHash.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            UserId.ToStream(output);
            AccessHash.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            AccessHash = GetObject<TLLong>(input);

            return this;
        }

        public override string ToString()
        {
            return "UserId " + UserId + " AccessHash " + AccessHash;
        }
    }
}
