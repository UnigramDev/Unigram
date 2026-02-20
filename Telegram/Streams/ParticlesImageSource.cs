//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Native;
using Telegram.Td.Api;
using Windows.UI;

namespace Telegram.Streams
{
    public partial class ParticlesImageSource : AnimatedImageSource
    {
        public Color Foreground { get; }

        public Color Background { get; }

        public ParticlesType Type { get; }

        public ParticlesImageSource()
        {
            Foreground = Colors.White;
            Background = Color.FromArgb(0x54, 0, 0, 0);
            Type = ParticlesType.Media;
        }

        public ParticlesImageSource(Color foreground, ParticlesType type = ParticlesType.Text)
        {
            Foreground = foreground;
            Background = Colors.Transparent;
            Type = type;
        }

        public ParticlesImageSource(UpgradedGiftBackdropColors backdrop)
        {
            Foreground = backdrop.SymbolColor.ToColor();
            Background = Colors.Transparent;
            Type = ParticlesType.Status;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Foreground, Background, Type);
        }

        public override string FilePath => string.Empty;

        public override long FileSize => 0;

        public override long Id => 0;

        public override long Offset => 0;

        public override void ReadCallback(long count, long buffer, out long bytesRead)
        {
            bytesRead = count;
        }

        public override void SeekCallback(long offset)
        {

        }
    }
}
