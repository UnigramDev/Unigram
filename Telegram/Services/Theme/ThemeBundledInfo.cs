//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services.Settings;
using Windows.UI;

namespace Telegram.Services
{
    public partial class ThemeBundledInfo : ThemeInfoBase
    {
        public override bool IsOfficial => true;
    }

    public abstract partial class ThemeInfoBase
    {
        public string Name { get; set; }
        public TelegramTheme Parent { get; set; }

        public abstract bool IsOfficial { get; }



        public virtual Color ChatBackgroundColor
        {
            get
            {
                if (Parent == TelegramTheme.Light)
                {
                    return Color.FromArgb(0xFF, 0xdf, 0xe4, 0xe8);
                }

                return Color.FromArgb(0xFF, 0x10, 0x14, 0x16);
            }
        }

        public virtual Color ChatBorderColor
        {
            get
            {
                if (Parent == TelegramTheme.Light)
                {
                    return Color.FromArgb(0xFF, 0xe6, 0xe6, 0xe6);
                }

                return Color.FromArgb(0xFF, 0x2b, 0x2b, 0x2b);
            }
        }

        public virtual Color MessageBackgroundColor
        {
            get
            {
                if (Parent == TelegramTheme.Light)
                {
                    return Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
                }

                return Color.FromArgb(0xFF, 0x18, 0x25, 0x33);
            }
        }

        public virtual Color MessageBackgroundOutColor
        {
            get
            {
                if (Parent == TelegramTheme.Light)
                {
                    return Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF);
                }

                return Color.FromArgb(0xFF, 0x2B, 0x52, 0x78);
            }
        }

        public virtual Color SelectionColor => AccentColor;

        public virtual Color AccentColor
        {
            get
            {
                if (Parent == TelegramTheme.Light)
                {
                    return Color.FromArgb(0xFF, 0x15, 0x8D, 0xCD);
                }

                return Color.FromArgb(0xFF, 0x71, 0xBA, 0xFA);
            }
        }
    }
}
