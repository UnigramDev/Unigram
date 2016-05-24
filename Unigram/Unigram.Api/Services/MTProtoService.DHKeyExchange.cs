﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using Org.BouncyCastle.Security;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.DHKeyExchange;

namespace Telegram.Api.Services
{
    public class AuthKeyItem
    {
        public long AutkKeyId { get; set; }
        public byte[] AuthKey { get; set; }
    }

    public partial class MTProtoService
    {
        /// <summary>
        /// Список имеющихся ключей авторизации
        /// </summary>
        private static readonly Dictionary<long, AuthKeyItem> _authKeys = new Dictionary<long, AuthKeyItem>(); 

        private static readonly object _authKeysRoot = new object();

        private void ReqPQAsync(TLInt128 nonce, Action<TLResPQ> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLReqPQ{ Nonce = nonce };

            SendNonEncryptedMessage("req_pq", obj, callback, faultCallback);
        }

        private void ReqDHParamsAsync(TLInt128 nonce, TLInt128 serverNonce, TLString p, TLString q, TLLong publicKeyFingerprint, TLString encryptedData, Action<TLServerDHParamsBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLReqDHParams { Nonce = nonce, ServerNonce = serverNonce, P = p, Q = q, PublicKeyFingerprint = publicKeyFingerprint, EncryptedData = encryptedData };

            SendNonEncryptedMessage("req_DH_params", obj, callback, faultCallback);
        }

        public void SetClientDHParamsAsync(TLInt128 nonce, TLInt128 serverNonce, TLString encryptedData, Action<TLDHGenBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLSetClientDHParams { Nonce = nonce, ServerNonce = serverNonce, EncryptedData = encryptedData };

            SendNonEncryptedMessage("set_client_DH_params", obj, callback, faultCallback);
        }

        private TimeSpan _authTimeElapsed;

        public void InitAsync(Action<WindowsPhone.Tuple<byte[], TLLong, TLLong>> callback, Action<TLRPCError> faultCallback = null)
        {
            var authTime = Stopwatch.StartNew();
            var newNonce = TLInt256.Random();

#if LOG_REGISTRATION
            TLUtils.WriteLog("Start ReqPQ");
#endif
            var nonce = TLInt128.Random();
            ReqPQAsync(nonce,
                resPQ =>
                {
                    var serverNonce = resPQ.ServerNonce;
                    if (!TLUtils.ByteArraysEqual(nonce.Value, resPQ.Nonce.Value))
                    {
                        var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect nonce") };
#if LOG_REGISTRATION
                        TLUtils.WriteLog("Stop ReqPQ with error " + error);
#endif

                        if (faultCallback != null) faultCallback(error);
                        TLUtils.WriteLine(error.ToString());
                    }

#if LOG_REGISTRATION
                    TLUtils.WriteLog("Stop ReqPQ");
#endif
                    TimeSpan calcTime;
                    WindowsPhone.Tuple<ulong, ulong> pqPair;
                    var innerData = GetInnerData(resPQ, newNonce, out calcTime, out pqPair);
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
                    ReqDHParamsAsync(
                        resPQ.Nonce,
                        resPQ.ServerNonce,
                        innerData.P,
                        innerData.Q,
                        resPQ.ServerPublicKeyFingerprints[0],
                        encryptedInnerData,
                        serverDHParams =>
                        {
                            if (!TLUtils.ByteArraysEqual(nonce.Value, serverDHParams.Nonce.Value))
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect nonce") };
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());
                            }
                            if (!TLUtils.ByteArraysEqual(serverNonce.Value, serverDHParams.ServerNonce.Value))
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect server_nonce") };
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());
                            }

#if LOG_REGISTRATION
                            TLUtils.WriteLog("Stop ReqDHParams");
#endif
                            var random = new SecureRandom();

                            var serverDHParamsOk = serverDHParams as TLServerDHParamsOk;
                            if (serverDHParamsOk == null)
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("Incorrect serverDHParams " + serverDHParams.GetType()) };
                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());
#if LOG_REGISTRATION
                            
