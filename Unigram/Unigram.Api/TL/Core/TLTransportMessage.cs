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
        public TLTransportMessage(TLBinaryReader from, TLType type = TLType.AccountAuthorizations)
        {
            Read(from, type);
        }

        public override void Read(TLBinaryReader from, TLType type = TLType.AccountAuthorizations)
        {
            Salt = from.ReadInt64();
            SessionId = from.ReadInt64();
            base.Read(from, type);
        }

        public override void Write(TLBinaryWriter to)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new TLBinaryWriter(output))
                {
                    Query.Write(writer);
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

        public IEnumerable<T> FindInnerObjects<T>() where T : TLObject
        {
            if (Query is T)
            {
                yield return (T)Query;
            }
            else
            {
                var packed = Query as TLGzipPacked;
                if (packed != null)
                {
                    if (packed.Query is T)
                    {
                        yield return (T)packed.Query;
                    }
                }

                var container = Query as TLMessageContainer;
                if (container != null)
                {
                    foreach (var message in container.Messages)
                    {
                        if (message.Query is T)
                        {
                            yield return (T)message.Query;
                        }
                    }
                }
            }
        }
    }
}
