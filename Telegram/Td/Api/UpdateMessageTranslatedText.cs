namespace Telegram.Td.Api
{
    public class UpdateMessageTranslatedText
    {
        public UpdateMessageTranslatedText(long chatId, long messageId, MessageTranslateResult translatedText)
        {
            ChatId = chatId;
            MessageId = messageId;
            TranslatedText = translatedText;
        }

        public long ChatId { get; set; }

        public long MessageId { get; set; }

        public MessageTranslateResult TranslatedText { get; set; }
    }
}
