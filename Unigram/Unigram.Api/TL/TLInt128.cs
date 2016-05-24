using Org.BouncyCastle.Security;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public class TLInt128 : TLObject
    {
        public byte[] Value { get; set; }

        public override byte[] ToBytes()
        {
            return Value;
        }

        public static TLInt128 Random()
        {
            var randomNumber = new byte[16];
            var random = new SecureRandom();
            random.NextBytes(randomNumber);
            return new TLInt128{ Value = randomNumber };
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            Value = bytes.SubArray(position, 16);
            position += 16;

            return this;
        }
    }
}
