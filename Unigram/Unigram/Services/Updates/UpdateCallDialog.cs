//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;

namespace Telegram.Services.Updates
{
    public class UpdateCallDialog
    {
        public UpdateCallDialog(Call call)
        {
            Call = call;
        }

        public UpdateCallDialog(GroupCall call)
        {
            GroupCall = call;
        }

        public UpdateCallDialog()
        {

        }

        public Call Call { get; private set; }
        public GroupCall GroupCall { get; private set; }
    }
}
