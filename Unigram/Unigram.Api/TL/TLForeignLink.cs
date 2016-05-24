using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLForeignLinkBase : TLObject { }

    public class TLForeignLinkUnknown : TLForeignLinkBase
    {
        public const uint Signature = TLConstructors.TLForeignLinkUnknown;

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
    }

    public class TLForeignLinkRequested : TLForeignLinkBase
    {
        public const uint Signature = TLConstructors.TLForeignLinkRequested;

        public TLBool HasPhone { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            HasPhone = GetObject<TLBool>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            HasPhone = GetObject<TLBool>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(HasPhone.ToBytes());
        }
    }

    public class TLForeignLinkMutual : TLForeignLinkBase
    {
        public const uint Signature = TLConstructors.TLForeignLinkMutual;

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
    }
}
