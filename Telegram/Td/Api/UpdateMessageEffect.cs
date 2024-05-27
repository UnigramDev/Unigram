namespace Telegram.Td.Api
{
    public class UpdateMessageEffect
    {
        public UpdateMessageEffect(MessageEffect effect)
        {
            Effect = effect;
        }

        public MessageEffect Effect { get; set; }
    }
}
