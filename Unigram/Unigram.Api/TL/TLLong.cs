using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;

namespace Telegram.Api.TL
{
    [DataContract]
    public class TLLong : TLObject
    {
        [DataMember]
        public Int64 Value { get; set; }

        public TLLong()
        {
            
        }

        public TLLong(long value)
        {
            Value = value;
        }

        private static readonly object _randomSyncRoot = new object();

        private static readonly Random _random = new Random();

        public static TLLong Random()
        {
            var randomNumber = new byte[8];

            lock (_randomSyncRoot)
            {
                var random = _random;
                random.NextBytes(randomNumber);
            }

            return new TLLong { Value = BitConverter.ToInt64(randomNumber, 0) };
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            Value = BitConverter.ToInt64(bytes, position);
            position += 8;

            return this;
        }

        public override byte[] ToBytes()
        {
            return BitConverter.GetBytes(Value);
        }

        public override TLObject FromStream(Stream input)
        {
            var buffer = new byte[8];
            input.Read(buffer, 0, 8);
            Value = BitConverter.ToInt64(buffer, 0);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(BitConverter.GetBytes(Value), 0, 8);
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);// + " " + TLUtils.MessageIdString(this);
        }
    }
}
