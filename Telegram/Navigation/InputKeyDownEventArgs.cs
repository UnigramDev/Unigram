//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.ComponentModel;
using Windows.System;

namespace Telegram.Services.Keyboard
{
    public partial class InputKeyDownEventArgs : HandledEventArgs
    {
        public bool AltKey { get; set; }
        public bool ControlKey { get; set; }
        public bool ShiftKey { get; set; }
        public VirtualKey VirtualKey { get; set; }

        public uint RepeatCount { get; set; }

        public bool OnlyAlt => AltKey & !ControlKey & !ShiftKey;
        public bool OnlyControl => !AltKey & ControlKey & !ShiftKey;
        public bool OnlyShift => !AltKey & !ControlKey & ShiftKey;

        public bool OnlyKey => !AltKey && !ControlKey && !ShiftKey;

        public override string ToString()
        {
            return $"KeyboardEventArgs = Handled {Handled}, AltKey {AltKey}, ControlKey {ControlKey}, ShiftKey {ShiftKey}, VirtualKey {VirtualKey}, OnlyAlt {OnlyAlt}, OnlyControl {OnlyControl}, OnlyShift {OnlyShift}";
        }
    }
}
