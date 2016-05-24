using System;
using System.Linq;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public class TLEncryptedTransportMessage : TLObject
    {
        public TLLong AuthKeyId { get; set; }
        public byte[] MsgKey { get; set; } //128 bit
        public byte[] Data { get; set; }

        public TLEncryptedTransportMessage Decrypt(byte[] authKey)
        {
            return Decrypt(this, authKey);
        }

        public static TLEncryptedTransportMessage Decrypt(TLEncryptedTransportMessage transportMessage, byte[] authKey)
        {
            var keyIV = Utils.GetDecryptKeyIV(authKey, transportMessage.MsgKey);
            transportMessage.Data = Utils.AesIge(transportMessage.Data, keyIV.Item1, keyIV.Item2, false);

            return transportMessage;
        }

        public TLEncryptedTransportMessage Encrypt(byte[] authKey)
        {
            return Encrypt(this, authKey);
        }

        public static TLEncryptedTransportMessage Encrypt(TLEncryptedTransportMessage transportMessage, byte[] authKey)
        {
            var random = new Random();
            
            var data = transportMessage.Data;

            var length = data.Length;
            var padding = 16 - (length % 16);
            byte[] paddingBytes = null;
            if (padding > 0 && padding < 16)
            {
                paddingBytes = new byte[padding];
                random.NextBytes(paddingBytes);
            }

            byte[] dataWithPadding = data;
            if (paddingBytes != null)
            {
                dataWithPadding = data.Concat(paddingBytes).ToArray();
            }


            var msgKey = TLUtils.GetMsgKey(data);
            var keyIV = Utils.GetEncryptKeyIV(authKey, msgKey);
            var encryptedData = Utils.AesIge(dataWithPadding, keyIV.Item1, keyIV.Item2, true);

            //TLUtils.WriteLine("--Compute auth key sha1--");
            var authKeyId = TLUtils.GenerateLongAuthKeyId(authKey);

            transportMessage.AuthKeyId = new TLLong(authKeyId);
            transportMessage.MsgKey = msgKey;
            transportMessage.Data = encryptedData;

            return transportMessage;
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {

            var response = new TLEncryptedTransportMessage();
            response.AuthKeyId = GetObject<TLLong>(bytes, ref position);
            //TLUtils.WriteLine("AuthKeyId: " + response.AuthKeyId);
            response.MsgKey = bytes.SubArray(position, 16);
            //TLUtils.WriteLine("MsgKey: " + BitConverter.ToString(response.MsgKey));


            TLUtils.WriteLine(string.Format("\n<<--Parse TLEncryptedTransportMessage AuthKeyId {0}, MsgKey {1}\n----------------------------", response.AuthKeyId, BitConverter.ToString(response.MsgKey)));
            //TLUtils.WriteLine("----------------------------");

            position += 16;
            response.Data = bytes.SubArray(position, bytes.Length - position);
            position = bytes.Length;
            return response;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                AuthKeyId.ToBytes(),
                MsgKey,
                Data);
        }
    }
}
