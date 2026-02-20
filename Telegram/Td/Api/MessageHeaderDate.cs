//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Td.Api
{
    public partial class MessageHeaderDate : MessageContent
    {
        public MessageHeaderDate(int date)
        {
            Date = date;
        }

        public int Date { get; set; }

        public override string ToString()
        {
            return nameof(MessageHeaderDate);
        }
    }
}
