using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLInt128 : TLObject
    {
        public long Low;
        public long High;

        public TLInt128() { }
        public TLInt128(TLBinaryReader from)
        {
            Read(from);
        }

        public override TLType TypeId { get { return TLType.Int128; } }

        public override void Read(TLBinaryReader from)
        {
            Low = from.ReadInt64();
            High = from.ReadInt64();
        }

        public override void Write(TLBinaryWriter to)
        {
            to.Write(Low);
            to.Write(High);
        }

        #region Operators
        public static bool operator ==(TLInt128 a, TLInt128 b)
        {
            return a.Low == b.Low && a.High == b.High;
        }

        public static bool operator !=(TLInt128 a, TLInt128 b)
        {
            return a.Low != b.Low || a.High != b.High;
        }

        public override bool Equals(object obj)
        {
            var b = obj as TLInt128;
            if ((object)b == null)
            {
                return false;
            }

            return base.Equals(obj) || (Low == b.Low && High == b.High);
        }

        public override int GetHashCode()
        {
            return Low.GetHashCode() ^ High.GetHashCode();
        }
        #endregion

        public static TLInt128 Random()
        {
            var random = new Random();
            var nonce = new byte[16];
            random.NextBytes(nonce);

            var l = BitConverter.ToInt64(nonce, 0);
            var h = BitConverter.ToInt64(nonce, 8);

            return new TLInt128 { Low = l, High = h };
        }
    }
}
