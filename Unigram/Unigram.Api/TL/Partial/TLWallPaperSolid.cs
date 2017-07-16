using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace Telegram.Api.TL
{
    public partial class TLWallPaperSolid
    {
        private Color? _backgroundColor;
        public Color BackgroundColor
        {
            get
            {
                if (_backgroundColor == null)
                    _backgroundColor = Windows.UI.Color.FromArgb(0xFF,
                        (byte)((BgColor >> 16) & 0xFF),
                        (byte)((BgColor >> 8) & 0xFF),
                        (byte)((BgColor & 0xFF)));

                return _backgroundColor.Value;
            }
        }
    }
}
