//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;

namespace Telegram.Td.Api
{
    public interface MessageTranslateResult
    {

    }

    public partial class MessageTranslateResultText : MessageTranslateResult
    {
        public MessageTranslateResultText(string language, StyledText text)
        {
            Language = language;
            Text = text;
        }

        public string Language { get; }

        public StyledText Text { get; }
    }

    public partial class MessageTranslateResultPending : MessageTranslateResult
    {

    }
}
