using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Org.BouncyCastle.Security;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods;

namespace Telegram.Api.Services
{
    public class AuthKeyItem
    {
        public long AutkKeyId { get; set; }
        public byte[] AuthKey { get; set; }
    }

    public partial class MTProtoService
    {
        public static byte[] GetSalt(byte[] newNonce, byte[] serverNonce)
        {
            var newNonceBytes = newNonce.Take(8).ToArray();
            var serverNonceBytes = serverNonce.Take(8).ToArray();

            var returnBytes = new byte[8];
            for (int i = 0; i < returnBytes.Length; i++)
            {
                returnBytes[i] = (byte)(newNonceBytes[i] ^ serverNonceBytes[i]);
            }

            return returnBytes;
        }

        // b - big endian bytes
        // g - serialized data
        // dhPrime - serialized data
        // returns big-endian G_B
        public static byte[] GetGB(byte[] bData, int gData, byte[] pString)
        {
            var i_g_a = Org.BouncyCastle.Math.BigInteger.ValueOf(gData);
            i_g_a = i_g_a.ModPow(new Org.BouncyCastle.Math.BigInteger(1, bData), new Org.BouncyCastle.Math.BigInteger(1, pString));

            byte[] g_a = i_g_a.ToByteArray();
            if (g_a.Length > 256)
            {
                byte[] correctedAuth = new byte[256];
                Buffer.BlockCopy(g_a, 1, correctedAuth, 0, 256);
                g_a = correctedAuth;
            }

            return g_a;

            // OLD IMPLEMENTATION
            ////var bBytes = new byte[256]; // big endian bytes
            ////var random = new Random();
            ////random.NextBytes(bBytes);

            //var g = new BigInteger(gData.Value);
            //var p = pString.ToBigInteger();
            //var b = new BigInteger(bData.Reverse().Concat(new byte[] { 0x00 }).ToArray());

            //var gb = BigInteger.ModPow(g, b, p).ToByteArray(); // little endian + (may be) zero last byte
            ////remove last zero byte
            //if (gb[gb.Length - 1] == 0x00)
            //{
            //    gb = gb.SubArray(0, gb.Length - 1);
            //}

            //var length = gb.Length;
            //var result = new byte[length];
            //for (int i = 0; i < length; i++)
            //{
            //    result[length - i - 1] = gb[i];
            //}

            //return result;
        }

        //public BigInteger ToBigInteger()
        //{
        //    var data = new List<byte>(Data);
        //    while (data[0] == 0x00)
        //    {
        //        data.RemoveAt(0);
        //    }

        //    return new BigInteger(Data.Reverse().Concat(new byte[] { 0x00 }).ToArray());  //NOTE: add reverse here
        //}

        public static Tuple<byte[], byte[]> GetAesKeyIV(byte[] serverNonce, byte[] newNonce)
        {
            var newNonceServerNonce = newNonce.Concat(serverNonce).ToArray();
            var serverNonceNewNonce = serverNonce.Concat(newNonce).ToArray();
            var key = Utils.ComputeSHA1(newNonceServerNonce)
                .Concat(Utils.ComputeSHA1(serverNonceNewNonce).SubArray(0, 12));
            var im = Utils.ComputeSHA1(serverNonceNewNonce).SubArray(12, 8)
                .Concat(Utils.ComputeSHA1(newNonce.Concat(newNonce).ToArray()))
                .Concat(newNonce.SubArray(0, 4));

            return new Tuple<byte[], byte[]>(key.ToArray(), im.ToArray());
        }
    }
}
