using System;
using System.IO;
using System.Linq;
using Windows.Security.Cryptography.Core;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Telegram.Api.TL;
using System.Globalization;
using Telegram.Api.Services;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace Telegram.Api.Helpers
{
    public class PollardRhoLong
    {
        public static long Gcd(long ths, long val)
        {
            if (val == 0)
                return Math.Abs(ths);
            if (ths == 0)
                return Math.Abs(val);

            long r;
            long u = ths;
            long v = val;

            while (v != 0)
            {
                r = u % v;
                u = v;
                v = r;
            }

            return u;
        }

        public static long Rho(long N)
        {
            var random = new Random();

            long divisor;
            var bytes = new byte[8];
            random.NextBytes(bytes);
            var c = BitConverter.ToInt64(bytes, 0);
            random.NextBytes(bytes);
            var x = BitConverter.ToInt64(bytes, 0);
            var xx = x;

            // check divisibility by 2
            if (N % 2 == 0) return 2;

            do
            {
                x = (x*x % N + c ) % N;
                xx = (xx * xx % N + c) % N;
                xx = (xx * xx % N + c) % N;
                divisor = Gcd(x - xx, N);
            } while (divisor == 1);

            return divisor;
        }
    }

    public class PollardRho
    {
        private static readonly BigInteger ZERO = new BigInteger("0");
        private static readonly BigInteger ONE = new BigInteger("1");
        private static readonly BigInteger TWO = new BigInteger("2");
        private static readonly SecureRandom random = new SecureRandom();

        public static BigInteger Rho(BigInteger N)
        {
            BigInteger divisor;
            var c = new BigInteger(N.BitLength, random);
            var x = new BigInteger(N.BitLength, random);
            var xx = x;

            // check divisibility by 2
            if (N.Mod(TWO).CompareTo(ZERO) == 0) return TWO;

            do
            {
                x = x.Multiply(x).Mod(N).Add(c).Mod(N);
                xx = xx.Multiply(xx).Mod(N).Add(c).Mod(N);
                xx = xx.Multiply(xx).Mod(N).Add(c).Mod(N);
                divisor = x.Subtract(xx).Gcd(N);
            } while ((divisor.CompareTo(ONE)) == 0);

            return divisor;
        }

        public static Tuple<BigInteger, BigInteger> Factor(BigInteger N)
        {
            //if (N.CompareTo(ONE) == 0)
            //{
            //    return new Tuple<BigInteger, BigInteger>(ONE, N);
            //}
            //if (N.IsProbablePrime(20))
            //{
            //    return new Tuple<BigInteger, BigInteger>(ONE, N);
            //}
            var divisor = Rho(N);

            var divisor2 = N.Divide(divisor);

            return divisor.CompareTo(divisor2) > 0
                ? new Tuple<BigInteger, BigInteger>(divisor2, divisor)
                : new Tuple<BigInteger, BigInteger>(divisor, divisor2);
        }


        //public static void main(String[] args) {
        //    BigInteger N = new BigInteger(args[0]);
        //    factor(N);
        //}
    }

    public static class Utils
    {
        public static string GetShortTimePattern(ref CultureInfo ci)
        {
            if (ci.DateTimeFormat.ShortTimePattern.Contains("H"))
            {
                return "H:mm";
            }
            ci.DateTimeFormat.AMDesignator = "am";
            ci.DateTimeFormat.PMDesignator = "pm";
            return "h:mmt";
        }

#if !WIN_RT
        public static bool XapContentFileExists(string relativePath)
        {
            return Application.GetResourceStream(new Uri(relativePath, UriKind.Relative)) != null;
        }
#endif

        public static byte[] GetRSABytes(byte[] bytes)
        {
            // big-endian exponent and modulus
            const string exponentString = "010001";
            const string modulusString = "C150023E2F70DB7985DED064759CFECF" +
                                     "0AF328E69A41DAF4D6F01B538135A6F91F8F8B2A0EC9BA9720CE352EFCF6C5680FFC424BD6348649" +
                                     "02DE0B4BD6D49F4E580230E3AE97D95C8B19442B3C0A10D8F5633FECEDD6926A7F6DAB0DDB7D457F" +
                                     "9EA81B8465FCD6FFFEED114011DF91C059CAEDAF97625F6C96ECC74725556934EF781D866B34F011" +
                                     "FCE4D835A090196E9A5F0E4449AF7EB697DDB9076494CA5F81104A305B6DD27665722C46B60E5DF6" +
                                     "80FB16B210607EF217652E60236C255F6A28315F4083A96791D7214BF64C1DF4FD0DB1944FB26A2A" +
                                     "57031B32EEE64AD15A8BA68885CDE74A5BFC920F6ABF59BA5C75506373E7130F9042DA922179251F";
            var modulusBytes = StringToByteArray(modulusString);
            var exponentBytes = StringToByteArray(exponentString);
            var modulus = new System.Numerics.BigInteger(modulusBytes.Reverse().Concat(new byte[] { 0x00 }).ToArray());
            var exponent = new System.Numerics.BigInteger(exponentBytes.Reverse().Concat(new byte[] { 0x00 }).ToArray());
            var num = new System.Numerics.BigInteger(bytes.Reverse().Concat(new byte[] { 0x00 }).ToArray());

            var rsa = System.Numerics.BigInteger.ModPow(num, exponent, modulus).ToByteArray().Reverse().ToArray();

#if LOG_REGISTRATION
            TLUtils.WriteLog("RSA bytes length " + rsa.Length);
#endif
            if (rsa.Length == 257)
            {
                if (rsa[0] != 0x00) throw new Exception("rsa last byte is " + rsa[0]);

#if LOG_REGISTRATION
                TLUtils.WriteLog("First RSA byte removes: byte value is " + rsa[0]);
#endif
                TLUtils.WriteLine("First RSA byte removes: byte value is " + rsa[0]);
                rsa = rsa.SubArray(1, 256);
            }
            else if (rsa.Length < 256)
            {
                var correctedRsa = new byte[256];
                Array.Copy(rsa, 0, correctedRsa, 256 - rsa.Length, rsa.Length);
                for (var i = 0; i < 256 - rsa.Length; i++)
                {
                    correctedRsa[i] = 0;
#if LOG_REGISTRATION
                    TLUtils.WriteLog("First RSA bytes added i=" + i + " " + correctedRsa[i]);
#endif           
                }
                rsa = correctedRsa;
            }

            return rsa;
        }

        private static UInt64 GetP(UInt64 data)
        {
            var sqrt = (UInt64)Math.Sqrt(data);
            if (sqrt % 2 == 0) sqrt++;


            for (UInt64 i = sqrt; i >= 1; i = i - 2)
            {
                if (data % i == 0) return i;
            }

            return data;
        }

        public static Tuple<UInt64, UInt64> GetPQ(UInt64 pq)
        {
            var p = GetP(pq);
            var q = pq / p;

            if (p > q)
            {
                var temp = p;
                p = q;
                q = temp;
            }

            return new Tuple<UInt64, UInt64>(p, q);
        }

        public static Tuple<UInt64, UInt64> GetPQPollard(UInt64 pq)
        {
            var n = new BigInteger(BitConverter.GetBytes(pq).Reverse().ToArray());
            var result = PollardRho.Factor(n);
            return new Tuple<UInt64, UInt64>((UInt64)result.Item1.LongValue, (UInt64)result.Item2.LongValue);
        }

        public static Tuple<UInt64, UInt64> GetFastPQ(UInt64 pq)
        {
            var first = FastFactor((long)pq);
            var second = (long)pq / first;

            return first < second?
                new Tuple<UInt64, UInt64>((UInt64)first, (UInt64)second) :
                new Tuple<UInt64, UInt64>((UInt64)second, (UInt64)first);
        }

        public static long GCD(long a, long b)
        {
            while (a != 0 && b != 0)
            {
                while ((b & 1) == 0)
                {
                    b >>= 1;
                }
                while ((a & 1) == 0)
                {
                    a >>= 1;
                }
                if (a > b)
                {
                    a -= b;
                }
                else
                {
                    b -= a;
                }
            }
            return b == 0 ? a : b;
        }

        public static long FastFactor(long what)
        {
            Random r = new Random();
            long g = 0;
            int it = 0;
            for (int i = 0; i < 3; i++)
            {
                int q = (r.Next(128) & 15) + 17;
                long x = r.Next(1000000000) + 1, y = x;
                int lim = 1 << (i + 18);
                for (int j = 1; j < lim; j++)
                {
                    ++it;
                    long a = x, b = x, c = q;
                    while (b != 0)
                    {
                        if ((b & 1) != 0)
                        {
                            c += a;
                            if (c >= what)
                            {
                                c -= what;
                            }
                        }
                        a += a;
                        if (a >= what)
                        {
                            a -= what;
                        }
                        b >>= 1;
                    }
                    x = c;
                    long z = x < y ? y - x : x - y;
                    g = GCD(z, what);
                    if (g != 1)
                    {
                        break;
                    }
                    if ((j & (j - 1)) == 0)
                    {
                        y = x;
                    }
                }
                if (g > 1)
                {
                    break;
                }
            }

            long p = what / g;
            return Math.Min(p, g);
        }




        private static byte[] XorArrays(byte[] first, byte[] second)
        {
            var bytes = new byte[16];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte) (first[i] ^ second[i]);
            }

            return bytes;
        }
