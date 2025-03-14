//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Services.Stripe
{
    public partial class StripeToken
    {
        public string Id { get; set; }
        public string Type { get; set; }

        public string Content { get; set; }
    }
}
