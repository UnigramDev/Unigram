namespace Telegram.Api.TL
{
    public abstract class TLSentMessageBase : TLObject
    {
        public TLInt Id { get; set; }
        public TLInt Date { get; set; }
        public TLInt Pts { get; set; }

        public virtual TLInt GetSeq()
        {
            return null;
        }
    }

    public interface ISentMessageMedia
    {
        TLMessageMediaBase Media { get; set; }
    }

    public class TLSentMessage : TLSentMessageBase
    {
        public const uint Signature = TLConstructors.TLSentMessage;

        public TLInt Seq { get; set; }

        public override TLInt GetSeq()
        {
            return Seq;
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Date.ToBytes(),
                Pts.ToBytes(),
                Seq.ToBytes());
        }
    }

    public class TLSentMessage24 : TLSentMessageBase, IMultiPts
    {
        public const uint Signature = TLConstructors.TLSentMessage24;

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Date.ToBytes(),
                Pts.ToBytes(),
                PtsCount.ToBytes());
        }
    }

    public class TLSentMessage26 : TLSentMessage24, ISentMessageMedia
    {
        public new const uint Signature = TLConstructors.TLSentMessage26;

        public TLMessageMediaBase Media { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Media = GetObject<TLMessageMediaBase>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);


            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Date.ToBytes(),
                Media.ToBytes(),
                Pts.ToBytes(),
                PtsCount.ToBytes());
        }
    }

    public class TLSentMessage34 : TLSentMessage26
    {
        public new const uint Signature = TLConstructors.TLSentMessage34;

        public TLVector<TLMessageEntityBase> Entities { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Media = GetObject<TLMessageMediaBase>(bytes, ref position);
            Entities = GetObject<TLVector<TLMessageEntityBase>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Date.ToBytes(),
                Media.ToBytes(),
                Entities.ToBytes(),
                Pts.ToBytes(),
                PtsCount.ToBytes());
        }
    }

    public class TLSentMessageLink : TLSentMessage
    {
        public new const uint Signature = TLConstructors.TLSentMessageLink;

        public TLVector<TLLinkBase> Links { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);
            Links = GetObject<TLVector<TLLinkBase>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Date.ToBytes(),
                Pts.ToBytes(),
                Seq.ToBytes(),
                Links.ToBytes());
        }
    }

    public class TLSentMessageLink24 : TLSentMessage24
    {
        public new const uint Signature = TLConstructors.TLSentMessageLink24;

        public TLVector<TLLinkBase> Links { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Links = GetObject<TLVector<TLLinkBase>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Date.ToBytes(),
                Pts.ToBytes(),
                PtsCount.ToBytes(),
                Links.ToBytes());
        }
    }

    public class TLSentMessageLink26 : TLSentMessageLink24, ISentMessageMedia
    {
        public new const uint Signature = TLConstructors.TLSentMessageLink26;

        public TLMessageMediaBase Media { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Media = GetObject<TLMessageMediaBase>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            Links = GetObject<TLVector<TLLinkBase>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                Date.ToBytes(),
                Media.ToBytes(),
                Pts.ToBytes(),
                PtsCount.ToBytes(),
                Links.ToBytes());
        }
    }
}
