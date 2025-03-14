//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Assets.Icons;

namespace Telegram.Controls
{
    public partial class MoreButton : BadgeButton
    {
        public MoreButton()
        {
            DefaultStyleKey = typeof(MoreButton);
            IconSource = new More();
        }
    }
}
