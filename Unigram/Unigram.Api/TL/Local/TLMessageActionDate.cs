using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLMessageActionDate : TLMessageActionBase
    {
        public Int32 Date { get; set; }

        // FFFFFF10

        public TLMessageActionDate() { }
        public TLMessageActionDate(TLBinaryReader from, bool cache = false)
        {
            Read(from, cache);
        }

        public override TLType TypeId { get { return (TLType)0xFFFFFF10; } }

        public override void Read(TLBinaryReader from, bool cache = false)
        {
            Date = from.ReadInt32();
            if (cache) ReadFromCache(from);
        }

        public override void Write(TLBinaryWriter to, bool cache = false)
        {
            to.Write(0xFFFFFF10);
            to.Write(Date);
            if (cache) WriteToCache(to);
        }

    }
}
