namespace Telegram.Api.TL
{
    public abstract class TLAppChangelogBase : TLObject { }

    public class TLAppChangelogEmpty : TLAppChangelogBase
    {
        public const uint Signature = TLConstructors.TLAppChangelogEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }
    }

    public class TLAppChangelog : TLAppChangelogBase
    {
        public const uint Signature = TLConstructors.TLAppChangelog;

        public TLString Text { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Text = GetObject<TLString>(bytes, ref position);

            return this;
        }
    }
}
