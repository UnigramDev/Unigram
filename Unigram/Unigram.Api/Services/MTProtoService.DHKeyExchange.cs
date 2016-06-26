using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Org.BouncyCastle.Security;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using System.Threading.Tasks;
using Telegram.Api.TL.Methods;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        /// <summary>
        /// Список имеющихся ключей авторизации
        /// </summary>
        private static readonly Dictionary<long, AuthKeyItem> _authKeys = new Dictionary<long, AuthKeyItem>();

        private static readonly object _authKeysRoot = new object();

        private Task<MTProtoResponse<TLResPQ>> ReqPQAsync(TLInt128 nonce)
        {
            var obj = new TLReqPQ { Nonce = nonce };

            return SendNonEncryptedMessage<TLResPQ>("req_pq", obj);
        }

        private Task<MTProtoResponse<TLServerDHParamsBase>> ReqDHParamsAsync(TLInt128 nonce, TLInt128 serverNonce, byte[] p, byte[] q, long publicKeyFingerprint, byte[] encryptedData)
        {
            var obj = new TLReqDHParams { Nonce = nonce, ServerNonce = serverNonce, P = p, Q = q, PublicKeyFingerprint = publicKeyFingerprint, EncryptedData = encryptedData };

            return SendNonEncryptedMessage<TLServerDHParamsBase>("req_DH_params", obj);
        }

        public Task<MTProtoResponse<TLSetClientDHParamsAnswerBase>> SetClientDHParamsAsync(TLInt128 nonce, TLInt128 serverNonce, byte[] encryptedData)
        {
            var obj = new TLSetClientDHParams { Nonce = nonce, ServerNonce = serverNonce, EncryptedData = encryptedData };

            return SendNonEncryptedMessage<TLSetClientDHParamsAnswerBase>("set_client_DH_params", obj);
        }

        private TimeSpan _authTimeElapsed;

        public async Task<MTProtoResponse<Tuple<byte[], long?, long?>>> InitAsync()
        {
            MTProtoResponse<Tuple<byte[], long?, long?>> result = null;

            var authTime = Stopwatch.StartNew();
            var newNonce = TLInt256.Random();

#if LOG_REGISTRATION
            TLUtils.WriteLog("Start ReqPQ");
#endif
            var nonce = TLInt128.Random();
            var resPQ = await ReqPQAsync(nonce);
            if (resPQ.Error == null)
            {
                var serverNonce = resPQ.Value.ServerNonce;
                if (nonce != resPQ.Value.Nonce)
                {
                    var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect nonce" };
#if LOG_REGISTRATION
                    TLUtils.WriteLog("Stop ReqPQ with error " + error);
#endif

                    result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, error);
                    TLUtils.WriteLine(error.ToString());
                }

#if LOG_REGISTRATION
                TLUtils.WriteLog("Stop ReqPQ");
#endif
                TimeSpan calcTime;
                Tuple<ulong, ulong> pqPair;
                var innerData = GetInnerData(resPQ.Value, newNonce, out calcTime, out pqPair);
                var encryptedInnerData = GetEncryptedInnerData(innerData);

#if LOG_REGISTRATION
                    var pq = BitConverter.ToUInt64(resPQ.PQ.Data.Reverse().ToArray(), 0);

                    var logPQString = new StringBuilder();
                    logPQString.AppendLine("PQ Counters");
                    logPQString.AppendLine();
                    logPQString.AppendLine("pq: " + pq);
                    logPQString.AppendLine("p: " + pqPair.Item1);
                    logPQString.AppendLine("q: " + pqPair.Item2);
                    logPQString.AppendLine("encrypted_data length: " + encryptedInnerData.Data.Length);
                    TLUtils.WriteLog(logPQString.ToString());

                    TLUtils.WriteLog("Start ReqDHParams");
#endif
                var serverDHParams = await ReqDHParamsAsync(
                     resPQ.Value.Nonce,
                     resPQ.Value.ServerNonce,
                     innerData.P,
                     innerData.Q,
                     resPQ.Value.ServerPublicKeyFingerprints[0],
                     encryptedInnerData);
                if (serverDHParams.Error == null)
                {
                    if (nonce != serverDHParams.Value.Nonce)
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect nonce" };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                        result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, error);
                        TLUtils.WriteLine(error.ToString());
                    }
                    if (serverNonce != serverDHParams.Value.ServerNonce)
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect server_nonce" };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                        result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, error);
                        TLUtils.WriteLine(error.ToString());
                    }

#if LOG_REGISTRATION
                    TLUtils.WriteLog("Stop ReqDHParams");
