//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Td;
using Telegram.Td.Api;

namespace Telegram.Views.Wallet.Popups
{
    public sealed partial class WalletAboutPopup : ModalPopup
    {
        public WalletAboutPopup()
        {
            InitializeComponent();

            PrimaryButtonText = "[Got it]";
            ButtonsLayout = ContentPopupButtonsLayout.Vertical;

            var markdown = ClientEx.ParseMarkdown("[By using Wallet you agree to **Terms of Service**.]");
            if (markdown.Entities.Count == 1)
            {
                markdown.Entities[0].Type = new TextEntityTypeTextUrl();
            }

            TextBlockHelper.SetFormattedText(Footer, markdown);
        }

        private void Footer_Click(object sender, TextUrlClickEventArgs e)
        {

        }
    }
}
