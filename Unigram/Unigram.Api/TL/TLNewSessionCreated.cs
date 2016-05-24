namespace Telegram.Api.TL
{
    internal class TLNewSessionCreated : TLObject
    {
        public const uint Signature = TLConstructors.TLNewSessionCreated;

        public TLLong FirstMessageId { get; set; }

        public TLLong UniqueId { get; set; }

        public TLLong ServerSalt { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            FirstMessageId = GetObject<TLLong>(bytes, ref position);
            UniqueId = GetObject<TLLong>(bytes, ref position);
            ServerSalt = GetObject<TLLong>(bytes, ref position);

            return this;
        }
    }
}