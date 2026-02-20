//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Services.Settings
{
    public class ToolTipSettings : SettingsServiceBase
    {
        public ToolTipSettings()
            : base("ToolTip")
        {

        }

        public bool Required(string key)
        {
            var count = GetValueOrDefault(key, 0);
            return count < 3;
        }

        public bool Increment(string key)
        {
            var count = GetValueOrDefault(key, 0);
            if (count < 3)
            {
                AddOrUpdateValue(key, count + 1);
                return true;
            }

            return false;
        }

        public void Complete(string key)
        {
            AddOrUpdateValue(key, 3);
        }

        public void Reset()
        {
            _container.Values.Clear();
        }
    }
}
