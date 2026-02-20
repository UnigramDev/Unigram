//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Td.Api;
using Windows.Storage;

namespace Telegram.Services.Settings
{
    public class VideoSettings : SettingsServiceBase
    {
        public VideoSettings(ApplicationDataContainer container)
            : base(container.CreateContainer("Video", ApplicationDataCreateDisposition.Always))
        {

        }

        public bool HasPosition(File file)
        {
            return _container.Values.ContainsKey("Video" + file.Remote.UniqueId);
        }

        public bool TryGetPosition(File file, out double position)
        {
            return _container.Values.TryGet("Video" + file.Remote.UniqueId, out position);
        }

        public void SetPosition(File file, double position)
        {
            if (position > 0)
            {
                _container.Values["Video" + file.Remote.UniqueId] = position;
            }
            else
            {
                _container.Values.Remove("Video" + file.Remote.UniqueId);
            }
        }

        public void RemovePosition(File file)
        {
            _container.Values.Remove("Video" + file.Remote.UniqueId);
        }
    }
}
