using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Org.BouncyCastle.Math;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL.Methods.Messages;

namespace Telegram.Api.TL
{
    public static partial class TLUtils
    {

        public static bool IsValidAction(TLObject obj)
        {
            var readHistoryAction = obj as TLMessagesReadHistory;
            if (readHistoryAction != null)
            {
                return true;
            }

            var sendMessageAction = obj as TLMessagesSendMessage;
            if (sendMessageAction != null)
            {
                return true;
            }

            var sendMediaAction = obj as TLMessagesSendMedia;
            if (sendMediaAction != null)
            {
                var mediaContact = sendMediaAction.Media as TLInputMediaContact;
                if (mediaContact != null)
                {
                    return true;
                }

                var mediaGeoPoint = sendMediaAction.Media as TLInputMediaGeoPoint;
                if (mediaGeoPoint != null)
                {
                    return true;
                }

                var mediaVenue = sendMediaAction.Media as TLInputMediaVenue;
                if (mediaVenue != null)
                {
                    return true;
                }
            }

            var forwardMessagesAction = obj as TLMessagesForwardMessages;
            if (forwardMessagesAction != null)
            {
                return true;
            }

            var forwardMessageAction = obj as TLMessagesForwardMessage;
            if (forwardMessageAction != null)
            {
                return true;
            }

            var startBotAction = obj as TLMessagesStartBot;
            if (startBotAction != null)
            {
                return true;
            }

            var sendEncryptedAction = obj as TLMessagesSendEncrypted;
            if (sendEncryptedAction != null)
            {
                return true;
            }

            var sendEncryptedFileAction = obj as TLMessagesSendEncryptedFile;
            if (sendEncryptedFileAction != null)
            {
                return true;
            }

            var sendEncryptedServiceAction = obj as TLMessagesSendEncryptedService;
            if (sendEncryptedServiceAction != null)
            {
                return true;
            }

            var readEncryptedHistoryAction = obj as TLMessagesReadEncryptedHistory;
            if (readEncryptedHistoryAction != null)
            {
                return true;
            }

            return false;
        }

        public static TLMessage GetShortMessage(int id, int fromId, TLPeerBase toId, int date, string message)
        {

#if LAYER_40
            var m = new TLMessage
            {
                Id = id,
                FromId = fromId,
                ToId = toId,
                IsOut = false,
                Date = date,
                Message = message,
                Media = new TLMessageMediaEmpty(),
                IsUnread = true,
            };

            if (m.FromId > 0) m.HasFromId = true;
            if (m.Media != null) m.HasMedia = true;
#else
            var m = new TLMessage
            {
                Id = id,
                FromId = fromId,
                ToId = toId,
                Out = false,
                _date = date,
                Message = message,
                _media = new TLMessageMediaEmpty()
            };
#endif
            return m;
        }

