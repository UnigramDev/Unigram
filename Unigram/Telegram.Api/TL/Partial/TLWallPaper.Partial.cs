using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLWallPaper
    {
        private TLPhotoSizeBase _thumb;
        public TLPhotoSizeBase Thumb
        {
            get
            {
                if (_thumb == null)
                    InitializeSizes();

                return _thumb;
            }
        }

        private TLPhotoSizeBase _full;
        public TLPhotoSizeBase Full
        {
            get
            {
                if (_full == null)
                    InitializeSizes();

                return _full;
            }
        }

        private void InitializeSizes()
        {
            TLPhotoSizeBase thumb = null, full = null;
            int thumbLevel = -1, fullLevel = -1;
            foreach (var i in Sizes)
            {
                char size = default(char);
                int w = 0, h = 0;
                switch (i)
                {
                    case TLPhotoSize photoSize:
                        {
                            var s = photoSize.Type;
                            if (s.Length > 0) size = s[0];
                            w = photoSize.W;
                            h = photoSize.H;
                        }
                        break;

                    case TLPhotoCachedSize cachedSize:
                        {
                            var s = cachedSize.Type;
                            if (s.Length > 0) size = s[0];
                            w = cachedSize.W;
                            h = cachedSize.H;
                        }
                        break;
                }
                if (size == default(char) || w == 0 || h == 0) continue;

                int newThumbLevel = Math.Abs((108 * 1) - w), newFullLevel = Math.Abs(2560 - w);
                if (thumbLevel < 0 || newThumbLevel < thumbLevel)
                {
                    thumbLevel = newThumbLevel;
                    thumb = i;
                }
                if (fullLevel < 0 || newFullLevel < fullLevel)
                {
                    fullLevel = newFullLevel;
                    full = i;
                }
            }

            _thumb = thumb;
            _full = full;
        }
    }
}
