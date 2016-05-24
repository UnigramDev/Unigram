using System.IO;
using System.Runtime.Serialization;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [DataContract]
    public class TLContact : TLObject
    {
        public const uint Signature = TLConstructors.TLContact;

        [DataMember]
        public TLInt UserId { get; set; }

        [DataMember]
        public TLBool Mutual { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            Mutual = GetObject<TLBool>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            Mutual = GetObject<TLBool>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(UserId.ToBytes());
            output.Write(Mutual.ToBytes());
        }
    }
}
