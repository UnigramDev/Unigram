using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public class TLEncryptedTransportMessage : TLTransportMessageBase
    {
        public Int64 AuthKeyId { get; set; }

        public byte[] MsgKey { get; set; }

        public TLContainerTransportMessage Query;

        public TLEncryptedTransportMessage() { }
        public TLEncryptedTransportMessage(TLBinaryReader from, byte[] authKey)
        {
            Read(from, authKey);
        }

        public void Read(TLBinaryReader from, byte[] authKey)
        {
            AuthKeyId = from.ReadInt64();
            MsgKey = from.ReadBytes(16);

            var data1 = from.ReadBytes((int)from.BaseStream.Length - (int)from.BaseStream.Position);

            var decryptKeyIV = Utils.GetDecryptKeyIV(authKey, MsgKey);
            var data2 = Utils.AesIge(data1, decryptKeyIV.Item1, decryptKeyIV.Item2, false);

            using (var reader = new TLBinaryReader(data2))
            {
                Query = new TLTransportMessage();
                Query.Read(reader);
            }

            //from.ReadUInt64();
            //MsgId = from.ReadUInt64();

            //var length = from.ReadUInt32();
            //var innerType = (TLType)from.ReadUInt32();
            //Inner = TLFactory.Read<T>(from, innerType);
        }

        public void Write(TLBinaryWriter to, byte[] authKey)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new TLBinaryWriter(output))
                {
                    writer.WriteObject(Query);
                    var buffer = output.ToArray();

                    var random = new Random();
                    var length = buffer.Length;
                    int num2 = 16 - length % 16;
                    byte[] array = null;
                    if (num2 > 0 && num2 < 16)
                    {
                        array = new byte[num2];
                        random.NextBytes(array);
                    }
                    byte[] data2 = buffer;
                    if (array != null)
                    {
                        data2 = buffer.Concat(array).ToArray<byte>();
                    }

                    var msgKey = TLUtils.GetMsgKey(buffer);
                    var encryptKeyIV = Utils.GetEncryptKeyIV(authKey, msgKey);
                    var data3 = Utils.AesIge(data2, encryptKeyIV.Item1, encryptKeyIV.Item2, true);

                    AuthKeyId = TLUtils.GenerateLongAuthKeyId(authKey);
                    MsgKey = msgKey;
                    var Data = data3;

                    to.Write(AuthKeyId);
                    to.Write(MsgKey);
                    to.Write(Data);
                }
            }
        }

        private byte[] _encrypted;

        public TLEncryptedTransportMessage Encrypt(byte[] authKey)
        {
            using (var stream = new MemoryStream())
            {
                using (var to = new TLBinaryWriter(stream))
                {
                    Write(to, authKey);
                }
                _encrypted = stream.ToArray();
            }

            return this;
        }

        public override void Read(TLBinaryReader from)
        {
            AuthKeyId = from.ReadInt64();
            MsgKey = from.ReadBytes(16);
        }

        public override void Write(TLBinaryWriter to)
        {
            to.Write(_encrypted);
        }
    }
}
