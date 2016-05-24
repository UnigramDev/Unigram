using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;

namespace Telegram.Api.TL
{
    [DataContract]
    public class TLBool : TLObject
    {
        public const uint BoolTrue = 0x997275b5;
        public const uint BoolFalse = 0xbc799737;

        [DataMember]
        public bool Value { get; set; }

        public TLBool()
        {
            
        }

        public TLBool(bool value)
        {
            Value = value;
        }

        public static TLBool True
        {
            get { return new TLBool(true); }
        }

        public static TLBool False
        {
            get { return new TLBool(false); }
        }

        public static TLBool Parse(byte[] bytes, out int bytesRead)
        {
            bytesRead = 4;
            if (bytes.StartsWith(BoolTrue))
            {
                return new TLBool{ Value = true };
            }
            if (bytes.StartsWith(BoolFalse))
            {
                return new TLBool { Value = false };
            }

            bytesRead = 0;
            bytes.ThrowNotSupportedException("TLBool");
            return null;
        }

        public override byte[] ToBytes()
        {
            return Value ? TLUtils.SignatureToBytes(BoolTrue) : TLUtils.SignatureToBytes(BoolFalse);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            if (bytes.StartsWith(position, BoolTrue))
            {
                Value = true;
            }
            else if (bytes.StartsWith(position, BoolFalse))
            {
                Value = false;
            }
            else
            {
                bytes.ThrowNotSupportedException("TLBool");
            }
            position += 4;

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            var buffer = new byte[4];
            input.Read(buffer, 0, 4);
            if (buffer.StartsWith(0, BoolTrue))
            {
                Value = true;
            }
            else if (buffer.StartsWith(0, BoolFalse))
            {
                Value = false;
            }
            else
            {
                buffer.ThrowNotSupportedException("TLBool");
            }

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(Value ? BitConverter.GetBytes(BoolTrue) : BitConverter.GetBytes(BoolFalse), 0, 4);
        }

        public override string ToString()
        {
#if WIN_RT
            return Value.ToString();
#else
            return Value.ToString(CultureInfo.InvariantCulture);
#endif
        }
    }
}
