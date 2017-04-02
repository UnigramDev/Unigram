using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLTuple<T1, T2> : TLTuple
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

    public class TLTuple<T1, T2, T3> : TLTuple<T1, T2>
    {
        public T3 Item3 { get; private set; }

        public TLTuple(T1 item1, T2 item2, T3 item3)
            : base(item1, item2)
        {
            Item3 = item3;
        }

        public TLTuple() { }
        public TLTuple(TLBinaryReader from)
        {
            Read(from);
        }

        public override void Read(TLBinaryReader from)
        {
            base.Read(from);
            Item3 = TLFactory.Read<T3>(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            base.Write(to);
            TLFactory.Write(to, Item3);
        }
    }

    public class TLTuple : TLObject
    {
        public static TLTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
        {
            return new TLTuple<T1, T2>(item1, item2);
        }

        public static TLTuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
        {
            return new TLTuple<T1, T2, T3>(item1, item2, item3);
        }
    }
}
