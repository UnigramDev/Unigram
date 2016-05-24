namespace Telegram.Api.TL
{
    public class TLExportedAuthorization : TLObject
    {
        public const uint Signature = TLConstructors.TLExportedAuthorization;

        public TLInt Id { get; set; }

        public TLString Bytes { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLExportedAuthorization--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Bytes = GetObject<TLString>(bytes, ref position);

            TLUtils.WriteLine("Id: " + Id);
            TLUtils.WriteLine("Bytes: " + Bytes);

            return this;
        }
    }
}