#if WINDOWS_PHONE || WIN_RT
        public static Stream AesIge(Stream data, byte[] key, byte[] iv, bool encrypt)
        {
            var cipher = CipherUtilities.GetCipher("AES/ECB/NOPADDING");
            var param = new KeyParameter(key);
            cipher.Init(encrypt, param);

            var inData = data;
            var outStream = new MemoryStream();
            var position = 0;

            byte[] xOld = new byte[16], yOld = new byte[16], x = new byte[16];

            Array.Copy(iv, 0, encrypt ? yOld : xOld, 0, 16);
            Array.Copy(iv, 16, encrypt ? xOld : yOld, 0, 16);

            while (position < inData.Length)
            {
                long length;
                if ((position + 16) < inData.Length)
                {
                    length = 16;
                }
                else
                {
                    length = inData.Length - position;
                }

                inData.Read(x, 0, (int)length);
                //Array.Copy(inData, position, x, 0, length);


                var processedBytes = cipher.ProcessBytes(XorArrays(x, yOld));
                byte[] y = XorArrays(processedBytes, xOld);

                xOld = (byte[])x.Clone();
                //xOld = new byte[x.Length];
                //Array.Copy(x, xOld, x.Length);
                yOld = y;

                outStream.Write(y, 0, y.Length);
                //outData = TLUtils.Combine(outData, y);

                position += 16;
            }
            return outStream;
            //return outData;
        }

        public static byte[] AesIge(byte[] data, byte[] key, byte[] iv, bool encrypt)
        {
            byte[] nextIV;
            return AesIge(data, key, iv, encrypt, out nextIV);
        }

        public static byte[] AesIge(byte[] data, byte[] key, byte[] iv, bool encrypt, out byte[] nextIV)
        {
            var cipher = CipherUtilities.GetCipher("AES/ECB/NOPADDING");
            var param = new KeyParameter(key);
            cipher.Init(encrypt, param);

            var inData = data;
            //var outData = new byte[]{};
            var outStream = new MemoryStream();
            var position = 0;

            byte[] xOld = new byte[16], yOld = new byte[16], x = new byte[16];

            Array.Copy(iv, 0, encrypt ? yOld : xOld, 0, 16);
            Array.Copy(iv, 16, encrypt ? xOld : yOld, 0, 16);

            while (position < inData.Length)
            {
                int length;
                if ((position + 16) < inData.Length)
                {
                    length = 16;
                }
                else
                {
                    length = inData.Length - position;
                }

                Array.Copy(inData, position, x, 0, length);


                var processedBytes = cipher.ProcessBytes(XorArrays(x, yOld));
                byte[] y = XorArrays(processedBytes, xOld);

                xOld = (byte[]) x.Clone();
                //xOld = new byte[x.Length];
                //Array.Copy(x, xOld, x.Length);
                yOld = y;

                outStream.Write(y, 0 , y.Length);
                //outData = TLUtils.Combine(outData, y);

                position += 16;
            }

            nextIV = encrypt ? TLUtils.Combine(yOld, xOld) : TLUtils.Combine(xOld, yOld);

            return outStream.ToArray();
            //return outData;
        }
