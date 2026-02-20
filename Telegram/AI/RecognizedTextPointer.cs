//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Native.AI;

namespace Telegram.AI
{
    public readonly struct RecognizedTextPointer
    {
        public readonly int BlockIndex;
        public readonly int LineIndex;
        public readonly int WordIndex;
        public readonly RecognizedTextBoundingBox BoundingBox;

        public RecognizedTextPointer(int blockIndex, int lineIndex, int wordIndex, RecognizedTextBoundingBox boundingBox = default)
        {
            BlockIndex = blockIndex;
            LineIndex = lineIndex;
            WordIndex = wordIndex;
            BoundingBox = boundingBox;
        }

        public static bool operator ==(RecognizedTextPointer a, RecognizedTextPointer b) => a.BlockIndex == b.BlockIndex && a.LineIndex == b.LineIndex && a.WordIndex == b.WordIndex;
        public static bool operator !=(RecognizedTextPointer a, RecognizedTextPointer b) => !(a == b);

        public override bool Equals(object obj) => obj is RecognizedTextPointer p && this == p;
        public override int GetHashCode() => HashCode.Combine(BlockIndex, LineIndex, WordIndex);

        public static RecognizedTextPointer Empty { get; } = new RecognizedTextPointer(-1, -1, -1, default);
    }
}
