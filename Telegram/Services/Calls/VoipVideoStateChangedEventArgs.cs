//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;

namespace Telegram.Services.Calls
{
    public partial class VoipVideoStateChangedEventArgs
    {
        public VoipVideoStateChangedEventArgs(bool active, Vector2 frame)
        {
            IsActive = active;
            Frame = frame;
        }

        public bool IsActive { get; init; }

        public Vector2 Frame { get; set; }
    }
}
