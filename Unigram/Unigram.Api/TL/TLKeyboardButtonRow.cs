using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLKeyboardButtonRow : TLObject
    {
        public const uint Signature = TLConstructors.TLKeyboardButtonRow;

        public TLVector<TLKeyboardButton> Buttons { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Buttons = GetObject<TLVector<TLKeyboardButton>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Buttons.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Buttons = GetObject<TLVector<TLKeyboardButton>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Buttons.ToBytes());
        }
    }
}
