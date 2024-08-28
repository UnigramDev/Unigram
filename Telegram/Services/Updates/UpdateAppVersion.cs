//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Services.Updates
{
    public partial class UpdateAppVersion
    {
        public CloudUpdate Update { get; set; }

        public UpdateAppVersion(CloudUpdate update)
        {
            Update = update;
        }
    }
}
