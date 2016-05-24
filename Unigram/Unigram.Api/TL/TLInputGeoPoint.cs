using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLInputGeoPointBase : TLObject { }

    public class TLInputGeoPointEmpty : TLInputGeoPointBase
    {
        public const uint Signature = TLConstructors.TLInputGeoPointEmpty;

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
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

    public class TLInputGeoPoint : TLInputGeoPointBase
    {
        public const uint Signature = TLConstructors.TLInputGeoPoint;

        public TLDouble Lat { get; set; }
        public TLDouble Long { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Lat.ToBytes(),
                Long.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Lat = GetObject<TLDouble>(input);
            Long = GetObject<TLDouble>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Lat.ToStream(output);
            Long.ToStream(output);
        }
    }
}