                                TLUtils.WriteLog("ServerDHParams " + serverDHParams);  
#endif
                                return;
                            }

                            var aesParams = GetAesKeyIV(resPQ.ServerNonce.ToBytes(), newNonce.ToBytes());

                            var decryptedAnswerWithHash = Utils.AesIge(serverDHParamsOk.EncryptedAnswer.Data, aesParams.Item1, aesParams.Item2, false);

                            var position = 0;
                            var serverDHInnerData = (TLServerDHInnerData)new TLServerDHInnerData().FromBytes(decryptedAnswerWithHash.Skip(20).ToArray(), ref position);

                            var sha1 = Utils.ComputeSHA1(serverDHInnerData.ToBytes());
                            if (!TLUtils.ByteArraysEqual(sha1, decryptedAnswerWithHash.Take(20).ToArray()))
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect sha1 TLServerDHInnerData") };
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());    
                            }

                            if (!TLUtils.CheckPrime(serverDHInnerData.DHPrime.Data, serverDHInnerData.G.Value))
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect (p, q) pair") };
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());                  
                            }

                            if (!TLUtils.CheckGaAndGb(serverDHInnerData.GA.Data, serverDHInnerData.DHPrime.Data))
                            {
                                var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect g_a") };
#if LOG_REGISTRATION
                                TLUtils.WriteLog("Stop ReqDHParams with error " + error);
#endif

                                if (faultCallback != null) faultCallback(error);
                                TLUtils.WriteLine(error.ToString());
                            }

                            var bBytes = new byte[256]; //big endian B
                            random.NextBytes(bBytes);

                            var gbBytes = GetGB(bBytes, serverDHInnerData.G, serverDHInnerData.DHPrime);

                            var clientDHInnerData = new TLClientDHInnerData
                            {
                                Nonce = resPQ.Nonce,
                                ServerNonce = resPQ.ServerNonce,
                                RetryId = new TLLong(0),
                                GB = TLString.FromBigEndianData(gbBytes)
                            };

                            var encryptedClientDHInnerData = GetEncryptedClientDHInnerData(clientDHInnerData, aesParams);
#if LOG_REGISTRATION                 
                            TLUtils.WriteLog("Start SetClientDHParams");  
