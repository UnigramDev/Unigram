using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLActionInfo : TLObject
    {
        public const uint Signature = TLConstructors.TLActionInfo;

        public TLInt SendBefore { get; set; }

        public TLObject Action { get; set; }

        public override string ToString()
        {
            return string.Format("send_before={0} action={1}", SendBefore, Action);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            SendBefore = GetObject<TLInt>(input);
            Action = GetObject<TLObject>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            SendBefore.ToStream(output);
            Action.ToStream(output);
        }
    }
}
