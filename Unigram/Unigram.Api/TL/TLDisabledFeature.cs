using System.Runtime.Serialization;

namespace Telegram.Api.TL
{
    [DataContract]
    public class TLDisabledFeature : TLObject
    {
        public const uint Signature = TLConstructors.TLDisabledFeature;

        [DataMember]
        public TLString Feature { get; set; }

        [DataMember]
        public TLString Description { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Feature = GetObject<TLString>(bytes, ref position);
            Description = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", Feature, Description);
        }
    }
}
