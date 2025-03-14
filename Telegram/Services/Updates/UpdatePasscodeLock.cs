//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Services.Updates
{
    public partial class UpdatePasscodeLock
    {
        public UpdatePasscodeLock(bool enabled)
        {
            IsEnabled = enabled;
        }

        public bool IsEnabled { get; private set; }
    }
}
