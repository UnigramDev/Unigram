namespace Telegram.Td.Api
{
    public partial class UpdateMessageEffect
    {
        public UpdateMessageEffect(MessageEffect effect)
        {
            Effect = effect;
        }

        public MessageEffect Effect { get; set; }
    }
}
