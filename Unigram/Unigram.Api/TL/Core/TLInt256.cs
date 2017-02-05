using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLInt256 : TLObject
    {
        public TLInt128 Low;
        public TLInt128 High;

        public TLInt256()
        {
            Low = new TLInt128();
            High = new TLInt128();
        }

        public TLInt256(TLBinaryReader from)
        {
            Low = new TLInt128();
            High = new TLInt128();

            Read(from);
        }

        public override TLType TypeId { get { return TLType.Int256; } }

        public override void Read(TLBinaryReader from)
        {
            Low.Read(from);
            High.Read(from);
        }

        public override void Write(TLBinaryWriter to)
        {
            Low.Write(to);
            High.Write(to);
        }

        #region Operators
        public static bool operator ==(TLInt256 a, TLInt256 b)
        {
            return a.Low == b.Low && a.High == b.High;
        }

        public static bool operator !=(TLInt256 a, TLInt256 b)
        {
            return a.Low != b.Low || a.High != b.High;
        }

        public override bool Equals(object obj)
        {
            var b = obj as TLInt256;
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

        public static TLInt256 Random()
        {
            return new TLInt256
            {
                Low = TLInt128.Random(),
                High = TLInt128.Random()
            };
        }
    }
}
