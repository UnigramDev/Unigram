//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

namespace Telegram.Td.Api
{
    // How far one of our conversions has got. TDLib reports it through updateFile, but only for a
    // file the client has seen: a story's file is a copy TDLib makes of the converted one, so the
    // progress of the conversion itself never reaches the client that way.
    public partial class UpdateFileGenerationProgress
    {
        public string Conversion { get; }

        // 0 to 100, as TDLib is told it.
        public int Percent { get; }

        public UpdateFileGenerationProgress(string conversion, int percent)
        {
            Conversion = conversion;
            Percent = percent;
        }
    }
}
