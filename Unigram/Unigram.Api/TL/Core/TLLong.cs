using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Linq;

namespace Telegram.Api.TL
{
    public static class TLLong
    {
        private static readonly object _randomSyncRoot = new object();
        private static readonly Random _random = new Random();

        public static long Random()
        {
            var randomNumber = new byte[8];

            lock (_randomSyncRoot)
            {
                var random = _random;
                random.NextBytes(randomNumber);
            }

            return BitConverter.ToInt64(randomNumber, 0);
        }

        public static long[] Random(int count)
        {
            var ids = new long[count];

            for (int i = 0; i < count; i++)
            {
                ids[i] = Random();
            }

            return ids.OrderBy(x => x).ToArray();
        }

        #region TLLong
        public static object FromBytes(this long? value, byte[] bytes, ref int position)
        {
            value = BitConverter.ToInt64(bytes, position);
            position += 8;

            return value;
        }

        public static byte[] ToBytes(this long? value)
        {
            return BitConverter.GetBytes(value ?? 0);
        }

        public static object FromStream(this long? value, Stream input)
        {
            var buffer = new byte[8];
            input.Read(buffer, 0, 8);
            value = BitConverter.ToInt64(buffer, 0);

            return value;
        }

        public static void ToStream(this long? value, Stream output)
        {
            output.Write(BitConverter.GetBytes(value.Value), 0, 8);
        }
        #endregion
    }
}
