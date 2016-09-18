using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLActionInfo : TLObject
    {
        public Int32 SendBefore { get; set; }
        public TLObject Action { get; set; }

        public TLActionInfo() { }
        public TLActionInfo(TLBinaryReader from, bool fromCache)
        {
            Read(from, fromCache);
        }

        public override string ToString()
        {
            return string.Format("send_before={0} action={1}", SendBefore, Action);
        }

        public override void Read(TLBinaryReader from, bool fromCache)
        {
            SendBefore = from.ReadInt32();
            Action = TLFactory.Read<TLObject>(from, fromCache);
        }

        public override void Write(TLBinaryWriter to, bool toCache)
        {
            to.Write(0xFFFFFF0D);
            to.Write(SendBefore);
            Action.Write(to, toCache);
        }
    }
}
