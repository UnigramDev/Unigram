//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Td.Api
{
    public partial class UpdatePremiumState
    {
        public bool IsPremium { get; }

        public bool IsPremiumAvailable { get; }

        public UpdatePremiumState(bool premium, bool available)
        {
            IsPremium = premium;
            IsPremiumAvailable = available;
        }
    }
}
