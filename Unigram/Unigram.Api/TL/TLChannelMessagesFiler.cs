using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLChannelMessagesFilerBase : TLObject { }

    public class TLChannelMessagesFilterEmpty : TLChannelMessagesFilerBase
    {
        public const uint Signature = TLConstructors.TLChannelMessagesFilterEmpty;

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

    public class TLChannelMessagesFilter : TLChannelMessagesFilerBase
    {
        public const uint Signature = TLConstructors.TLChannelMessagesFilter;

        public TLInt Flags { get; set; }

        public TLVector<TLMessageRange> Ranges { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Ranges.ToBytes()
            );
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Flags.ToStream(output);
            Ranges.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Ranges = GetObject<TLVector<TLMessageRange>>(input);

            return this;
        }
    }

    public class TLChannelMessagesFilterCollapsed : TLChannelMessagesFilerBase
    {
        public const uint Signature = TLConstructors.TLChannelMessagesFilterCollapsed;

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
}