#endif
                    var random = new SecureRandom();

                    var serverDHParamsOk = serverDHParams.Value as TLServerDHParamsOk;
                    if (serverDHParamsOk == null)
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = $"Incorrect serverDHParams {serverDHParams.GetType()}" };
                        result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, error);
                        TLUtils.WriteLine(error.ToString());
#if LOG_REGISTRATION
                            
                        TLUtils.WriteLog("ServerDHParams " + serverDHParams);  
#endif
                        return result;
                    }

                    var aesParams = GetAesKeyIV(resPQ.Value.ServerNonce.ToArray(), newNonce.ToArray());

                    var decryptedAnswerWithHash = Utils.AesIge(serverDHParamsOk.EncryptedAnswer, aesParams.Item1, aesParams.Item2, false);

                    //var position = 0;
                    //var serverDHInnerData = (TLServerDHInnerData)new TLServerDHInnerData().FromBytes(decryptedAnswerWithHash.Skip(20).ToArray(), ref position);
                    var serverDHInnerData = TLFactory.From<TLServerDHInnerData>(decryptedAnswerWithHash.Skip(20).ToArray());

                    var sha1 = Utils.ComputeSHA1(serverDHInnerData.ToArray());
                    if (!TLUtils.ByteArraysEqual(sha1, decryptedAnswerWithHash.Take(20).ToArray()))
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect sha1 TLServerDHInnerData" };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                        result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, error);
                        TLUtils.WriteLine(error.ToString());
                    }

                    if (!TLUtils.CheckPrime(serverDHInnerData.DHPrime, serverDHInnerData.G))
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect (p, q) pair" };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                        result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, error);
                        TLUtils.WriteLine(error.ToString());
                    }

                    if (!TLUtils.CheckGaAndGb(serverDHInnerData.GA, serverDHInnerData.DHPrime))
                    {
                        var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect g_a" };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                        result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, error);
                        TLUtils.WriteLine(error.ToString());
                    }

                    var bBytes = new byte[256]; //big endian B
                    random.NextBytes(bBytes);

                    var gbBytes = GetGB(bBytes, serverDHInnerData.G, serverDHInnerData.DHPrime);

                    var clientDHInnerData = new TLClientDHInnerData
                    {
                        Nonce = resPQ.Value.Nonce,
                        ServerNonce = resPQ.Value.ServerNonce,
                        RetryId = 0,
                        GB = gbBytes
                    };

                    var encryptedClientDHInnerData = GetEncryptedClientDHInnerData(clientDHInnerData, aesParams);
#if LOG_REGISTRATION
                            TLUtils.WriteLog("Start SetClientDHParams");  
#endif
                    var dhGen = await SetClientDHParamsAsync(resPQ.Value.Nonce, resPQ.Value.ServerNonce, encryptedClientDHInnerData);
                    if (dhGen.Error == null)
                    {
                        if (nonce != dhGen.Value.Nonce)
                        {
                            var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect nonce" };
#if LOG_REGISTRATION
                            TLUtils.WriteLog("Stop SetClientDHParams with error " + error);
#endif

                            result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, error);
                            TLUtils.WriteLine(error.ToString());
                        }
                        if (serverNonce != dhGen.Value.ServerNonce)
                        {
                            var error = new TLRPCError { ErrorCode = 404, ErrorMessage = "incorrect server_nonce" };
#if LOG_REGISTRATION
                            TLUtils.WriteLog("Stop SetClientDHParams with error " + error);
#endif

                            result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, error);
                            TLUtils.WriteLine(error.ToString());
                        }

                        var dhGenOk = dhGen.Value as TLDHGenOk;
                        if (dhGenOk == null)
                        {
                            var error = new TLRPCError { ErrorCode = 404, ErrorMessage = $"Incorrect dhGen {dhGen.GetType()}" };
                            result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, error);
                            TLUtils.WriteLine(error.ToString());
#if LOG_REGISTRATION
                            TLUtils.WriteLog("DHGen result " + serverDHParams);
#endif
                            return result;
                        }


                        _authTimeElapsed = authTime.Elapsed;
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop SetClientDHParams");
#endif
                        var getKeyTimer = Stopwatch.StartNew();
                        var authKey = GetAuthKey(bBytes, serverDHInnerData.GA.ToBytes(), serverDHInnerData.DHPrime.ToBytes());

                        var logCountersString = new StringBuilder();

                        logCountersString.AppendLine("Auth Counters");
                        logCountersString.AppendLine();
                        logCountersString.AppendLine("pq factorization time: " + calcTime);
                        logCountersString.AppendLine("calc auth key time: " + getKeyTimer.Elapsed);
                        logCountersString.AppendLine("auth time: " + _authTimeElapsed);
