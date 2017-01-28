using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLPhoto
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

        private TLPhotoSizeBase _medium;
        public TLPhotoSizeBase Medium
        {
            get
            {
                if (_medium == null)
                    InitializeSizes();

                return _medium;
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
            TLPhotoSizeBase thumb = null, medium = null, full = null;
            int thumbLevel = -1, mediumLevel = -1, fullLevel = -1;

            foreach (var i in Sizes)
            {
                var size = i.Type.Length > 0 ? i.Type[0] : 'z';
                int newThumbLevel = -1, newMediumLevel = -1, newFullLevel = -1;

                switch (size)
                {
                    case 's': newThumbLevel = 0; newMediumLevel = 5; newFullLevel = 4; break; // box 100x100
                    case 'm': newThumbLevel = 2; newMediumLevel = 0; newFullLevel = 3; break; // box 320x320
                    case 'x': newThumbLevel = 5; newMediumLevel = 3; newFullLevel = 1; break; // box 800x800
                    case 'y': newThumbLevel = 6; newMediumLevel = 6; newFullLevel = 0; break; // box 1280x1280
                    case 'w': newThumbLevel = 8; newMediumLevel = 8; newFullLevel = 2; break; // box 2560x2560
                    case 'a': newThumbLevel = 1; newMediumLevel = 4; newFullLevel = 8; break; // crop 160x160
                    case 'b': newThumbLevel = 3; newMediumLevel = 1; newFullLevel = 7; break; // crop 320x320
                    case 'c': newThumbLevel = 4; newMediumLevel = 2; newFullLevel = 6; break; // crop 640x640
                    case 'd': newThumbLevel = 7; newMediumLevel = 7; newFullLevel = 5; break; // crop 1280x1280
                }

                if (newThumbLevel < 0 || newMediumLevel < 0 || newFullLevel < 0)
                {
                    continue;
                }
                if (thumbLevel < 0 || newThumbLevel < thumbLevel)
                {
                    thumbLevel = newThumbLevel;
                    thumb = i;
                }
                if (mediumLevel < 0 || newMediumLevel < mediumLevel)
                {
                    mediumLevel = newMediumLevel;
                    medium = i;
                }
                if (fullLevel < 0 || newFullLevel < fullLevel)
                {
                    fullLevel = newFullLevel;
                    full = i;
                }
            }

            _thumb = thumb;
            _medium = medium;
            _full = full;
        }
    }
}
