using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLTuple<T1, T2> : TLObject
    {
        public T1 Item1 { get; private set; }

        public T2 Item2 { get; private set; }

        public TLTuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public TLTuple() { }
        public TLTuple(TLBinaryReader from)
        {
            Read(from);
        }

        public override void Read(TLBinaryReader from)
        {
            from.ReadInt32();
            Item1 = TLFactory.Read<T1>(from);
            Item2 = TLFactory.Read<T2>(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            to.Write(0xFFFFFF0E);
            to.Write(2);
            TLFactory.Write(to, Item1);
            TLFactory.Write(to, Item2);
        }
    }

    public static class TLTuple
    {

        public static TLTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
        {
            return new TLTuple<T1, T2>(item1, item2);
        }

    }
}
