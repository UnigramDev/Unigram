using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;

namespace Telegram.Api.TL
{
    [DataContract]
    public class TLInt : TLObject
    {
        [DataMember]
        public int Value { get; set; }

        public TLInt() { }

        public TLInt(int value)
        {
            Value = value;
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            Value = BitConverter.ToInt32(bytes, position);
            position += 4;

            return this;
        }

        public override byte[] ToBytes()
        {
            return BitConverter.GetBytes(Value);
        }

        public override TLObject FromStream(Stream input)
        {
            var buffer = new byte[4];
            input.Read(buffer, 0, 4);
            Value = BitConverter.ToInt32(buffer, 0);
            
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(BitConverter.GetBytes(Value), 0, 4);
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }

        private static readonly Random _random = new Random();

        public static TLInt Random()
        {
            var randomNumber = new byte[4];
            var random = _random;
            random.NextBytes(randomNumber);
            return new TLInt { Value = BitConverter.ToInt32(randomNumber, 0) };
        }
        
    }
}
