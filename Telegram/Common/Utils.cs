//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Runtime.CompilerServices;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;

namespace Telegram.Common
{
    public static class Utils
    {
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
            if (a == null || b == null || a.Length != b.Length)
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
    }
}
