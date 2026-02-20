//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Td.Api
{
    public partial class UpdateGiftIsSaved
    {
        public string ReceivedGiftId { get; set; }

        public bool IsSaved { get; set; }

        public UpdateGiftIsSaved(string receivedGiftId, bool isSaved)
        {
            ReceivedGiftId = receivedGiftId;
            IsSaved = isSaved;
        }
    }
}
