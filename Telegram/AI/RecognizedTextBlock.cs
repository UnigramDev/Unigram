//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Native.AI;

namespace Telegram.AI
{
    public class RecognizedTextBlock
    {
        public RecognizedTextBlock(List<RecognizedLine> lines)
        {
            // TODO: dynamic tolerance
            var tolerance = 4.0f;
            var padding = lines.Select(x => x.BoundingBox.Height()).Average();

            Polygons = RecognizedTextBoundingBoxSimplifier.Union(lines, tolerance, padding * 0.25f);
            Padding = padding * 0.25f;
            Lines = lines;
        }

        public IList<RecognizedLine> Lines { get; }

        public IList<List<Vector2>> Polygons { get; }

        public float Padding { get; }

        public override string ToString()
        {
            return string.Join('\n', Lines.Select(x => x.Text));
        }
    }
}
