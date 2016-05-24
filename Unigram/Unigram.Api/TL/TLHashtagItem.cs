using System.IO;
using System.Linq;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLHashtagItem : TLObject
    {
        public const uint Signature = TLConstructors.TLHashtagItem;

        public TLString Hashtag { get; set; }

        public TLHashtagItem()
        {
            
        }

        public TLHashtagItem(string hashtag)
        {
            Hashtag = new TLString(hashtag);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Hashtag = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature)
                .Concat(Hashtag.ToBytes())
                .ToArray();
        }

        public override TLObject FromStream(Stream input)
        {
            Hashtag = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Hashtag.ToBytes());
        }

        public override string ToString()
        {
            return Hashtag != null ? Hashtag.ToString() : string.Empty;
        }
    }
}
