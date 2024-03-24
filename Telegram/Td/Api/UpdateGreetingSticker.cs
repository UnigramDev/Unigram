namespace Telegram.Td.Api
{
    public class UpdateGreetingSticker
    {
        public UpdateGreetingSticker(Sticker sticker)
        {
            Sticker = sticker;
        }

        public Sticker Sticker { get; }
    }
}
