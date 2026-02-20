//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Td.Api
{
    public partial class UpdateGiftIsSold
    {
        public string ReceivedGiftId { get; set; }

        public UpdateGiftIsSold(string receivedGiftId)
        {
            ReceivedGiftId = receivedGiftId;
        }
    }
}
