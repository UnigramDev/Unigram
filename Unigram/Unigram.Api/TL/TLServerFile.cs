using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLServerFile : TLObject
    {
        public const uint Signature = TLConstructors.TLServerFile;

        public TLLong MD5Checksum { get; set; }

        public TLInputMediaBase Media { get; set; }

        public override TLObject FromStream(Stream input)
        {
            MD5Checksum = GetObject<TLLong>(input);
            Media = GetObject<TLInputMediaBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            MD5Checksum.ToStream(output);
            Media.ToStream(output);
        }
    }
}
