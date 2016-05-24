using System;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public class TLInt256 : TLObject
    {
        public byte[] Value { get; set; }

        public override byte[] ToBytes()
        {
            return Value;
        }

        public static TLInt256 Random()
        {
            var randomNumber = new byte[32];
            var random = new Random();
            random.NextBytes(randomNumber);
            return new TLInt256 { Value = randomNumber };
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            Value = bytes.SubArray(position, 32);
            position += 32;

            return this;
        }
    }
}
