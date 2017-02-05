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
        public TLContainerTransportMessage(TLBinaryReader from)
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
            MsgId = from.ReadInt64();
            SeqNo = from.ReadInt32();
            QueryLength = from.ReadInt32();
            Query = TLFactory.Read<TLObject>(from, (TLType)from.ReadInt32());
        }

        public override void Write(TLBinaryWriter to)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new TLBinaryWriter(output))
                {
                    writer.WriteObject(Query);
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
