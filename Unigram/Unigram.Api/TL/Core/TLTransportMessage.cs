using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLTransportMessage : TLContainerTransportMessage
    {
        public Int64 Salt { get; set; }
        public Int64 SessionId { get; set; }

        public TLTransportMessage() { }
        public TLTransportMessage(TLBinaryReader from)
        {
            Read(from);
        }

        public override void Read(TLBinaryReader from)
        {
            Salt = from.ReadInt64();
            SessionId = from.ReadInt64();
            base.Read(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new TLBinaryWriter(output))
                {
                    writer.WriteObject(Query);
                    var buffer = output.ToArray();

                    to.Write(Salt);
                    to.Write(SessionId);
                    to.Write(MsgId);
                    to.Write(SeqNo);
                    to.Write(buffer.Length);
                    to.Write(buffer);
                }
            }
        }
    }
}
