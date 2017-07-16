using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLNonEncryptedTransportMessage : TLTransportMessageBase
    {
        public Int64 AuthKeyId;
        public TLObject Query;

        public TLNonEncryptedTransportMessage() { }
        public TLNonEncryptedTransportMessage(TLBinaryReader from)
        {
            Read(from);
        }

        public override TLType TypeId
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override void Read(TLBinaryReader from)
        {
            AuthKeyId = from.ReadInt64();
            MsgId = from.ReadInt64();

            var length = from.ReadInt32();
            var innerType = (TLType)from.ReadInt32();
            Query = TLFactory.Read<TLObject>(from, innerType);
            //Query = TLFactory.Read<TLObject>(from, (TLType)from.ReadInt32());
        }

        public override void Write(TLBinaryWriter to)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new TLBinaryWriter(output))
                {
                    //writer.Write((uint)Object.TypeId);
                    writer.WriteObject(Query);
                    var buffer = output.ToArray();

                    to.Write(AuthKeyId);
                    to.Write(MsgId);
                    to.Write(buffer.Length);
                    to.Write(buffer);
                }
            }
        }
    }
}
