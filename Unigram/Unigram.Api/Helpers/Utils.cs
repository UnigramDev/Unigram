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
using System.Diagnostics;
using Org.BouncyCastle.OpenSsl;
using System.Collections.Generic;

namespace Telegram.Api.Helpers
{
    public static class Utils
    {
        public static long BytesToLong(byte[] bytes)
        {
            return ((long)bytes[7] << 56) + (((long)bytes[6] & 0xFF) << 48) + (((long)bytes[5] & 0xFF) << 40) + (((long)bytes[4] & 0xFF) << 32)
                    + (((long)bytes[3] & 0xFF) << 24) + (((long)bytes[2] & 0xFF) << 16) + (((long)bytes[1] & 0xFF) << 8) + ((long)bytes[0] & 0xFF);
        }

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

        public static byte[] GetRSABytes(byte[] bytes, string key)
        {
            if (key == null)
            {
                key =
    "-----BEGIN RSA PUBLIC KEY-----\n" +
    "MIIBCgKCAQEAwVACPi9w23mF3tBkdZz+zwrzKOaaQdr01vAbU4E1pvkfj4sqDsm6\n" +
    "lyDONS789sVoD/xCS9Y0hkkC3gtL1tSfTlgCMOOul9lcixlEKzwKENj1Yz/s7daS\n" +
    "an9tqw3bfUV/nqgbhGX81v/+7RFAEd+RwFnK7a+XYl9sluzHRyVVaTTveB2GazTw\n" +
    "Efzk2DWgkBluml8OREmvfraX3bkHZJTKX4EQSjBbbdJ2ZXIsRrYOXfaA+xayEGB+\n" +
    "8hdlLmAjbCVfaigxX0CDqWeR1yFL9kwd9P0NsZRPsmoqVwMbMu7mStFai6aIhc3n\n" +
    "Slv8kg9qv1m6XHVQY3PnEw+QQtqSIXklHwIDAQAB\n" +
    "-----END RSA PUBLIC KEY-----";
            }

            using (var text = new StringReader(key))
            {
                var reader = new PemReader(text);
                var parameter = reader.ReadObject() as RsaKeyParameters;
                if (parameter != null)
                {
                    var modulus = parameter.Modulus;
                    var exponent = parameter.Exponent;

                    var num = new BigInteger(new byte[] { 0x00 }.Concat(bytes).ToArray());
                    var rsa = num.ModPow(exponent, modulus).ToByteArray();

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
            }

            return null;
        }

        // Note: ivec - big-endian, but BigInterger.ctor and BigInteger.ToByteArray return little-endian
        public static byte[] AES_ctr128_encrypt(byte[] input, IBuffer key, ref byte[] ivec, ref byte[] ecount_buf, ref uint num)
        {
            uint n;
            var output = new byte[input.Length];
            n = num;

            var provider = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesEcb);
            var keySymmetric = provider.CreateSymmetricKey(key);

            for (uint i = 0; i < input.Length; i++)
            {
                if (n == 0)
                {
                    var ivecBuffer = CryptographicBuffer.CreateFromByteArray(ivec);
                    var ecountBuffer = CryptographicEngine.Encrypt(keySymmetric, ivecBuffer, null);

                    CryptographicBuffer.CopyToByteArray(ecountBuffer, out ecount_buf);
                    Array.Reverse(ivec);
                    var bi = new System.Numerics.BigInteger(TLUtils.Combine(ivec, new byte[] { 0x00 }));
                    bi = (bi + 1);
                    var biArray = bi.ToByteArray();
                    var b = new byte[16];
                    //for (var j = 0; j < biArray.Length && j < b.Length; j++)
                    //{
                    //    b[j] = biArray[j];
                    //}

                    System.Buffer.BlockCopy(biArray, 0, b, 0, Math.Min(biArray.Length, b.Length));

                    //System.Diagnostics.Debug.WriteLine(bi);
                    Array.Reverse(b);
                    ivec = b;
                }

                output[i] = (byte)(input[i] ^ ecount_buf[n]);
                n = (n + 1) % 16;
            }

            num = n;
            return output;
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

            return first < second ?
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
                bytes[i] = (byte)(first[i] ^ second[i]);
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

                xOld = (byte[])x.Clone();
                //xOld = new byte[x.Length];
                //Array.Copy(x, xOld, x.Length);
                yOld = y;

                outStream.Write(y, 0, y.Length);
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

        public static long CurrentTimestamp
        {
            get
            {
                var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                DateTime.SpecifyKind(dtDateTime, DateTimeKind.Utc);

                return (long)(DateTime.Now.ToUniversalTime() - dtDateTime).TotalMilliseconds;
            }
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
            var algorithm = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha1);
            var buffer = CryptographicBuffer.CreateFromByteArray(data);
            var hash = algorithm.HashData(buffer);

            CryptographicBuffer.CopyToByteArray(hash, out byte[] digest);
            return digest;
        }

