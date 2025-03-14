//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Td.Api
{
    public partial class UpdateMessageTranslatedText
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
