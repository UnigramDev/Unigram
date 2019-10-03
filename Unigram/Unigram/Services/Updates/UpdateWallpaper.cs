using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Services.Updates
{
    public class UpdateWallpaper
    {
        public UpdateWallpaper(int background, int color)
        {
            Background = background;
            Color = color;
        }

        public int Background { get; private set; }
        public int Color { get; private set; }
    }
}