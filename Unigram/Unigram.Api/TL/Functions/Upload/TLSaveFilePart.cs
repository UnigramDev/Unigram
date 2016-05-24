namespace Telegram.Api.TL.Functions.Upload
{
    public class TLSaveFilePart : TLObject
    {
        public const string Signature = "#b304a621";

        public TLLong FileId { get; set; }

        public TLInt FilePart { get; set; }

        public TLString Bytes { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                FileId.ToBytes(),
                FilePart.ToBytes(),
                Bytes.ToBytes());
        }
    }

    public class TLSaveBigFilePart : TLObject
    {
        public const string Signature = "#de7b673d";

        public TLLong FileId { get; set; }

        public TLInt FilePart { get; set; }

        public TLInt FileTotalParts { get; set; }

        public TLString Bytes { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                FileId.ToBytes(),
                FilePart.ToBytes(),
                FileTotalParts.ToBytes(),
                Bytes.ToBytes());
        }
    }
}
