//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Charts.DataView
{
    public partial class TransitionParams
    {
        public float pickerStartOut;
        public float pickerEndOut;

        public float xPercentage;
        public long date;

        public float pX;
        public float pY;

        public bool needScaleY = true;

        public float progress;

        public float[] startX;
        public float[] startY;
        public float[] endX;
        public float[] endY;

        public float[] angle;
    }
}