#if LOG_REGISTRATION
                        TLUtils.WriteLog(logCountersString.ToString());
#endif
                        //newNonce - little endian
                        //authResponse.ServerNonce - little endian
                        var salt = GetSalt(newNonce.ToArray(), resPQ.Value.ServerNonce.ToArray());
                        var sessionId = new byte[8];
                        random.NextBytes(sessionId);

                        TLUtils.WriteLine("Salt " + BitConverter.ToInt64(salt, 0) + " (" + BitConverter.ToString(salt) + ")");
                        TLUtils.WriteLine("Session id " + BitConverter.ToInt64(sessionId, 0) + " (" + BitConverter.ToString(sessionId) + ")");

                        result = new MTProtoResponse<Tuple<byte[], long?, long?>>(
                            new Tuple<byte[], long?, long?>(
                                authKey, 
                                new long?(
                                    BitConverter.ToInt64(salt, 0)), 
                                    new long?(BitConverter.ToInt64(sessionId, 0))));
                    }
                    else
                    {

#if LOG_REGISTRATION
                                    TLUtils.WriteLog("Stop SetClientDHParams with error " + error.ToString());
#endif
                        result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, dhGen.Error);
                        TLUtils.WriteLine(dhGen.Error.ToString());
                    }
                }
                else
                {

#if LOG_REGISTRATION
                            TLUtils.WriteLog("Stop ReqDHParams with error " + error.ToString());
#endif
                    result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, serverDHParams.Error);
                    TLUtils.WriteLine(serverDHParams.Error.ToString());
                }
            }
            else
            {

#if LOG_REGISTRATION
                    TLUtils.WriteLog("Stop ReqPQ with error " + error.ToString());
#endif
                result = new MTProtoResponse<Tuple<byte[], long?, long?>>(null, resPQ.Error);
                TLUtils.WriteLine(resPQ.Error.ToString());
            }

            return result;
        }

        private static TLPQInnerData GetInnerData(TLResPQ resPQ, TLInt256 newNonce, out TimeSpan calcTime, out Tuple<ulong, ulong> pqPair)
        {
            var pq = BitConverter.ToUInt64(resPQ.PQ.Reverse().ToArray(), 0);       //NOTE: add Reverse here
            TLUtils.WriteLine("pq: " + pq);

            var pqCalcTime = Stopwatch.StartNew();
            try
            {
                pqPair = Utils.GetFastPQ(pq);
                pqCalcTime.Stop();
                calcTime = pqCalcTime.Elapsed;
                TLUtils.WriteLineAtBegin("Pq Fast calculation time: " + pqCalcTime.Elapsed);
                TLUtils.WriteLine("p: " + pqPair.Item1);
                TLUtils.WriteLine("q: " + pqPair.Item2);
            }
            catch (Exception e)
            {
                pqCalcTime = Stopwatch.StartNew();
                pqPair = Utils.GetPQPollard(pq);
                pqCalcTime.Stop();
                calcTime = pqCalcTime.Elapsed;
                TLUtils.WriteLineAtBegin("Pq Pollard calculation time: " + pqCalcTime.Elapsed);
                TLUtils.WriteLine("p: " + pqPair.Item1);
                TLUtils.WriteLine("q: " + pqPair.Item2);
            }

            var p = pqPair.Item1.FromUInt64();
            var q = pqPair.Item2.FromUInt64();

            var innerData1 = new TLPQInnerData
            {
                NewNonce = newNonce,
                Nonce = resPQ.Nonce,
                P = p,
                Q = q,
                PQ = resPQ.PQ,
                ServerNonce = resPQ.ServerNonce
            };

            return innerData1;
        }

        private static byte[] GetEncryptedClientDHInnerData(TLClientDHInnerData clientDHInnerData, Tuple<byte[], byte[]> aesParams)
        {
            var random = new Random();
            var client_DH_inner_data = clientDHInnerData.ToArray();

            var client_DH_inner_dataWithHash = Utils.ComputeSHA1(client_DH_inner_data).Concat(client_DH_inner_data).ToArray();
            var addedBytesLength = 16 - (client_DH_inner_dataWithHash.Length % 16);
            if (addedBytesLength > 0 && addedBytesLength < 16)
            {
                var addedBytes = new byte[addedBytesLength];
                random.NextBytes(addedBytes);
                client_DH_inner_dataWithHash = client_DH_inner_dataWithHash.Concat(addedBytes).ToArray();
                //TLUtils.WriteLine(string.Format("Added {0} bytes", addedBytesLength));
            }

            var aesEncryptClientDHInnerDataWithHash = Utils.AesIge(client_DH_inner_dataWithHash, aesParams.Item1, aesParams.Item2, true);

            return aesEncryptClientDHInnerDataWithHash;
        }

        public static byte[] GetEncryptedInnerData(TLPQInnerData innerData)
        {
            var innerDataBytes = innerData.ToArray();
#if LOG_REGISTRATION
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("pq " + innerData.PQ.ToBytes().Length);
            sb.AppendLine("p " + innerData.P.ToBytes().Length);
            sb.AppendLine("q " + innerData.Q.ToBytes().Length);
            sb.AppendLine("nonce " + innerData.Nonce.ToBytes().Length);
            sb.AppendLine("serverNonce " + innerData.ServerNonce.ToBytes().Length);
            sb.AppendLine("newNonce " + innerData.NewNonce.ToBytes().Length);
            sb.AppendLine("innerData length " + innerDataBytes.Length);

            TLUtils.WriteLog(sb.ToString());
#endif

            var sha1 = Utils.ComputeSHA1(innerDataBytes);
            var dataWithHash = TLUtils.Combine(sha1, innerDataBytes); //116

#if LOG_REGISTRATION
            TLUtils.WriteLog("innerData+hash length " + dataWithHash.Length);
#endif

            var data255 = new byte[255];
            var random = new Random();
            random.NextBytes(data255);
            Array.Copy(dataWithHash, data255, dataWithHash.Length);


            var reverseRSABytes = Utils.GetRSABytes(data255);               // NOTE: remove Reverse here

            return reverseRSABytes;
        }

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


        // return big-endian authKey
        public static byte[] GetAuthKey(byte[] bBytes, byte[] g_aData, byte[] dhPrimeData)
        {
            var b = new BigInteger(bBytes.Reverse().Concat(new byte[] { 0x00 }).ToArray());
            var dhPrime = TLFactory.From<byte[]>(dhPrimeData).ToBigInteger(); // TODO: I don't like this.
            var g_a = TLFactory.From<byte[]>(g_aData).ToBigInteger();

            var authKey = BigInteger.ModPow(g_a, b, dhPrime).ToByteArray(); // little endian + (may be) zero last byte

            //remove last zero byte
            if (authKey[authKey.Length - 1] == 0x00)
            {
                authKey = authKey.SubArray(0, authKey.Length - 1);
            }

            authKey = authKey.Reverse().ToArray();

            if (authKey.Length > 256)
            {
#if DEBUG
                var authKeyInfo = new StringBuilder();
                authKeyInfo.AppendLine("auth_key length > 256: " + authKey.Length);
                authKeyInfo.AppendLine("g_a=" + g_a);
                authKeyInfo.AppendLine("b=" + b);
                authKeyInfo.AppendLine("dhPrime=" + dhPrime);
                Execute.ShowDebugMessage(authKeyInfo.ToString());
#endif

                var correctedAuth = new byte[256];
                Array.Copy(authKey, authKey.Length - 256, correctedAuth, 0, 256);
                authKey = correctedAuth;
            }
            else if (authKey.Length < 256)
            {
#if DEBUG
                var authKeyInfo = new StringBuilder();
                authKeyInfo.AppendLine("auth_key length < 256: " + authKey.Length);
                authKeyInfo.AppendLine("g_a=" + g_a);
                authKeyInfo.AppendLine("b=" + b);
                authKeyInfo.AppendLine("dhPrime=" + dhPrime);
                Execute.ShowDebugMessage(authKeyInfo.ToString());
#endif

                var correctedAuth = new byte[256];
                Array.Copy(authKey, 0, correctedAuth, 256 - authKey.Length, authKey.Length);
                for (var i = 0; i < 256 - authKey.Length; i++)
                {
                    authKey[i] = 0;
                }
                authKey = correctedAuth;
            }

            return authKey;
        }

        // b - big endian bytes
        // g - serialized data
        // dhPrime - serialized data
        // returns big-endian G_B
        public static byte[] GetGB(byte[] bData, int? gData, byte[] pString)
        {
            //var bBytes = new byte[256]; // big endian bytes
            //var random = new Random();
            //random.NextBytes(bBytes);

            var g = new BigInteger(gData.Value);
            var p = pString.ToBigInteger();
            var b = new BigInteger(bData.Reverse().Concat(new byte[] { 0x00 }).ToArray());

            var gb = BigInteger.ModPow(g, b, p).ToByteArray(); // little endian + (may be) zero last byte
            //remove last zero byte
            if (gb[gb.Length - 1] == 0x00)
            {
                gb = gb.SubArray(0, gb.Length - 1);
            }

            var length = gb.Length;
            var result = new byte[length];
            for (int i = 0; i < length; i++)
            {
                result[length - i - 1] = gb[i];
            }

            return result;
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
