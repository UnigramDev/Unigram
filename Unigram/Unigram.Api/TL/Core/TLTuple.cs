using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Native.TL;

namespace Telegram.Api.TL
{
    public class TLTuple : TLObject
    {
        public override TLType TypeId => (TLType)0xFFFFFF0E;

        public static TLTuple<T1> Create<T1>(T1 item1)
        {
            return new TLTuple<T1>(item1);
        }

        public static TLTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
        {
            return new TLTuple<T1, T2>(item1, item2);
        }

        public static TLTuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
        {
            return new TLTuple<T1, T2, T3>(item1, item2, item3);
        }

        public static TLTuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
        {
            return new TLTuple<T1, T2, T3, T4>(item1, item2, item3, item4);
        }

        public static TLTuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
        {
            return new TLTuple<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5);
        }

        public static TLTuple<T1, T2, T3, T4, T5, T6> Create<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
        {
            return new TLTuple<T1, T2, T3, T4, T5, T6>(item1, item2, item3, item4, item5, item6);
        }

        public static TLTuple<T1, T2, T3, T4, T5, T6, T7> Create<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
        {
            return new TLTuple<T1, T2, T3, T4, T5, T6, T7>(item1, item2, item3, item4, item5, item6, item7);
        }

        public static TLTuple<T1, T2, T3, T4, T5, T6, T7, T8> Create<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
        {
            return new TLTuple<T1, T2, T3, T4, T5, T6, T7, T8>(item1, item2, item3, item4, item5, item6, item7, item8);
        }
    }

    public class TLTuple<T1> : TLTuple
    {
        public T1 Item1 { get; set; }

        public TLTuple(T1 item1)
        {
            Item1 = item1;
        }

        public TLTuple() { }
        public TLTuple(TLBinaryReader from)
        {
            Read(from);
        }

        public override void Read(TLBinaryReader from)
        {
            Item1 = TLFactory.Read<T1>(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            TLFactory.Write<T1>(to, Item1);
        }
    }

    public class TLTuple<T1, T2> : TLTuple<T1>
    {
        public T2 Item2 { get; set; }

        public TLTuple(T1 item1, T2 item2)
            : base(item1)
        {
            Item2 = item2;
        }

        public TLTuple() { }
        public TLTuple(TLBinaryReader from)
        {
            Read(from);
        }

        public override void Read(TLBinaryReader from)
        {
            base.Read(from);
            Item2 = TLFactory.Read<T2>(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            base.Write(to);
            TLFactory.Write<T2>(to, Item2);
        }
    }

    public class TLTuple<T1, T2, T3> : TLTuple<T1, T2>
    {
        public T3 Item3 { get; set; }

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
            TLFactory.Write<T3>(to, Item3);
        }
    }

    public class TLTuple<T1, T2, T3, T4> : TLTuple<T1, T2, T3>
    {
        public T4 Item4 { get; set; }

        public TLTuple(T1 item1, T2 item2, T3 item3, T4 item4)
            : base(item1, item2, item3)
        {
            Item4 = item4;
        }

        public TLTuple() { }
        public TLTuple(TLBinaryReader from)
        {
            Read(from);
        }

        public override void Read(TLBinaryReader from)
        {
            base.Read(from);
            Item4 = TLFactory.Read<T4>(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            base.Write(to);
            TLFactory.Write<T4>(to, Item4);
        }
    }

    public class TLTuple<T1, T2, T3, T4, T5> : TLTuple<T1, T2, T3, T4>
    {
        public T5 Item5 { get; set; }

        public TLTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
            : base(item1, item2, item3, item4)
        {
            Item5 = item5;
        }

        public TLTuple() { }
        public TLTuple(TLBinaryReader from)
        {
            Read(from);
        }

        public override void Read(TLBinaryReader from)
        {
            base.Read(from);
            Item5 = TLFactory.Read<T5>(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            base.Write(to);
            TLFactory.Write<T5>(to, Item5);
        }
    }

    public class TLTuple<T1, T2, T3, T4, T5, T6> : TLTuple<T1, T2, T3, T4, T5>
    {
        public T6 Item6 { get; set; }

        public TLTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
            : base(item1, item2, item3, item4, item5)
        {
            Item6 = item6;
        }

        public TLTuple() { }
        public TLTuple(TLBinaryReader from)
        {
            Read(from);
        }

        public override void Read(TLBinaryReader from)
        {
            base.Read(from);
            Item6 = TLFactory.Read<T6>(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            base.Write(to);
            TLFactory.Write<T6>(to, Item6);
        }
    }

    public class TLTuple<T1, T2, T3, T4, T5, T6, T7> : TLTuple<T1, T2, T3, T4, T5, T6>
    {
        public T7 Item7 { get; set; }

        public TLTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
            : base(item1, item2, item3, item4, item5, item6)
        {
            Item7 = item7;
        }

        public TLTuple() { }
        public TLTuple(TLBinaryReader from)
        {
            Read(from);
        }

        public override void Read(TLBinaryReader from)
        {
            base.Read(from);
            Item7 = TLFactory.Read<T7>(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            base.Write(to);
            TLFactory.Write<T7>(to, Item7);
        }
    }

    public class TLTuple<T1, T2, T3, T4, T5, T6, T7, T8> : TLTuple<T1, T2, T3, T4, T5, T6, T7>
    {
        public T8 Item8 { get; set; }

        public TLTuple(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
            : base(item1, item2, item3, item4, item5, item6, item7)
        {
            Item8 = item8;
        }

        public TLTuple() { }
        public TLTuple(TLBinaryReader from)
        {
            Read(from);
        }

        public override void Read(TLBinaryReader from)
        {
            base.Read(from);
            Item8 = TLFactory.Read<T8>(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            base.Write(to);
            TLFactory.Write<T8>(to, Item8);
        }
    }
}
