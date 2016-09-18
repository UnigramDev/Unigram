using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLContainerTransportMessage : TLTransportMessageBase
    {
        public Int32 SeqNo { get; set; }
        public Int32 QueryLength { get; set; }
        public TLObject Query { get; set; }

        public TLContainerTransportMessage() { }
        public TLContainerTransportMessage(TLBinaryReader from, bool cache)
        {
            Read(from, cache);
        }

        public override TLType TypeId
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override void Read(TLBinaryReader from, bool cache = false)
        {
            MsgId = from.ReadInt64();
            SeqNo = from.ReadInt32();
            QueryLength = from.ReadInt32();
            Query = TLFactory.Read<TLObject>(from, (TLType)from.ReadInt32(), cache);
        }

        public override void Write(TLBinaryWriter to, bool cache = false)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new TLBinaryWriter(output))
                {
                    writer.WriteObject(Query, cache);
                    var buffer = output.ToArray();

                    to.Write(MsgId);
                    to.Write(SeqNo);
                    to.Write(buffer.Length);
                    to.Write(buffer);
                }
            }
        }
    }
}
