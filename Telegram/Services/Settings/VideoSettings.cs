//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Td.Api;

namespace Telegram.Services.Settings
{
    public class VideoSettings : SettingsServiceBase
    {
        public VideoSettings(ISettingsStore container)
            : base(container.GetContainer("Video"))
        {

        }

        public bool HasPosition(File file)
        {
            return _container.ContainsKey("Video" + file.Remote.UniqueId);
        }

        public bool TryGetPosition(File file, out double position)
        {
            return TryGetValue(_container, "Video" + file.Remote.UniqueId, out position);
        }

        public void SetPosition(File file, double position)
        {
            if (position > 0)
            {
                _container.SetValue("Video" + file.Remote.UniqueId, position);
            }
            else
            {
                _container.Remove("Video" + file.Remote.UniqueId);
            }
        }

        public void RemovePosition(File file)
        {
            _container.Remove("Video" + file.Remote.UniqueId);
        }
    }
}
