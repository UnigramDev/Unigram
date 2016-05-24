using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLKeyboardButton : TLObject
    {
        public const uint Signature = TLConstructors.TLKeyboardButton;

        public TLString Text { get; set; }

        public TLKeyboardButton() { }

        public TLKeyboardButton(TLString text)
        {
            Text = text;
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Text = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Text.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Text = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Text.ToBytes());
        }
    }
}
