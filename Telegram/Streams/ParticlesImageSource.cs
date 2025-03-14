//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Streams
{
    public partial class ParticlesImageSource : AnimatedImageSource
    {
        public override string FilePath => string.Empty;

        public override long FileSize => 0;

        public override long Id => 0;

        public override long Offset => 0;

        public override void ReadCallback(long count)
        {

        }

        public override void SeekCallback(long offset)
        {

        }
    }
}
