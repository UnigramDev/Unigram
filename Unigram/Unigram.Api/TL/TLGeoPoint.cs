using System.IO;
using System.Runtime.Serialization;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [KnownType(typeof(TLGeoPointEmpty))]
    [KnownType(typeof(TLGeoPoint))]
    [DataContract]
    public abstract class TLGeoPointBase : TLObject { }

    [DataContract]
    public class TLGeoPointEmpty : TLGeoPointBase
    {
        public const uint Signature = TLConstructors.TLGeoPointEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
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

    [DataContract]
    public class TLGeoPoint : TLGeoPointBase
    {
        public const uint Signature = TLConstructors.TLGeoPoint;

        [DataMember]
        public TLDouble Long { get; set; }

        [DataMember]
        public TLDouble Lat { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Long = GetObject<TLDouble>(bytes, ref position);
            Lat = GetObject<TLDouble>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Long = GetObject<TLDouble>(input);
            Lat = GetObject<TLDouble>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Long.ToBytes());
            output.Write(Lat.ToBytes());
        }
    }
}