        public static TLMessage GetMessage(
            int fromId,
            TLPeerBase toId,
            TLMessageState state,
            bool outFlag,
            bool unreadFlag,
            int date,
            string message, 
            TLMessageMediaBase media,
            long randomId,
            int? replyToMsgId)
        {
#if LAYER_40
            var m = new TLMessage
            {
                FromId = fromId,
                ToId = toId,
                State = state,
                IsOut = outFlag,
                IsUnread = unreadFlag,
                Date = date,
                Message = message,
                Media = media,
                RandomId = randomId,
                ReplyToMsgId = replyToMsgId
            };
            if (m.FromId != null) m.HasFromId = true;
            if (m.Media != null) m.HasMedia = true;
            if (m.ReplyToMsgId != null) m.HasReplyToMsgId = true;
#else
            var m = new TLMessage
            {
                FromId = fromId,
                ToId = toId,
                _status = status,
                Out = outFlag,
                Unread = unreadFlag,
                _date = date,
                Message = message,
                _media = media,
                RandomId = randomId,
                ReplyToMsgId = replyToMsgId
            };
#endif

            return m;
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

        public static IList<int> GetPtsRange(ITLMultiPts multiPts)
        {
            var pts = multiPts.Pts;
            var ptsCount = multiPts.PtsCount;

            return GetPtsRange(pts, ptsCount);
        }

        public static IList<int> GetPtsRange(int pts, int ptsCount)
        {
            var ptsList = new List<int>(ptsCount);
            for (var i = ptsCount - 1; i >= 0; i--)
            {
                ptsList.Add(pts - i);
            }

            return ptsList;
        }

        // TODO: Secrets
//        public static bool IsDisplayedDecryptedMessage(TLDecryptedMessageBase message, bool displayEmpty = false)
//        {
//            if (message == null) return false;

//#if DEBUG
//            return true;
//#endif
//            return IsDisplayedDecryptedMessageInternal(message, displayEmpty);
//        }

        public static bool CheckPrime(byte[] prime, int g)
        {
            if (!(g >= 2 && g <= 7))
            {
                return false;
            }

            if (prime.Length != 256 || prime[0] <= 127)
            {
                return false;
            }

            var dhBI = new BigInteger(1, prime);

            if (g == 2)
            { // p mod 8 = 7 for g = 2;
                var res = dhBI.Mod(BigInteger.ValueOf(8));
                if (res.IntValue != 7)
                {
                    return false;
                }
            }
            else if (g == 3)
            { // p mod 3 = 2 for g = 3;
                var res = dhBI.Mod(BigInteger.ValueOf(3));
                if (res.IntValue != 2)
                {
                    return false;
                }
            }
            else if (g == 5)
            { // p mod 5 = 1 or 4 for g = 5;
                var res = dhBI.Mod(BigInteger.ValueOf(5));
                int val = res.IntValue;
                if (val != 1 && val != 4)
                {
                    return false;
                }
            }
            else if (g == 6)
            { // p mod 24 = 19 or 23 for g = 6;
                var res = dhBI.Mod(BigInteger.ValueOf(24));
                int val = res.IntValue;
                if (val != 19 && val != 23)
                {
                    return false;
                }
            }
            else if (g == 7)
            { // p mod 7 = 3, 5 or 6 for g = 7.
                var res = dhBI.Mod(BigInteger.ValueOf(7));
                int val = res.IntValue;
                if (val != 3 && val != 5 && val != 6)
                {
                    return false;
                }
            }

            var hex = BitConverter.ToString(prime).Replace("-", string.Empty).ToUpperInvariant();
            if (hex.Equals("C71CAEB9C6B1C9048E6C522F70F13F73980D40238E3E21C14934D037563D930F48198A0AA7C14058229493D22530F4DBFA336F6E0AC925139543AED44CCE7C3720FD51F69458705AC68CD4FE6B6B13ABDC9746512969328454F18FAF8C595F642477FE96BB2A941D5BCD1D4AC8CC49880708FA9B378E3C4F3A9060BEE67CF9A4A4A695811051907E162753B56B0F6B410DBA74D8A84B2A14B3144E0EF1284754FD17ED950D5965B4B9DD46582DB1178D169C6BC465B0D6FF9CA3928FEF5B9AE4E418FC15E83EBEA0F87FA9FF5EED70050DED2849F47BF959D956850CE929851F0D8115F635B105EE2E4E15D04B2454BF6F4FADF034B10403119CD8E3B92FCC5B"))
            {
                return true;
            }

            var dhBI2 = dhBI.Subtract(BigInteger.ValueOf(1)).Divide(BigInteger.ValueOf(2));
            return !(!dhBI.IsProbablePrime(30) || !dhBI2.IsProbablePrime(30));
        }


        public static bool CheckGaAndGb(byte[] ga, byte[] prime)
        {
            var g_a = new BigInteger(1, ga);
            var p = new BigInteger(1, prime);

            return !(g_a.CompareTo(BigInteger.ValueOf(1)) != 1 || g_a.CompareTo(p.Subtract(BigInteger.ValueOf(1))) != -1);
        }

        // TODO: Secrets
        //public static bool IsDisplayedDecryptedMessageInternal(TLDecryptedMessageBase message, bool displayEmpty = false)
        //{
        //    var serviceMessage = message as TLDecryptedMessageService;
        //    if (serviceMessage != null)
        //    {
        //        var emptyAction = serviceMessage.Action as TLDecryptedMessageActionEmpty;
        //        if (emptyAction != null)
        //        {
        //            if (displayEmpty)
        //            {
        //                return true;
        //            }

        //            return false;
        //        }

        //        var notifyLayerAction = serviceMessage.Action as TLDecryptedMessageActionNotifyLayer;
        //        if (notifyLayerAction != null)
        //        {
        //            return false;
        //        }

        //        var deleteMessagesAction = serviceMessage.Action as TLDecryptedMessageActionDeleteMessages;
        //        if (deleteMessagesAction != null)
        //        {
        //            return false;
        //        }

        //        var readMessagesAction = serviceMessage.Action as TLDecryptedMessageActionReadMessages;
        //        if (readMessagesAction != null)
        //        {
        //            return false;
        //        }

        //        var flushHistoryAction = serviceMessage.Action as TLDecryptedMessageActionFlushHistory;
        //        if (flushHistoryAction != null)
        //        {
        //            return false;
        //        }

        //        var resendAction = serviceMessage.Action as TLDecryptedMessageActionResend;
        //        if (resendAction != null)
        //        {
        //            return false;
        //        }

        //        var requestKey = serviceMessage.Action as TLDecryptedMessageActionRequestKey;
        //        if (requestKey != null)
        //        {
        //            return false;
        //        }

        //        var commitKey = serviceMessage.Action as TLDecryptedMessageActionCommitKey;
        //        if (commitKey != null)
        //        {
        //            return false;
        //        }

        //        var acceptKey = serviceMessage.Action as TLDecryptedMessageActionAcceptKey;
        //        if (acceptKey != null)
        //        {
        //            return false;
        //        }

        //        var noop = serviceMessage.Action as TLDecryptedMessageActionNoop;
        //        if (noop != null)
        //        {
        //            return false;
        //        }

        //        var abortKey = serviceMessage.Action as TLDecryptedMessageActionAbortKey;
        //        if (abortKey != null)
        //        {
        //            return false;
        //        }
        //    }

        //    return true;
        //}

        //public static int? GetOutSeqNo(int? currentUserId, TLEncryptedChat17 chat)
        //{
        //    var isAdmin = chat.AdminId.Value == currentUserId.Value;
        //    var seqNo = 2 * chat.RawOutSeqNo.Value + (isAdmin ? 1 : 0);

        //    return new int?(seqNo);
        //}

        //public static int? GetInSeqNo(int? currentUserId, TLEncryptedChat17 chat)
        //{
        //    var isAdmin = chat.AdminId.Value == currentUserId.Value;
        //    var seqNo = 2 * chat.RawInSeqNo.Value + (isAdmin ? 0 : 1);

        //    return new int?(seqNo);
        //}

        //public static string EncryptMessage(TLObject decryptedMessage, TLEncryptedChatCommon chat)
        //{
        //    var random = new Random();

        //    var key = chat.Key.Data;
        //    var keyHash = Utils.ComputeSHA1(key);
        //    var keyFingerprint = new long?(BitConverter.ToInt64(keyHash, 12));
        //    var decryptedBytes = decryptedMessage.ToBytes();
        //    var bytes = Combine(BitConverter.GetBytes(decryptedBytes.Length), decryptedBytes);
        //    var sha1Hash = Utils.ComputeSHA1(bytes);
        //    var msgKey = sha1Hash.SubArray(sha1Hash.Length - 16, 16);

        //    var padding = (bytes.Length % 16 == 0) ? 0 : (16 - (bytes.Length % 16));
        //    var paddingBytes = new byte[padding];
        //    random.NextBytes(paddingBytes);
        //    var bytesWithPadding = Combine(bytes, paddingBytes);

        //    var x = 0;
        //    var sha1_a = Utils.ComputeSHA1(Combine(msgKey, key.SubArray(x, 32)));
        //    var sha1_b = Utils.ComputeSHA1(Combine(key.SubArray(32 + x, 16), msgKey, key.SubArray(48 + x, 16)));
        //    var sha1_c = Utils.ComputeSHA1(Combine(key.SubArray(64 + x, 32), msgKey));
        //    var sha1_d = Utils.ComputeSHA1(Combine(msgKey, key.SubArray(96 + x, 32)));

        //    var aesKey = Combine(sha1_a.SubArray(0, 8), sha1_b.SubArray(8, 12), sha1_c.SubArray(4, 12));
        //    var aesIV = Combine(sha1_a.SubArray(8, 12), sha1_b.SubArray(0, 8), sha1_c.SubArray(16, 4), sha1_d.SubArray(0, 8));

        //    var encryptedBytes = Utils.AesIge(bytesWithPadding, aesKey, aesIV, true);

        //    var resultBytes = Combine(keyFingerprint.ToBytes(), msgKey, encryptedBytes);

        //    return TLString.FromBigEndianData(resultBytes);
        //}

        //public static TLDecryptedMessageBase DecryptMessage(string data, TLEncryptedChat chat, out bool commitChat)
        //{
        //    commitChat = false;

        //    var bytes = data.Data;

        //    var keyFingerprint = BitConverter.ToInt64(bytes, 0);
        //    var msgKey = bytes.SubArray(8, 16);
        //    var key = chat.Key.Data;
        //    var keyHash = Utils.ComputeSHA1(key);
        //    var calculatedKeyFingerprint = BitConverter.ToInt64(keyHash, keyHash.Length - 8);
            
        //    if (keyFingerprint != calculatedKeyFingerprint)
        //    {
        //        var chat20 = chat as TLEncryptedChat20;
        //        if (chat20 != null && chat20.PFS_Key != null)
        //        {
        //            var pfsKeyHash = Utils.ComputeSHA1(chat20.PFS_Key.Data);
        //            var pfsKeyFingerprint = BitConverter.ToInt64(pfsKeyHash, pfsKeyHash.Length - 8);
        //            if (pfsKeyFingerprint == keyFingerprint)
        //            {
        //                chat20.Key = chat20.PFS_Key;
        //                chat20.PFS_Key = null;
        //                chat20.PFS_KeyFingerprint = null;
        //                chat20.PFS_A = null;
        //                chat20.PFS_ExchangeId = null;
        //                commitChat = true;
        //            }
        //            else
        //            {
        //                return null;
        //            }
        //        }
        //        else
        //        {
        //            return null;
        //        }
        //    }

        //    var x = 0;
        //    var sha1_a = Utils.ComputeSHA1(Combine(msgKey, key.SubArray(x, 32)));
        //    var sha1_b = Utils.ComputeSHA1(Combine(key.SubArray(32 + x, 16), msgKey, key.SubArray(48 + x, 16)));
        //    var sha1_c = Utils.ComputeSHA1(Combine(key.SubArray(64 + x, 32), msgKey));
        //    var sha1_d = Utils.ComputeSHA1(Combine(msgKey, key.SubArray(96 + x, 32)));

        //    var aesKey = Combine(sha1_a.SubArray(0, 8), sha1_b.SubArray(8, 12), sha1_c.SubArray(4, 12));
        //    var aesIV = Combine(sha1_a.SubArray(8, 12), sha1_b.SubArray(0, 8), sha1_c.SubArray(16, 4), sha1_d.SubArray(0, 8));

        //    var encryptedBytes = bytes.SubArray(24, bytes.Length - 24);
        //    var decryptedBytes = Utils.AesIge(encryptedBytes, aesKey, aesIV, false);

        //    var length = BitConverter.ToInt32(decryptedBytes, 0);
        //    if (length <= 0 || (4 + length) > decryptedBytes.Length)
        //    {
        //        return null;
        //    }

        //    var calculatedMsgKey = Utils.ComputeSHA1(decryptedBytes.SubArray(0, 4 + length));
        //    for (var i = 0; i < 16; i++)
        //    {
        //        if (msgKey[i] != calculatedMsgKey[i + 4])
        //        {
        //            return null;
        //        }
        //    }

        //    var position = 4;
        //    var decryptedObject = TLObject.GetObject<TLObject>(decryptedBytes, ref position);
        //    var decryptedMessageLayer = decryptedObject as TLDecryptedMessageLayer;
        //    var decryptedMessageLayer17 = decryptedObject as TLDecryptedMessageLayer17;
        //    TLDecryptedMessageBase decryptedMessage = null;

        //    if (decryptedMessageLayer17 != null)
        //    {
        //        decryptedMessage = decryptedMessageLayer17.Message;
        //        var decryptedMessage17 = decryptedMessage as ISeqNo;
        //        if (decryptedMessage17 != null)
        //        {
        //            decryptedMessage17.InSeqNo = decryptedMessageLayer17.InSeqNo;
        //            decryptedMessage17.OutSeqNo = decryptedMessageLayer17.OutSeqNo;
        //        }
        //    }
        //    else if (decryptedMessageLayer != null)
        //    {
        //        decryptedMessage = decryptedMessageLayer.Message;
        //    }
        //    else if (decryptedObject is TLDecryptedMessageBase)
        //    {
        //        decryptedMessage = (TLDecryptedMessageBase)decryptedObject;
        //    }

        //    return decryptedMessage;
        //}

        public static T OpenObjectFromFile<T>(object syncRoot, string fileName)
            where T : class
        {
            try
            {
                lock (syncRoot)
                {
                    using (var fileStream = FileUtils.GetLocalFileStreamForRead(fileName))
                    {
                        if (fileStream.Length > 0)
                        {
                            var serializer = new DataContractSerializer(typeof(T));
                            return serializer.ReadObject(fileStream) as T;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                WriteLine("FILE ERROR: cannot read " + typeof(T) + " from file " + fileName, LogSeverity.Error);
                WriteException(e);
            }
            return default(T);
        }

        public static T OpenObjectFromMTProtoFile<T>(object syncRoot, string fileName)
            where T : TLObject
        {
            try
            {
                lock (syncRoot)
                {
                    using (var fileStream = FileUtils.GetLocalFileStreamForRead(fileName))
                    {
                        if (fileStream.Length > 0)
                        {
                            using (var from = new TLBinaryReader(fileStream))
                            {
                                return TLFactory.Read<T>(from);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                WriteLine("MTPROTO FILE ERROR: cannot read " + typeof(T) + " from file " + fileName, LogSeverity.Error);
                WriteException(e);
            }
            return default(T);
        }

        public static void SaveObjectToFile<T>(object syncRoot, string fileName, T data)
        {
            try
            {
                lock (syncRoot)
                {
                    using (var fileStream = FileUtils.GetLocalFileStreamForWrite(fileName))
                    {
                        var dcs = new DataContractSerializer(typeof(T));
                        dcs.WriteObject(fileStream, data);
                    }
                }
            }
            catch (Exception e)
            {
                WriteLine("FILE ERROR: cannot write " + typeof(T) + " to file " + fileName, LogSeverity.Error);
                WriteException(e);
            }
        }

        public static void SaveObjectToMTProtoFile<T>(object syncRoot, string fileName, T data) where T: TLObject
        {
            try
            {
                lock (syncRoot)
                {
                    FileUtils.SaveWithTempFile(fileName, data);
                }
            }
            catch (Exception e)
            {
                WriteLine("MTPROTO FILE ERROR: cannot write " + typeof(T) + " to file " + fileName, LogSeverity.Error);
                WriteException(e);
            }
        }

        // TODO: Secrets
        //public static TLPeerBase GetPeerFromMessage(TLDecryptedMessageBase message)
        //{
        //    TLPeerBase peer = null;
        //    var commonMessage = message;
        //    if (commonMessage != null)
        //    {
        //        if (commonMessage.ChatId != null)
        //        {
        //            peer = new TLPeerEncryptedChat{ Id = commonMessage.ChatId };
        //        }
        //    }
        //    else
        //    {
        //        WriteLine("Cannot get peer from non TLDecryptedMessage", LogSeverity.Error);
        //    }

        //    return peer;
        //}

        public static TLPeerBase GetPeerFromMessage(TLMessageBase message)
        {
            TLPeerBase peer = null;
            var commonMessage = message as TLMessage;
            if (commonMessage != null)
            {
                if (commonMessage.ToId is TLPeerChannel)
                {
                    peer = commonMessage.ToId;
                }
                else if (commonMessage.ToId is TLPeerChat)
                {
                    peer = commonMessage.ToId;
                }
                else
                {
                    if (commonMessage.IsOut)
                    {
                        peer = commonMessage.ToId;
                    }
                    else
                    {
                        peer = new TLPeerUser { Id = commonMessage.FromId.Value };
                    }
                }
            }
            else
            {
                WriteLine("Cannot get peer from non TLMessage", LogSeverity.Error);
            }

            return peer;
        }

        public static bool IsChannelMessage(TLMessageBase message, out TLPeerChannel channel)
        {
            var isChannel = false;
            channel = null;

            var messageCommon = message as TLMessage;
            if (messageCommon != null)
            {
                channel = messageCommon.ToId as TLPeerChannel;
                isChannel = channel != null;
            }

            return isChannel;
        }

        public static bool InsertItem<T>(IList<T> items, T item, Func<T, long> getField, Func<T, long> equalitysField = null)
            where T : TLObject
        {
            var fieldValue = getField(item);
            for (var i = 0; i < items.Count; i++)
            {
                if (getField(items[i]) > fieldValue)
                {
                    items.Insert(i, item);
                    return true;
                }
                if (getField(items[i]) == fieldValue
                    && equalitysField != null
                    && equalitysField(items[i]) == equalitysField(item))
                {
                    return false;
                }
            }

            items.Add(item);
            return true;
        }

        public static bool InsertItemByDesc<T>(IList<T> items, T item, Func<T, long> getField, Func<T, long> equalityField = null)
            where T : TLObject
        {
            var fieldValue = getField(item);
            for (var i = 0; i < items.Count; i++)
            {
                if (getField(items[i]) < fieldValue)
                {
                    items.Insert(i, item);
                    return true;
                }
                if (getField(items[i]) == fieldValue
                    && equalityField != null
                    && equalityField(items[i]) == equalityField(item))
                {
                    return false;
                }
            }

            items.Add(item);
            return true;
        }

        public static IEnumerable<T> FindInnerObjects<T>(TLTransportMessage obj)
            where T : TLObject
        {
            var result = obj.Query as T;
            if (result != null)
            {
                yield return (T)obj.Query;
            }
            else
            {
                var gzipData = obj.Query as TLGzipPacked;
                if (gzipData != null)
                {
                    result = gzipData.Query as T;
                    if (result != null)
                    {
                        yield return result;
                    }
                }

                var container = obj.Query as TLMsgContainer;
                if (container != null)
                {
                    foreach (var message in container.Messages)
                    {
                        result = message.Query as T;
                        if (result != null)
                        {
                            yield return (T)message.Query;
                        }

                        gzipData = message.Query as TLGzipPacked;
                        if (gzipData != null)
                        {
                            result = gzipData.Query as T;
                            if (result != null)
                            {
                                yield return result;
                            }
                        }
                    }
                }
            }
        }

        public static int InputPeerToId(TLInputPeerBase inputPeer, int? selfId)
        {
            var chat = inputPeer as TLInputPeerChat;
            if (chat != null)
            {
                return chat.ChatId;
            }

            var channel = inputPeer as TLInputPeerChannel;
            if (channel != null)
            {
                return channel.ChannelId;
            }

            // TODO: is this needed?
            //var contact = inputPeer as TLInputPeerContact;
            //if (contact != null)
            //{
            //    return contact.UserId.Value;
            //}

            //var foreign = inputPeer as TLInputPeerForeign;
            //if (foreign != null)
            //{
            //    return foreign.UserId.Value;
            //}

            var self = inputPeer as TLInputPeerSelf;
            if (self != null)
            {
                return selfId.Value;
            }

            return -1;
        }

        public static TLPeerBase InputPeerToPeer(TLInputPeerBase inputPeer, int selfId)
        {
            var channel = inputPeer as TLInputPeerChannel;
            if (channel != null)
            {
                return new TLPeerChannel { Id = channel.ChannelId };
            }

            // Broadcast are no more supported.
            //var broadcast = inputPeer as TLInputPeerBroadcast;
            //if (broadcast != null)
            //{
            //    return new TLPeerBroadcast { Id = broadcast.ChatId };
            //}

            var chat = inputPeer as TLInputPeerChat;
            if (chat != null)
            {
                return new TLPeerChat { Id = chat.ChatId };
            }

            var user = inputPeer as TLInputPeerUser;
            if (user != null)
            {
                return new TLPeerUser { Id = user.UserId };
            }

            var self = inputPeer as TLInputPeerSelf;
            if (self != null)
            {
                return new TLPeerUser { Id = selfId };
            }

            return null;
        }


        public static int MergeItemsDesc<T>(Func<T, int> dateIndexFunc, IList<T> current, IList<T> updated, int offset, int maxId, int count, out IList<T> removedItems, Func<T, int> indexFunc, Func<T, bool> skipTailFunc)
        {
            removedItems = new List<T>();

            var currentIndex = 0;
            var updatedIndex = 0;

            var index = new Dictionary<int, int>();
            foreach (var item in current)
            {
                var id = indexFunc(item);
                if (id > 0)
                {
                    index[id] = id;
                }
            }


            //skip just added or sending items
            while (updatedIndex < updated.Count
                && currentIndex < current.Count
                && dateIndexFunc(updated[updatedIndex]) < dateIndexFunc(current[currentIndex]))
            {
                currentIndex++;
            }

            // insert before current items
            while (updatedIndex < updated.Count
                && (current.Count < currentIndex 
                    || (currentIndex < current.Count && dateIndexFunc(updated[updatedIndex]) > dateIndexFunc(current[currentIndex]))))
            {
                if (dateIndexFunc(current[currentIndex]) == 0)
                {
                    currentIndex++;
                    continue;
                }
                if (index.ContainsKey(indexFunc(updated[updatedIndex])))
                {
                    updatedIndex++;
                    continue;
                } 
                current.Insert(currentIndex, updated[updatedIndex]);
                updatedIndex++;
                currentIndex++;
            }

            // update existing items
            if (updatedIndex < updated.Count)
            {
                for (; currentIndex < current.Count; currentIndex++)
                {
                    if (indexFunc != null
                        && indexFunc(current[currentIndex]) == 0)
                    {
                        continue;
                    }

                    for (; updatedIndex < updated.Count; updatedIndex++)
                    {
                        // missing item at current list
                        if (dateIndexFunc(updated[updatedIndex]) > dateIndexFunc(current[currentIndex]))
                        {
                            current.Insert(currentIndex, updated[updatedIndex]);
                            updatedIndex++;
                            break;
                        }
                        // equal item
                        if (dateIndexFunc(updated[updatedIndex]) == dateIndexFunc(current[currentIndex]))
                        {
                            updatedIndex++;
                            break;
                        }
                        // deleted item
                        if (dateIndexFunc(updated[updatedIndex]) < dateIndexFunc(current[currentIndex]))
                        {
                            var removedItem = current[currentIndex];
                            removedItems.Add(removedItem);
                            current.RemoveAt(currentIndex);
                            currentIndex--;
                            break;
                        }
                    }

                    // at the end of updated list
                    if (updatedIndex == updated.Count)
                    {
                        currentIndex++;
                        break;
                    }
                }
            }


            // all other items were deleted
            if (updated.Count > 0 && updated.Count < count && current.Count != currentIndex)
            {
                for (var i = current.Count - 1; i >= updatedIndex; i--)
                {
                    if (skipTailFunc != null && skipTailFunc(current[i]))
                    {
                        continue;
                    }
                    current.RemoveAt(i);
                }
                return currentIndex - 1;
            }

            // add after current items
            while (updatedIndex < updated.Count)
            {
                current.Add(updated[updatedIndex]);
                updatedIndex++;
                currentIndex++;
            }

            return currentIndex - 1;
        }

        public static int DateToUniversalTimeTLInt(long clientDelta, DateTime date)
        {
            clientDelta = MTProtoService.Current.ClientTicksDelta;

            var unixTime = (long)(Utils.DateTimeToUnixTimestamp(date) * 4294967296) + clientDelta; //int * 2^32 + clientDelta

            return (int)(unixTime / 4294967296);
        }

        public static int ToTLInt(DateTime date)
        {
            var unixTime = (long)(Utils.DateTimeToUnixTimestamp(date) * 4294967296); //int * 2^32 + clientDelta

            return (int)(unixTime / 4294967296);
        }

        public static int? ToTLInt(byte[] value)
        {
            try
            {
                if (value.Length == 0)
                {
                    return null;
                }

                var intValue = Convert.ToInt32(value);
                return intValue;
            }
            catch (Exception ex)
            {

            }

            return null;
        }

        public static byte[] GenerateAuthKeyId(byte[] authKey)
        {
            var authKeyHash = Utils.ComputeSHA1(authKey);
            var authKeyId = authKeyHash.SubArray(12, 8);

            return authKeyId;
        }

        public static long GenerateLongAuthKeyId(byte[] authKey)
        {
            var authKeyHash = Utils.ComputeSHA1(authKey);
            var authKeyId = authKeyHash.SubArray(12, 8);

            return BitConverter.ToInt64(authKeyId, 0);
        }

        public static byte[] GetMsgKey(byte[] saveDeveloperInfoData)
        {
            var bytes = Utils.ComputeSHA1(saveDeveloperInfoData);
            var last16Bytes = bytes.SubArray(4, 16);

            return last16Bytes;
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

        public static string MessageIdString(byte[] bytes)
        {
            var ticks = BitConverter.ToInt64(bytes, 0);
            var date = Utils.UnixTimestampToDateTime(ticks >> 32);

            return BitConverter.ToString(bytes) + " " 
                + ticks + "%4=" + ticks % 4 + " " 
                + date;
        }

        public static string MessageIdString(long messageId)
        {
            var bytes = BitConverter.GetBytes(messageId);
            var ticks = BitConverter.ToInt64(bytes, 0);
            var date = Utils.UnixTimestampToDateTime(ticks >> 32);

            return BitConverter.ToString(bytes) + " "
                + ticks + "%4=" + ticks % 4 + " "
                + date;
        }

        public static string MessageIdString(int? messageId)
        {
            var ticks = messageId.Value;
            var date = Utils.UnixTimestampToDateTime(ticks >> 32);

            return BitConverter.ToString(BitConverter.GetBytes(messageId.Value)) + " "
                + ticks + "%4=" + ticks % 4 + " "
                + date;
        }

        public static DateTime ToDateTime(int? date)
        {
            var ticks = date.Value;
            return Utils.UnixTimestampToDateTime(ticks >> 32);
        }

        public static void ThrowNotSupportedException(this byte[] bytes, string objectType)
        {
            throw new NotSupportedException(String.Format("Not supported {0} signature: {1}", objectType, BitConverter.ToString(bytes.SubArray(0, 4))));
        }

        public static void ThrowNotSupportedException(this byte[] bytes, int position, string objectType)
        {
            throw new NotSupportedException(String.Format("Not supported {0} signature: {1}", objectType, BitConverter.ToString(bytes.SubArray(position, position + 4))));
        }

        public static void ThrowExceptionIfIncorrect(this byte[] bytes, ref int position, uint signature)
        {
            //if (!bytes.SubArray(position, 4).StartsWith(signature))
            //{
            //    throw new ArgumentException(String.Format("Incorrect signature: actual - {1}, expected - {0}", SignatureToBytesString(signature), BitConverter.ToString(bytes.SubArray(0, 4))));
            //}
            position += 4;
        }

        public static void ThrowExceptionIfIncorrect(this byte[] bytes, ref int position, string signature)
        {
            //if (!bytes.SubArray(position, 4).StartsWith(signature))
            //{
            //    throw new ArgumentException(String.Format("Incorrect signature: actual - {1}, expected - {0}", SignatureToBytesString(signature), BitConverter.ToString(bytes.SubArray(0, 4))));
            //}
            position += 4;
        }

        private static bool StartsWith(this byte[] array, byte[] startArray)
        {
            for (var i = 0; i < startArray.Length; i++)
            {
                if (array[i] != startArray[i]) return false;
            }
            return true;
        }

        private static bool StartsWith(this byte[] array, int position, byte[] startArray)
        {
            for (var i = 0; i < startArray.Length; i++)
            {
                if (array[position + i] != startArray[i]) return false;
            }
            return true;
        }

        public static bool StartsWith(this byte[] bytes, uint signature)
        {
            var sign = BitConverter.ToUInt32(bytes, 0);

            return sign == signature;
        }

        public static bool StartsWith(this Stream input, uint signature)
        {
            var bytes = new byte[4];
            input.Read(bytes, 0, 4);
            var sign = BitConverter.ToUInt32(bytes, 0);

            return sign == signature;
        }

        public static bool StartsWith(this byte[] bytes, string signature)
        {
            if (signature[0] != '#') throw new ArgumentException("Incorrect first symbol of signature: expexted - #, actual - " + signature[0]);

            var signatureBytes = SignatureToBytes(signature);

            return bytes.StartsWith(signatureBytes);
        }

        public static bool StartsWith(this byte[] bytes, int position, uint signature)
        {
            var sign = BitConverter.ToUInt32(bytes, position);

            return sign == signature;
        }

        public static bool StartsWith(this byte[] bytes, int position, string signature)
        {
            if (signature[0] != '#') throw new ArgumentException("Incorrect first symbol of signature: expexted - #, actual - " + signature[0]);

            var signatureBytes = SignatureToBytes(signature);

            return bytes.StartsWith(position, signatureBytes);
        }

        public static string SignatureToBytesString(string signature)
        {
            if (signature[0] != '#') throw new ArgumentException("Incorrect first symbol of signature: expexted - #, actual - " + signature[0]);

            return BitConverter.ToString(SignatureToBytes(signature));
        }

        public static byte[] SignatureToBytes(uint signature)
        {
            return BitConverter.GetBytes(signature);
        }

        public static byte[] SignatureToBytes(string signature)
        {
            if (signature[0]!= '#') throw new ArgumentException("Incorrect first symbol of signature: expexted - #, actual - " + signature[0]);
            
            var bytesString = 
                signature.Length % 2 == 0?
                new string(signature.Replace("#", "0").ToArray()):
                new string(signature.Replace("#", String.Empty).ToArray());

            var bytes = Utils.StringToByteArray(bytesString);
            Array.Reverse(bytes);
            return bytes;
        }

    }
}
