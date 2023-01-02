//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Storage;

namespace Unigram.Services.Settings
{
    public class FiltersSettings : SettingsServiceBase
    {
        public FiltersSettings(ApplicationDataContainer container = null)
            : base(container)
        {
        }
    }
}