#endif
                            SetClientDHParamsAsync(resPQ.Nonce, resPQ.ServerNonce, encryptedClientDHInnerData,
                                dhGen =>
                                {
                                    if (!TLUtils.ByteArraysEqual(nonce.Value, dhGen.Nonce.Value))
                                    {
                                        var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect nonce") };
#if LOG_REGISTRATION
                                        TLUtils.WriteLog("Stop SetClientDHParams with error " + error);
#endif

                                        if (faultCallback != null) faultCallback(error);
                                        TLUtils.WriteLine(error.ToString());
                                    }
                                    if (!TLUtils.ByteArraysEqual(serverNonce.Value, dhGen.ServerNonce.Value))
                                    {
                                        var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("incorrect server_nonce") };
#if LOG_REGISTRATION
                                        TLUtils.WriteLog("Stop SetClientDHParams with error " + error);
#endif

                                        if (faultCallback != null) faultCallback(error);
                                        TLUtils.WriteLine(error.ToString());
                                    }

                                    var dhGenOk = dhGen as TLDHGenOk;
                                    if (dhGenOk == null)
                                    {
                                        var error = new TLRPCError { Code = new TLInt(404), Message = new TLString("Incorrect dhGen " + dhGen.GetType()) };
                                        if (faultCallback != null) faultCallback(error);
                                        TLUtils.WriteLine(error.ToString());
#if LOG_REGISTRATION
                                        TLUtils.WriteLog("DHGen result " + serverDHParams);
#endif
                                        return;
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
                                    var salt = GetSalt(newNonce.ToBytes(), resPQ.ServerNonce.ToBytes());
                                    var sessionId = new byte[8];
                                    random.NextBytes(sessionId);

                                    TLUtils.WriteLine("Salt " + BitConverter.ToInt64(salt, 0) + " (" + BitConverter.ToString(salt) + ")");
                                    TLUtils.WriteLine("Session id " + BitConverter.ToInt64(sessionId, 0) + " (" + BitConverter.ToString(sessionId) + ")");

                                    callback(new WindowsPhone.Tuple<byte[], TLLong, TLLong>(authKey, new TLLong(BitConverter.ToInt64(salt, 0)), new TLLong(BitConverter.ToInt64(sessionId, 0))));
                                },
                                error =>
                                {
#if LOG_REGISTRATION
                                    TLUtils.WriteLog("Stop SetClientDHParams with error " + error.ToString());
#endif
                                    if (faultCallback != null) faultCallback(error);
                                    TLUtils.WriteLine(error.ToString());
                                });
                        },
                        error =>
                        {
#if LOG_REGISTRATION
                            TLUtils.WriteLog("Stop ReqDHParams with error " + error.ToString());
#endif
                            if (faultCallback != null) faultCallback(error);
                            TLUtils.WriteLine(error.ToString());
                        });
                },
                error =>
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog("Stop ReqPQ with error " + error.ToString());
#endif
                    if (faultCallback != null) faultCallback(error);
                    TLUtils.WriteLine(error.ToString());
                });
        }

        private static TLPQInnerData GetInnerData(TLResPQ resPQ, TLInt256 newNonce, out TimeSpan calcTime, out WindowsPhone.Tuple<ulong, ulong> pqPair)
        {
            var pq = BitConverter.ToUInt64(resPQ.PQ.Data.Reverse().ToArray(), 0);       //NOTE: add Reverse here
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

            var p = TLString.FromUInt64(pqPair.Item1);
            var q = TLString.FromUInt64(pqPair.Item2);

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

        private static TLString GetEncryptedClientDHInnerData(TLClientDHInnerData clientDHInnerData, WindowsPhone.Tuple<byte[], byte[]> aesParams)
        {
            var random = new Random();
            var client_DH_inner_data = clientDHInnerData.ToBytes();

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

            return TLString.FromBigEndianData(aesEncryptClientDHInnerDataWithHash);
        }

        public static TLString GetEncryptedInnerData(TLPQInnerData innerData)
        {
            var innerDataBytes = innerData.ToBytes();
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

            var encryptedData = new TLString { Data = reverseRSABytes };

            return encryptedData;
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
            int position = 0;
            var b = new BigInteger(bBytes.Reverse().Concat(new byte[] { 0x00 }).ToArray());
            var dhPrime = TLObject.GetObject<TLString>(dhPrimeData, ref position).ToBigInteger();
            position = 0;
            var g_a = TLObject.GetObject<TLString>(g_aData, ref position).ToBigInteger();

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
        public static byte[] GetGB(byte[] bData, TLInt gData, TLString pString)
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

        public static WindowsPhone.Tuple<byte[], byte[]> GetAesKeyIV(byte[] serverNonce, byte[] newNonce)
        {
            var newNonceServerNonce = newNonce.Concat(serverNonce).ToArray();
            var serverNonceNewNonce = serverNonce.Concat(newNonce).ToArray();
            var key = Utils.ComputeSHA1(newNonceServerNonce)
                .Concat(Utils.ComputeSHA1(serverNonceNewNonce).SubArray(0, 12));
            var im = Utils.ComputeSHA1(serverNonceNewNonce).SubArray(12, 8)
                .Concat(Utils.ComputeSHA1(newNonce.Concat(newNonce).ToArray()))
                .Concat(newNonce.SubArray(0, 4));

            return new WindowsPhone.Tuple<byte[], byte[]>(key.ToArray(), im.ToArray());
        }
    }
}
