//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Calls
{
    public sealed partial class VerificationState : UserControl
    {
        private readonly VerificationStateReel[] _reels;

        public VerificationState()
        {
            this.InitializeComponent();

            _reels = new[]
            {
                Reel0,
                Reel1,
                Reel2,
                Reel3
            };
        }

        public void UpdateState(int generation, IList<string> emojis)
        {
            if (emojis.Count == 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    _reels[i].UpdateState(generation, emojis[i], i);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    _reels[i].UpdateState(generation, string.Empty, i);
                }
            }
        }
    }
}
