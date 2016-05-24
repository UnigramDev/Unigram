using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLLinkBase : TLObject
    {
        public TLUserBase User { get; set; }
    }

    public class TLLink : TLLinkBase
    {
        public const uint Signature = TLConstructors.TLLink;

        public TLMyLinkBase MyLink { get; set; }
        public TLForeignLinkBase ForeignLink { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            MyLink = GetObject<TLMyLinkBase>(bytes, ref position);
            ForeignLink = GetObject<TLForeignLinkBase>(bytes, ref position);
            User = GetObject<TLUserBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            MyLink = GetObject<TLMyLinkBase>(input);
            ForeignLink = GetObject<TLForeignLinkBase>(input);
            User = GetObject<TLUserBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            MyLink.ToStream(output);
            ForeignLink.ToStream(output);
            User.ToStream(output);
        }
    }

    public class TLLink24 : TLLinkBase
    {
        public const uint Signature = TLConstructors.TLLink24;

        public TLContactLinkBase MyLink { get; set; }
        public TLContactLinkBase ForeignLink { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            MyLink = GetObject<TLContactLinkBase>(bytes, ref position);
            ForeignLink = GetObject<TLContactLinkBase>(bytes, ref position);
            User = GetObject<TLUserBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            MyLink = GetObject<TLContactLinkBase>(input);
            ForeignLink = GetObject<TLContactLinkBase>(input);
            User = GetObject<TLUserBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            MyLink.ToStream(output);
            ForeignLink.ToStream(output);
            User.ToStream(output);
        }
    }
}