        public static byte[] ComputeSHA256(byte[] data)
        {
            var algorithm = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);
            var buffer = CryptographicBuffer.CreateFromByteArray(data);
            var hash = algorithm.HashData(buffer);

            CryptographicBuffer.CopyToByteArray(hash, out byte[] digest);
            return digest;
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

        public static string MD5(string data)
        {
            var input = CryptographicBuffer.ConvertStringToBinary(data, BinaryStringEncoding.Utf8);
            var hasher = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
            var hashed = hasher.HashData(input);
            var digest = CryptographicBuffer.EncodeToBase64String(hashed);

            return digest;
        }

        public static string CurrentUICulture()
        {
            //return "en";

            return Windows.Globalization.Language.CurrentInputMethodLanguageTag;
        }

        public static int GetColorIndex(int id)
        {
            return id % 6;
        }

        public static List<TLMessageEntityBase> GetEntities(ref string message)
        {
            if (message == null)
            {
                return null;
            }
            List<TLMessageEntityBase> entities = null;
            int index;
            int start = -1;
            int lastIndex = 0;
            bool isPre = false;
            const String mono = "`";
            const String pre = "```";
            const String bold = "**";
            const String italic = "__";
            while ((index = message.IndexOf(!isPre ? mono : pre, lastIndex)) != -1)
            {
                if (start == -1)
                {
                    isPre = message.Length - index > 2 && message[index + 1] == '`' && message[index + 2] == '`';
                    start = index;
                    lastIndex = index + (isPre ? 3 : 1);
                }
                else
                {
                    if (entities == null)
                    {
                        entities = new List<TLMessageEntityBase>();
                    }
                    for (int a = index + (isPre ? 3 : 1); a < message.Length; a++)
                    {
                        if (message[a] == '`')
                        {
                            index++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    lastIndex = index + (isPre ? 3 : 1);
                    if (isPre)
                    {
                        int firstChar = start > 0 ? message[start - 1] : 0;
                        bool replacedFirst = firstChar == ' ' || firstChar == '\n';
                        string startMessage = message.Substr(0, start - (replacedFirst ? 1 : 0));
                        string content = message.Substr(start + 3, index);
                        firstChar = index + 3 < message.Length ? message[index + 3] : 0;
                        string endMessage = message.Substr(index + 3 + (firstChar == ' ' || firstChar == '\n' ? 1 : 0), message.Length);
                        if (startMessage.Length != 0)
                        {
                            startMessage = string.Concat(startMessage, "\n");
                        }
                        else
                        {
                            replacedFirst = true;
                        }
                        if (endMessage.Length != 0)
                        {
                            endMessage = string.Concat("\n", endMessage);
                        }
                        if (!string.IsNullOrEmpty(content))
                        {
                            message = string.Concat(startMessage, content, endMessage);
                            TLMessageEntityPre entity = new TLMessageEntityPre();
                            entity.Offset = start + (replacedFirst ? 0 : 1);
                            entity.Length = index - start - 3 + (replacedFirst ? 0 : 1);
                            entity.Language = string.Empty;
                            entities.Add(entity);
                            lastIndex -= 6;
                        }
                    }
                    else
                    {
                        if (start + 1 != index)
                        {
                            message = string.Concat(message.Substr(0, start), message.Substr(start + 1, index), message.Substr(index + 1, message.Length));
                            TLMessageEntityCode entity = new TLMessageEntityCode();
                            entity.Offset = start;
                            entity.Length = index - start - 1;
                            entities.Add(entity);
                            lastIndex -= 2;
                        }
                    }
                    start = -1;
                    isPre = false;
                }
            }
            if (start != -1 && isPre)
            {
                message = string.Concat(message.Substr(0, start), message.Substr(start + 2, message.Length));
                if (entities == null)
                {
                    entities = new List<TLMessageEntityBase>();
                }
                TLMessageEntityCode entity = new TLMessageEntityCode();
                entity.Offset = start;
                entity.Length = 1;
                entities.Add(entity);
            }

            //            if (message instanceof Spannable) {
            //                Spannable spannable = (Spannable)message;
            //                TypefaceSpan spans[] = spannable.getSpans(0, message.length(), TypefaceSpan.class);
            //            if (spans != null && spans.length > 0) {
            //                for (int a = 0; a<spans.length; a++) {
            //                    TypefaceSpan span = spans[a];
            //        int spanStart = spannable.getSpanStart(span);
            //        int spanEnd = spannable.getSpanEnd(span);
            //                    if (checkInclusion(spanStart, entities) || checkInclusion(spanEnd, entities) || checkIntersection(spanStart, spanEnd, entities)) {
            //                        continue;
            //                    }
            //                    if (entities == null) {
            //                        entities = new ArrayList<>();
            //                    }
            //    TLRPC.MessageEntity entity;
            //                    if (span.isBold()) {
            //                        entity = new TLRPC.TL_messageEntityBold();
            //                    } else {
            //                        entity = new TLRPC.TL_messageEntityItalic();
            //                    }
            //                    entity.offset = spanStart;
            //                    entity.length = spanEnd - spanStart;
            //                    entities.add(entity);
            //                }
            //            }

            //            URLSpanUserMention spansMentions[] = spannable.getSpans(0, message.length(), URLSpanUserMention.class);
            //            if (spansMentions != null && spansMentions.length > 0) {
            //                if (entities == null) {
            //                    entities = new ArrayList<>();
            //                }
            //                for (int b = 0; b<spansMentions.length; b++) {
            //                    TLRPC.TL_inputMessageEntityMentionName entity = new TLRPC.TL_inputMessageEntityMentionName();
            //entity.user_id = MessagesController.getInputUser(Utilities.parseInt(spansMentions[b].getURL()));
            //                    if (entity.user_id != null) {
            //                        entity.offset = spannable.getSpanStart(spansMentions[b]);
            //                        entity.length = Math.min(spannable.getSpanEnd(spansMentions[b]), message.length()) - entity.offset;
            //                        if (message.charAt(entity.offset + entity.length - 1) == ' ') {
            //                            entity.length--;
            //                        }
            //                        entities.add(entity);
            //                    }
            //                }
            //            }
            //        }

            for (int c = 0; c < 2; c++)
            {
                lastIndex = 0;
                start = -1;
                String checkString = c == 0 ? bold : italic;
                char checkChar = c == 0 ? '*' : '_';
                while ((index = message.IndexOf(checkString, lastIndex)) != -1)
                {
                    if (start == -1)
                    {
                        char prevChar = index == 0 ? ' ' : message[index - 1];
                        if (!CheckInclusion(index, entities) && (prevChar == ' ' || prevChar == '\n'))
                        {
                            start = index;
                        }
                        lastIndex = index + 2;
                    }
                    else
                    {
                        for (int a = index + 2; a < message.Length; a++)
                        {
                            if (message[a] == checkChar)
                            {
                                index++;
                            }
                            else
                            {
                                break;
                            }
                        }
                        lastIndex = index + 2;
                        if (CheckInclusion(index, entities) || CheckIntersection(start, index, entities))
                        {
                            start = -1;
                            continue;
                        }
                        if (start + 2 != index)
                        {
                            if (entities == null)
                            {
                                entities = new List<TLMessageEntityBase>();
                            }
                            message = string.Concat(message.Substr(0, start), message.Substr(start + 2, index), message.Substr(index + 2, message.Length));
                            TLMessageEntityBase entity;
                            if (c == 0)
                            {
                                entity = new TLMessageEntityBold();
                            }
                            else
                            {
                                entity = new TLMessageEntityItalic();
                            }
                            entity.Offset = start;
                            entity.Length = index - start - 2;
                            RemoveOffsetAfter(entity.Offset + entity.Length, 4, entities);
                            entities.Add(entity);
                            lastIndex -= 4;
                        }
                        start = -1;
                    }
                }
            }

            return entities;
        }

        public static bool CheckInclusion(int index, List<TLMessageEntityBase> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                return false;
            }
            int count = entities.Count;
            for (int a = 0; a < count; a++)
            {
                TLMessageEntityBase entity = entities[a];
                if (entity.Offset <= index && entity.Offset + entity.Length > index)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CheckIntersection(int start, int end, List<TLMessageEntityBase> entities)
        {
            if (entities == null || entities.Count == 0)
            {
                return false;
            }
            int count = entities.Count;
            for (int a = 0; a < count; a++)
            {
                TLMessageEntityBase entity = entities[a];
                if (entity.Offset > start && entity.Offset + entity.Length <= end)
                {
                    return true;
                }
            }
            return false;
        }

        private static void RemoveOffsetAfter(int start, int countToRemove, List<TLMessageEntityBase> entities)
        {
            int count = entities.Count;
            for (int a = 0; a < count; a++)
            {
                TLMessageEntityBase entity = entities[a];
                if (entity.Offset > start)
                {
                    entity.Offset -= countToRemove;
                }
            }
        }
    }

    internal class PollardRhoLong
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
                x = (x * x % N + c) % N;
                xx = (xx * xx % N + c) % N;
                xx = (xx * xx % N + c) % N;
                divisor = Gcd(x - xx, N);
            } while (divisor == 1);

            return divisor;
        }
    }

    internal class PollardRho
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
}
