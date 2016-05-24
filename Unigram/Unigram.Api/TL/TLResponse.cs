using System;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public class TLResponse
    {
        public byte[] AuthKeyId { get; set; }

        public byte[] MessageKey { get; set; }

        public byte[] EncryptedData { get; set; }

        public byte[] DecryptedData { get; set; }

        public byte[] Salt { get; set; }

        public byte[] SessionId { get; set; }

        public TLLong MessageId { get; set; }

        public TLInt SequenceNumber { get; set; }

        public Int32 MessageLength { get; set; }

        public byte[] MessageData { get; set; }

        public TLObject Data { get; set; }

        public static TLResponse Parse(byte[] bytes, byte[] authKey)
        {
            TLUtils.WriteLine("-------------------");
            TLUtils.WriteLine("--Parse response --");
            TLUtils.WriteLine("-------------------");


            int position = 0;
            var response = new TLResponse();
            response.AuthKeyId = bytes.SubArray(0, 8);
            TLUtils.WriteLine("AuthKeyId: " + BitConverter.ToString(response.AuthKeyId));
            response.MessageKey = bytes.SubArray(8, 16);
            TLUtils.WriteLine("MessageKey: " + BitConverter.ToString(response.MessageKey));

            response.EncryptedData = bytes.SubArray(24, bytes.Length - 24);
            //TLUtils.WriteLine("Encrypted data: " + BitConverter.ToString(response.Data));

            var keyIV = Utils.GetDecryptKeyIV(authKey, response.MessageKey);

            response.DecryptedData = Utils.AesIge(response.EncryptedData, keyIV.Item1, keyIV.Item2, false);
            //TLUtils.WriteLine("Decrypted data: " + BitConverter.ToString(response.DecryptedData));
            
            response.Salt = response.DecryptedData.SubArray(0, 8);
            TLUtils.WriteLine("Salt: " + BitConverter.ToString(response.Salt));

            response.SessionId = response.DecryptedData.SubArray(8, 8);
            TLUtils.WriteLine("SessionId: " + BitConverter.ToString(response.SessionId));

            position = 0;
            response.MessageId = TLObject.GetObject<TLLong>(response.DecryptedData.SubArray(16, 8), ref position);
            TLUtils.WriteLine("<-MESSAGEID: " + TLUtils.MessageIdString(response.MessageId));

            position = 0;
            response.SequenceNumber = TLObject.GetObject<TLInt>(response.DecryptedData.SubArray(24, 4), ref position);
            TLUtils.WriteLine("  SEQUENCENUMBER: " + response.SequenceNumber);

            response.MessageLength = BitConverter.ToInt32(response.DecryptedData.SubArray(28, 4), 0);
            TLUtils.WriteLine("MessageLength: " + response.MessageLength);

            response.MessageData = response.DecryptedData.SubArray(32, response.MessageLength);
            TLUtils.WriteLine("MessageData: " + BitConverter.ToString(response.MessageData));

            position = 0;
            response.Data = TLObject.GetObject<TLObject>(response.MessageData, ref position);

            return response;
        }
    }
}