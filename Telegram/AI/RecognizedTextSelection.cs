//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Native.AI;

namespace Telegram.AI
{
    public class RecognizedTextSelection
    {
        public RecognizedTextSelection(string text, IList<RecognizedTextBoundingBox> boundingBoxes)
        {
            Text = text;
            BoundingBoxes = boundingBoxes;
        }

        public string Text { get; }

        public IList<RecognizedTextBoundingBox> BoundingBoxes { get; }
    }
}
