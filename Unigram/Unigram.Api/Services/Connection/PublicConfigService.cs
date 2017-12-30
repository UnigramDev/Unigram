using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Runtime.Serialization.Json;
#if !WIN_RT
using System.Security.Cryptography;
#endif
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Help;
using Org.BouncyCastle.OpenSsl;
using Telegram.Api.Native.TL;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Telegram.Api.Services.Connection
{
    public class MockupPublicConfigService : IPublicConfigService
    {
        public void GetAsync(Action<TLConfigSimple> callback, Action<Exception> faultCallback = null)
        {

        }
    }

    public class PublicConfigService : IPublicConfigService
    {
        private static void Log(string str)
        {
            Logs.Log.Write(string.Format("  PublicConfigService {0}", str));
        }

        public bool Test { get; set; }

        private void PerformAppRequestAsync(Action<TLConfigSimple> callback, Action<Exception> faultCallback)
        {
            var request = Test ? WebRequest.Create("https://google.com/test/") : WebRequest.Create("https://google.com/");
            request.Headers["Host"] = "dns-telegram.appspot.com";

            Log("Start app request");

            request.BeginGetResponse(
                result =>
                {
                    Log("Stop app request");
                    try
                    {
                        var response = request.EndGetResponse(result);

                        string dataString;
                        using (var s = response.GetResponseStream())
                        {
                            using (var readStream = new StreamReader(s))
                            {
                                dataString = readStream.ReadToEnd();
                            }
                        }

                        //dataString = ParseDataString(dataString);

                        var configSimple = DecryptSimpleConfig(dataString);

                        callback?.Invoke(configSimple);
                    }
                    catch (Exception ex)
                    {
                        Log("App request exception\n" + ex);

                        faultCallback?.Invoke(ex);
                    }
                },
                request);
        }

        private void PerformDnsRequestAsync(Action<TLConfigSimple> callback, Action<Exception> faultCallback)
        {
            var request = Test ? WebRequest.Create("https://google.com/resolve?name=tap.stel.com&type=16") : WebRequest.Create("https://google.com/resolve?name=ap.stel.com&type=16");
            request.Headers["Host"] = "dns.google.com";

            Log("Start dns request");

            request.BeginGetResponse(
                result =>
                {
                    Log("Stop dns request");
                    try
                    {
                        var response = request.EndGetResponse(result);

                        string dataString;
                        using (var s = response.GetResponseStream())
                        {
                            using (var readStream = new StreamReader(s))
                            {
                                dataString = readStream.ReadToEnd();
                            }
                        }

                        dataString = ParseDataString(dataString);

                        var configSimple = DecryptSimpleConfig(dataString);

                        callback?.Invoke(configSimple);
                    }
                    catch (Exception ex)
                    {
                        Log("Dns request exception\n" + ex);

                        faultCallback?.Invoke(ex);
                    }
                },
                request);
        }

        private static TLConfigSimple DecryptSimpleConfig(string dataString)
        {
            TLConfigSimple result = null;

            var base64Chars = dataString.Where(ch =>
            {
                var isGoodBase64 =
                    (ch == '+') || (ch == '=') || (ch == '/')
                    || (ch >= 'a' && ch <= 'z')
                    || (ch >= 'A' && ch <= 'Z')
                    || (ch >= '0' && ch <= '9');

                return isGoodBase64;
            }).ToArray();

            var cleanDataString = new string(base64Chars);
            const int kGoodSizeBase64 = 344;
            if (cleanDataString.Length != kGoodSizeBase64)
            {
                Log(string.Format("Bad base64 size {0} required {1}", cleanDataString.Length, kGoodSizeBase64));
                return null;
            }
            byte[] data = null;
            try
            {
                data = Convert.FromBase64String(cleanDataString);
            }
            catch (Exception ex)
            {
                Log("Bad base64 bytes");

                return null;
            }
            const int kGoodSizeData = 256;
            if (data.Length != kGoodSizeData)
            {
                Log(string.Format("Bad data size {0} required {1}", data.Length, kGoodSizeData));

                return null;
            }

            var rsa =
"-----BEGIN RSA PUBLIC KEY-----\n" +
"MIIBCgKCAQEAyr+18Rex2ohtVy8sroGPBwXD3DOoKCSpjDqYoXgCqB7ioln4eDCF\n" +
"fOBUlfXUEvM/fnKCpF46VkAftlb4VuPDeQSS/ZxZYEGqHaywlroVnXHIjgqoxiAd\n" +
"192xRGreuXIaUKmkwlM9JID9WS2jUsTpzQ91L8MEPLJ/4zrBwZua8W5fECwCCh2c\n" +
"9G5IzzBm+otMS/YKwmR1olzRCyEkyAEjXWqBI9Ftv5eG8m0VkBzOG655WIYdyV0H\n" +
"fDK/NWcvGqa0w/nriMD6mDjKOryamw0OP9QuYgMN0C9xMW9y8SmP4h92OAWodTYg\n" +
"Y1hZCxdv6cs5UnW9+PWvS+WIbkh+GaWYxwIDAQAB\n" +
"-----END RSA PUBLIC KEY-----";

            var text = new StringReader(rsa);
            var reader = new PemReader(text);
            var parameter = reader.ReadObject() as RsaKeyParameters;

            var modulus = parameter.Modulus.ToByteArray();
            var exponent = parameter.Exponent.ToByteArray();

            var dataBI = new BigInteger(data.Reverse().Concat(new byte[] { 0x00 }).ToArray());
            var exponentBI = new BigInteger(exponent.Reverse().Concat(new byte[] { 0x00 }).ToArray());
            var modulusBI = new BigInteger(modulus.Reverse().Concat(new byte[] { 0x00 }).ToArray());

            var authKey = BigInteger.ModPow(dataBI, exponentBI, modulusBI).ToByteArray();
            if (authKey[authKey.Length - 1] == 0x00)
            {
                authKey = authKey.SubArray(0, authKey.Length - 1);
            }

            authKey = authKey.Reverse().ToArray();
            if (authKey.Length > 256)
            {
                var correctedAuth = new byte[256];
                Array.Copy(authKey, authKey.Length - 256, correctedAuth, 0, 256);
                authKey = correctedAuth;
            }
            else if (authKey.Length < 256)
            {
                var correctedAuth = new byte[256];
                Array.Copy(authKey, 0, correctedAuth, 256 - authKey.Length, authKey.Length);
                for (var i = 0; i < 256 - authKey.Length; i++)
                {
                    authKey[i] = 0;
                }
                authKey = correctedAuth;
            }

            var key = authKey.SubArray(0, 32);
            var iv = authKey.SubArray(16, 16);
            var encryptedData = authKey.SubArray(32, authKey.Length - 32);

            var cipher = CipherUtilities.GetCipher("AES/CBC/NOPADDING");
            var param = new KeyParameter(key);
            cipher.Init(false, new ParametersWithIV(param, iv));
            var decryptedData = cipher.DoFinal(encryptedData);

            const int kDigestSize = 16;
            var hash = Utils.ComputeSHA256(decryptedData.SubArray(0, 208));
            for (var i = 0; i < kDigestSize; i++)
            {
                if (hash[i] != decryptedData[208 + i])
                {
                    Log("Bad digest");
                    return null;
                }
            }

            using (var from = TLObjectSerializer.CreateReader(decryptedData.AsBuffer()))
            {
                var length = from.ReadInt32();
                if (length <= 0 || length > 208 || length % 4 != 0)
                {
                    Log(string.Format("Bad length {0}", length));
                    return null;
                }

                try
                {
                    result = TLFactory.Read<TLConfigSimple>(from);
                }
                catch (Exception ex)
                {
                    Log("Could not read configSimple");
                    return null;
                }

                if (from.Position != length)
                {
                    Log(string.Format("Bad read length {0} shoud be {1}", from.Position, length));
                    return null;
                }
            }

            return result;
        }

        public void GetAsync(Action<TLConfigSimple> callback, Action<Exception> faultCallback = null)
        {
            PerformDnsRequestAsync(
                result =>
                {
                    Execute.ShowDebugMessage(result != null ? result.ToString() : "null");

                    callback?.Invoke(result);
                },
                faultCallback);
        }

        private static string ParseDataString(string dataString)
        {
            var serializer = new DataContractJsonSerializer(typeof(RootObject));
            RootObject rootObject;
            try
            {
                using (var stream = new MemoryStream(Encoding.Unicode.GetBytes(dataString)))
                {
                    rootObject = serializer.ReadObject(stream) as RootObject;
                }
            }
            catch (Exception ex)
            {
                Log("Failed to parse dns response JSON, ex\n" + ex);
                return null;
            }

            if (rootObject == null)
            {
                Log("Not an object received in dns response JSON");
                return null;
            }

            if (rootObject.Answer == null)
            {
                Log("Could not find Answer in dns response JSON");
                return null;
            }

            var result = new StringBuilder();
            for (int i = rootObject.Answer.Count - 1; i >= 0; i--)
            {
                result.Append(rootObject.Answer[i].data);
            }

            return result.ToString();
        }
    }

    public interface IPublicConfigService
    {
        void GetAsync(Action<TLConfigSimple> callback, Action<Exception> faultCallback = null);
    }

    public class Question
    {
        public string name { get; set; }
        public int type { get; set; }
    }

    public class Answer
    {
        public string name { get; set; }
        public int type { get; set; }
        public int TTL { get; set; }
        public string data { get; set; }
    }

    public class RootObject
    {
        public int Status { get; set; }
        public bool TC { get; set; }
        public bool RD { get; set; }
        public bool RA { get; set; }
        public bool AD { get; set; }
        public bool CD { get; set; }
        public List<Question> Question { get; set; }
        public List<Answer> Answer { get; set; }
    }
}
