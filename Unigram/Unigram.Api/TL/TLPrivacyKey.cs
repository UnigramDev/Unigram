using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLPrivacyKeyBase : TLObject { }

    public class TLPrivacyKeyStatusTimestamp : TLPrivacyKeyBase
    {
        public const uint Signature = TLConstructors.TLPrivacyKeyStatusTimestamp;

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
    }
}
