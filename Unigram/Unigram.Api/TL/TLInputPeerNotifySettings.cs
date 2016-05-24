namespace Telegram.Api.TL
{
    public class TLInputPeerNotifySettings : TLObject
    {
        public const uint Signature = TLConstructors.TLInputPeerNotifySettings;

        public TLInt MuteUntil { get; set; }

        public TLString Sound { get; set; }

        public TLBool ShowPreviews { get; set; }

        public TLInt EventsMask { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                MuteUntil.ToBytes(),
                Sound.ToBytes(),
                ShowPreviews.ToBytes(),
                EventsMask.ToBytes());
        }

        public override string ToString()
        {
            return string.Format("mute_until={0} sound={1} show_previews={2} events_mask={3}", MuteUntil, Sound, ShowPreviews, EventsMask);
        }
    }
}
