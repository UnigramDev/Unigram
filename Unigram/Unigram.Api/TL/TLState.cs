using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLState : TLObject
    {
        public const uint Signature = TLConstructors.TLState;

        public TLInt Pts { get; set; }

        public TLInt Qts { get; set; }

        public TLInt Date { get; set; }

        public TLInt Seq { get; set; }

        public TLInt UnreadCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Pts = GetObject<TLInt>(bytes, ref position);
            Qts = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);
            UnreadCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Pts = GetNullableObject<TLInt>(input);
            Qts = GetNullableObject<TLInt>(input);
            Date = GetNullableObject<TLInt>(input);
            Seq = GetNullableObject<TLInt>(input);
            UnreadCount = GetNullableObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Pts.NullableToStream(output);
            Qts.NullableToStream(output);
            Date.NullableToStream(output);
            Seq.NullableToStream(output);
            UnreadCount.NullableToStream(output);
        }

        public override string ToString()
        {
            return string.Format("p={0} d={1} q={2} s={3} u_c={4}", Pts, Date, Qts, Seq, UnreadCount);
        }
    }
}
