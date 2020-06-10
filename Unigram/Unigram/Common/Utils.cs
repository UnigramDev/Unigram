using System;
using System.Runtime.CompilerServices;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;

namespace Unigram.Common
{
    public static class Utils
    {
        public static DateTime UnixTimestampToDateTime(double unixTimeStamp)
        {
            // From UTC0 UnixTime to local DateTime

            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }



        public static byte[] ComputeSHA1(byte[] data)
        {
            var algorithm = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);
            var buffer = CryptographicBuffer.CreateFromByteArray(data);
            var hash = algorithm.HashData(buffer);

            CryptographicBuffer.CopyToByteArray(hash, out byte[] digest);
            return digest;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static byte[] ComputeHash(byte[] salt, byte[] passcode)
        {
            var array = Combine(salt, passcode, salt);
            for (int i = 0; i < 1000; i++)
            {
                var data = Combine(BitConverter.GetBytes(i), array);
                ComputeSHA1(data);
            }
            return ComputeSHA1(array);
        }


        public static byte[] Combine(params byte[][] arrays)
        {
            var length = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                length += arrays[i].Length;
            }

            var result = new byte[length]; ////[arrays.Sum(a => a.Length)];
            var offset = 0;
            foreach (var array in arrays)
            {
                Buffer.BlockCopy(array, 0, result, offset, array.Length);
                offset += array.Length;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (((a == null) || (b == null)) || (a.Length != b.Length))
            {
                return false;
            }
            var flag = true;
            for (var i = 0; i < a.Length; i++)
            {
                flag &= a[i] == b[i];
            }
            return flag;
        }

        public static DateTime ToDateTime(int? date)
        {
            var ticks = date.Value;
            return Utils.UnixTimestampToDateTime(ticks >> 32);
        }
    }
}