#else
        public static byte[] AesIge(byte[] data, byte[] key, byte[] iv, bool encrypt)
        {
            var inData = new List<byte>(data);
            var outData = new List<byte>();
            var position = 0;
            
            byte[] xOld = new byte[16], yOld = new byte[16], x = new byte[16], y = new byte[16];

            Array.Copy(iv,  0, encrypt ? yOld : xOld, 0, 16);
            Array.Copy(iv, 16, encrypt ? xOld : yOld, 0, 16);
            
            using (var rijAlg = new RijndaelManaged {Mode = CipherMode.ECB, Padding = PaddingMode.None})
            {
                //rijAlg.GenerateIV();
                
                while (position < inData.Count)
                {
                    int length;
                    if ((position + 16) < inData.Count)
                    {
                        length = 16;
                    }
                    else
                    {
                        length = inData.Count - position;
                    }

                    Array.Copy(inData.ToArray(), position, x, 0, length);

                    // Create a decrytor to perform the stream transform.
                    var cryptor = encrypt ? rijAlg.CreateEncryptor(key, iv) : rijAlg.CreateDecryptor(key, iv);

                    // Create the streams used for encryption. 
                    using (var msEncrypt = new MemoryStream())
                    {
                        using (var csEncrypt = new CryptoStream(msEncrypt, cryptor, CryptoStreamMode.Write))
                        {
                            using (var swEncrypt = new BinaryWriter(csEncrypt))
                            {

                                //Write all data to the stream.
                                swEncrypt.Write(XorArrays(x, yOld));
                            }
                            y = XorArrays(msEncrypt.ToArray(), xOld);
                        }
                    }

                    xOld = (byte[]) x.Clone();
                    yOld = y;

                    outData = outData.Concat(y).ToList();

                    position += 16;
                }
            }

            return outData.ToArray();
        }
