//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Windows.UI;
using Windows.UI.Xaml;

namespace Telegram.Controls.Media
{
    public class CheckBoxResources : ResourceDictionary
    {
        private Color _color;
        public Color Color
        {
            get => _color; 
            set => Create(_color = value);
        }

        private void Create(Color value)
        {
            Theme.AddCheckBoxPalette(this, value, false);
        }
    }
}
