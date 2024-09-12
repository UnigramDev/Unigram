namespace Telegram.Td.Api
{
    public partial class UpdateGreetingSticker
    {
        public UpdateGreetingSticker(Sticker sticker)
        {
            Sticker = sticker;
        }

        public Sticker Sticker { get; }
    }
}
