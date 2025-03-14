//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;

namespace Telegram.Td.Api
{
    public partial class ReactionTypeEqualityComparer : IEqualityComparer<ReactionType>
    {
        public bool Equals(ReactionType x, ReactionType y)
        {
            return x.AreTheSame(y);
        }

        public int GetHashCode(ReactionType obj)
        {
            if (obj is ReactionTypeEmoji emoji)
            {
                return emoji.Emoji.GetHashCode();
            }
            else if (obj is ReactionTypeCustomEmoji customEmoji)
            {
                return customEmoji.CustomEmojiId.GetHashCode();
            }
            else if (obj is ReactionTypePaid)
            {
                return "\u2B50".GetHashCode();
            }

            return obj.GetHashCode();
        }
    }
}
