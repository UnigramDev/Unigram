namespace Telegram.Api.TL
{
    public class TLFile : TLObject
    {
        public const uint Signature = TLConstructors.TLFile;

        public TLFileTypeBase Type { get; set; }

        public TLInt MTime { get; set; }

        public TLString Bytes { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLFile--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Type = GetObject<TLFileTypeBase>(bytes, ref position);
            MTime = GetObject<TLInt>(bytes, ref position);
            Bytes = GetObject<TLString>(bytes, ref position);

            return this;
        }
    }
}
