using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Text;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    [DataContract]
    public class TLString : TLObject
    {
        public TLString(){ }

        public TLString(string s)
        {
            if (s == null) s = "";
            var data = Encoding.UTF8.GetBytes(s);
            //Length = data.Length;
            Data = data;                  // NOTE: remove Reverse here
        }

        public static TLString Empty
        {
            get
            {
                return new TLString("");
            }
        }

        //little endian data with leading 0x00 padding
        [DataMember]
        public byte[] Data { get; set; }            // NOT: now its big endian with 0x0 padding at the end

        public static TLString FromBigEndianData(byte[] data)
        {
            var str = new TLString();
            str.Data = data;                        // NOTE: remove Reverse here
            return str;
        }

        // UInt64 - little endian 
        public static TLString FromUInt64(UInt64 data)
        {
            var str = new TLString();

            // revert to big endian and remove first zero bytes
            var bigEndianBytes = RemoveFirstZeroBytes(BitConverter.GetBytes(data).Reverse().ToArray());

            // revert to little endian again
            str.Data = bigEndianBytes;              // NOTE: remove Reverse here

            return str;
        }

        private static byte[] RemoveFirstZeroBytes(IList<byte> bytes)
        {
            var result = new List<byte>(bytes);

            while (result.Count > 0 && result[0] == 0x00)
            {
                result.RemoveAt(0);
            }

            return result.ToArray();
        }

        public BigInteger ToBigInteger()
        {
            var data = new List<byte>(Data);
            while (data[0] == 0x00)
            {
                data.RemoveAt(0);
            }

            return new BigInteger(Data.Reverse().Concat(new byte[] {0x00}).ToArray());  //NOTE: add reverse here
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            int bytesRead;
            int length;
            if (bytes[position] == 0xfe)
            {
                var lengthBytes = bytes.SubArray(position + 1, 3);
                length = BitConverter.ToInt32(lengthBytes.Concat(new byte[] { 0x00 }).ToArray(), 0);
                bytesRead = 4;
            }
            else
            {
                length = bytes[position];
                bytesRead = 1;
            }
            Data = bytes.SubArray(position + bytesRead, length);            //NOTE: remove Reverse here

            bytesRead += Data.Length;
            var padding = (bytesRead % 4 == 0) ? 0 : (4 - (bytesRead % 4));
            bytesRead += padding;
            if (bytesRead % 4 != 0) throw new Exception("Length must be divisible on 4");

            position += bytesRead;

            return this;
        }

        // length + big endian data + zero bytes
        public override byte[] ToBytes()
        {
            var length = (Data.Length >= 254) ? 4 + Data.Length : 1 + Data.Length;
            var padding = (length % 4 == 0)? 0 : (4 - (length % 4));
            length = length + padding;
            var bytes = new byte[length];

            if (Data.Length >= 254)
            {
                bytes[0] = 0xFE;
                var lengthBytes = BitConverter.GetBytes(Data.Length);
                Array.Copy(lengthBytes, 0, bytes, 1, 3);
                Array.Copy(Data, 0, bytes, 4, Data.Length);                         //NOTE: Remove Reverse here
            }
            else
            {
                bytes[0] = (byte)Data.Length;
                Array.Copy(Data, 0, bytes, 1, Data.Length);                         //NOTE: Remove Reverse here
            }          

            return bytes;
        }

        public override TLObject FromStream(Stream input)
        {
            int bytesRead;
            int length;
            var bytes = new byte[1];
            input.Read(bytes, 0, 1);
            if (bytes[0] == 0xfe)
            {
                var lengthBytes = new byte[3];
                input.Read(lengthBytes, 0, 3);
                length = BitConverter.ToInt32(lengthBytes.Concat(new byte[] { 0x00 }).ToArray(), 0);
                bytesRead = 4;
            }
            else
            {
                length = bytes[0];
                bytesRead = 1;
            }
            Data = new byte[length];
            input.Read(Data, 0, Data.Length);
            //    bytes.SubArray(position + bytesRead, length);            //NOTE: remove Reverse here

            bytesRead += Data.Length;
            var padding = (bytesRead % 4 == 0) ? 0 : (4 - (bytesRead % 4));
            bytesRead += padding;
            if (bytesRead % 4 != 0) throw new Exception("Length must be divisible on 4");

            input.Position += padding;

            return this;
        }

        public override void ToStream(Stream output)
        {
            var buffer = ToBytes();
            output.Write(buffer, 0, buffer.Length);
        }

        public override string ToString()
        {
#if SILVERLIGHT || WIN_RT
            var bigEndianData = Data;

            if (bigEndianData == null) return string.Empty;

            return Encoding.UTF8.GetString(bigEndianData, 0, bigEndianData.Length);
#else
            return Encoding.UTF8.GetString(Data);
#endif
        }

        public string Value
        {
            get { return ToString(); }
        }

        public static bool IsNullOrEmpty(TLString str)
        {
            return str == null || string.IsNullOrEmpty(str.ToString());
        }

        public static bool Equals(TLString str1, TLString str2, StringComparison comparison)
        {
            if (str1 == null && str2 == null)
            {
                return true;
            }

            if (str1 != null && str2 == null)
            {
                return false;
            }

            if (str1 == null)
            {
                return false;
            }

            return string.Equals(str1.ToString(), str2.ToString(), comparison);
        }
    }
}
