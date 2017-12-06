using System.Numerics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.Native.TL;

namespace Telegram.Api
{
    public static class TLExtensions
    {
        //#region Generic
        //public static object FromBytes<T>(this object value, byte[] bytes, ref int position)
        //{
        //    if (typeof(T) == typeof(long?))
        //    {
        //        return ((long?)value).FromBytes(bytes, ref position);
        //    }
        //    if (typeof(T) == typeof(int?))
        //    {
        //        return ((int?)value).FromBytes(bytes, ref position);
        //    }

        //    return ((TLObject)value).FromBytes(bytes, ref position);
        //}

        //public static byte[] ToBytes(this object value)
        //{
        //    if (value is long?)
        //    {
        //        return BitConverter.GetBytes((long?)value ?? 0);
        //    }
        //    if (value is int?)
        //    {
        //        return BitConverter.GetBytes((int?)value ?? 0);
        //    }

        //    return ((TLObject)value).ToBytes();
        //}

        //public static object FromStream<T>(this object value, Stream input)
        //{
        //    if (typeof(T) == typeof(long?))
        //    {
        //        var buffer = new byte[8];
        //        input.Read(buffer, 0, 8);
        //        value = BitConverter.ToInt64(buffer, 0);

        //        return value;
        //    }
        //    if (typeof(T) == typeof(int?))
        //    {
        //        var buffer = new byte[4];
        //        input.Read(buffer, 0, 4);
        //        value = BitConverter.ToInt32(buffer, 0);

        //        return value;
        //    }

        //    return ((TLObject)value).FromStream(input);
        //}

        //public static void ToStream(this object value, Stream output)
        //{
        //    if (value is long?)
        //    {
        //        output.Write(BitConverter.GetBytes((long?)value ?? 0), 0, 8);
        //        return;
        //    }
        //    else if (value is int?)
        //    {
        //        output.Write(BitConverter.GetBytes((int?)value ?? 0), 0, 4);
        //        return;
        //    }

        //    ((TLObject)value).ToStream(output);
        //}
        //#endregion

        public static byte[] ToBytes(this byte[] data)
        {
            int num = (data.Length >= 254) ? (4 + data.Length) : (1 + data.Length);
            int num2 = (num % 4 == 0) ? 0 : (4 - num % 4);
            num += num2;
            byte[] array = new byte[num];
            if (data.Length >= 254)
            {
                array[0] = 254;
                byte[] bytes = BitConverter.GetBytes(data.Length);
                Array.Copy(bytes, 0, array, 1, 3);
                Array.Copy(data, 0, array, 4, data.Length);
            }
            else
            {
                array[0] = (byte)data.Length;
                Array.Copy(data, 0, array, 1, data.Length);
            }
            return array;
        }

        public static BigInteger ToBigInteger(this byte[] value)
        {
            var data = new List<byte>(value);
            while (data[0] == 0x00)
            {
                data.RemoveAt(0);
            }

            return new BigInteger(value.Reverse().Concat(new byte[] { 0x00 }).ToArray());  //NOTE: add reverse here
        }

        public static bool CodeEquals(this TLRPCError error, TLErrorCode code)
        {
            if (error == null)
            {
                return false;
            }

            if (Enum.IsDefined(typeof(TLErrorCode), error.ErrorCode))
            {
                return (TLErrorCode)error.ErrorCode == code;
            }

            return false;
        }

        public static bool TypeEquals(this TLRPCError error, TLErrorType type)
        {
            if (error == null || error.ErrorMessage == null)
            {
                return false;
            }

            var strings = error.ErrorMessage.Split(':');
            var typeString = strings[0];
            if (Enum.IsDefined(typeof(TLErrorType), typeString))
            {
                var value = (TLErrorType)Enum.Parse(typeof(TLErrorType), typeString, true);

                return value == type;
            }

            return false;
        }

        public static bool TypeStarsWith(this TLRPCError error, TLErrorType type)
        {
            var strings = error.ErrorMessage.Split(':');
            var typeString = strings[0];

            return typeString.StartsWith(type.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public static string GetErrorTypeString(this TLRPCError error)
        {
            var strings = error.ErrorMessage.Split(':');
            return strings[0];
        }

        public static byte[] FromUInt64(this UInt64 data)
        {
            return RemoveFirstZeroBytes(BitConverter.GetBytes(data).Reverse().ToArray());
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

        public static TLInputChannel ToInputChannel(this TLChannel channel)
        {
            return new TLInputChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash.Value };
        }

        public static TLInputChannel ToInputChannel(this TLChannelForbidden channel)
        {
            return new TLInputChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash };
        }
    }
}
