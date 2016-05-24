using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLBotCommand : TLObject
    {
        public const uint Signature = TLConstructors.TLBotCommand;

        public TLString Command { get; set; }

        public TLString Description { get; set; }


        public TLUserBase Bot { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Command = GetObject<TLString>(bytes, ref position);
            Description = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Command.ToBytes(),
                Description.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Command = GetObject<TLString>(input);
            Description = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Command.ToBytes());
            output.Write(Description.ToBytes());
        }
    }
}