#endif

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length / 2;
            byte[] bytes = new byte[NumberChars];
            StringReader sr = new StringReader(hex);
            for (int i = 0; i < NumberChars; i++)
                bytes[i] = Convert.ToByte(new string(new char[2] { (char)sr.Read(), (char)sr.Read() }), 16);
            sr.Dispose();
            return bytes;
        }

        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            var result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static double DateTimeToUnixTimestamp(DateTime dateTime)
        {
            // From local DateTime to UTC0 UnixTime

            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            return (dateTime.ToUniversalTime() - dtDateTime).TotalSeconds;
        }

        public static DateTime UnixTimestampToDateTime(double unixTimeStamp)
        {
            // From UTC0 UnixTime to local DateTime

            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }


        private const int encryptKeyIVParam = 0;
        private const int decryptKeyIVParam = 8;

        public static Tuple<byte[], byte[]> GetEncryptKeyIV(byte[] authKey, byte[] msgKey)
        {
            //TLUtils.WriteLine("--Compute encrypt Key IV Common--");          
            return GetKeyIVCommon(authKey, msgKey, encryptKeyIVParam);
        }

        public static Tuple<byte[], byte[]> GetDecryptKeyIV(byte[] authKey, byte[] msgKey)
        {
            //TLUtils.WriteLine("--Compute decrypt Key IV Common--");
            return GetKeyIVCommon(authKey, msgKey, decryptKeyIVParam);
        }

        private static Tuple<byte[], byte[]> GetKeyIVCommon(byte[] authKey, byte[] msgKey, int x)
        {
            var sha1_a = ComputeSHA1(msgKey.Concat(authKey.SubArray(x, 32)).ToArray());
            var sha1_b = ComputeSHA1(
                authKey.SubArray(32 + x, 16)
                    .Concat(msgKey)
                    .Concat(authKey.SubArray(48 + x, 16)).ToArray());

            var sha1_c = ComputeSHA1(
                authKey.SubArray(64 + x, 32).Concat(msgKey).ToArray());

            var sha1_d = ComputeSHA1(msgKey.Concat(authKey.SubArray(96 + x, 32)).ToArray());

            var aesKey = sha1_a.SubArray(0, 8)
                .Concat(sha1_b.SubArray(8, 12))
                .Concat(sha1_c.SubArray(4, 12))
                .ToArray();

            var aesIV = sha1_a.SubArray(8, 12)
                .Concat(sha1_b.SubArray(0, 8))
                .Concat(sha1_c.SubArray(16, 4))
                .Concat(sha1_d.SubArray(0, 8))
                .ToArray();

            return new Tuple<byte[], byte[]>(aesKey, aesIV);
        }

        public static byte[] ComputeSHA1(byte[] data)
        {
            var sha1 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);
            return sha1.HashData(data.AsBuffer()).ToArray();
        }

        public static byte[] ComputeMD5(string data)
        {
            var input = CryptographicBuffer.ConvertStringToBinary(data, BinaryStringEncoding.Utf8);
            var hasher = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
            var hashed = hasher.HashData(input);
            byte[] digest;
            CryptographicBuffer.CopyToByteArray(hashed, out digest);

            return digest;
        }

        public static string CurrentUICulture()
        {
            return Windows.Globalization.Language.CurrentInputMethodLanguageTag;
        }

        public static int GetColorIndex(int id)
        {
            return id % 6;

            if (id < 0)
            {
                id += 256;
            }

            try
            {
                var str = string.Format("{0}{1}", id, MTProtoService.Current.CurrentUserId);
                if (str.Length > 15)
                {
                    str = str.Substring(0, 15);
                }

                var input = CryptographicBuffer.ConvertStringToBinary(str, BinaryStringEncoding.Utf8);
                //if (input.Length > 16)
                //{
                //    byte[] temp;
                //    CryptographicBuffer.CopyToByteArray(input, out temp);
                //    input = CryptographicBuffer.CreateFromByteArray(temp.Take(16).ToArray());
                //}

                var hasher = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
                var hashed = hasher.HashData(input);
                byte[] digest;
                CryptographicBuffer.CopyToByteArray(hashed, out digest);

                return digest[id % 0x0F] & 0x07;
            }
            catch { }

            return id % 8;
        }
    }
}
