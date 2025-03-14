//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Telegram.Td.Api
{
    public partial class MessageBigEmoji : MessageContent
    {
        public FormattedText Text { get; set; }

        public int Count { get; set; }

        public MessageBigEmoji()
        {

        }

        public MessageBigEmoji(FormattedText text, int count)
        {
            Text = text;
            Count = count;
        }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }
}
