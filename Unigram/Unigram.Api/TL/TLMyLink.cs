using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLMyLinkBase : TLObject { }

    public class TLMyLinkEmpty : TLMyLinkBase
    {
        public const uint Signature = TLConstructors.TLMyLinkEmpty;

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

    public class TLMyLinkRequested : TLMyLinkBase
    {
        public const uint Signature = TLConstructors.TLMyLinkRequested;

        public TLBool Contact { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Contact = GetObject<TLBool>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Contact = GetObject<TLBool>(input);

            return this;
        }
        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Contact.ToStream(output);
        }
    }

    public class TLMyLinkContact : TLMyLinkBase
    {
        public const uint Signature = TLConstructors.TLMyLinkContact;

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
