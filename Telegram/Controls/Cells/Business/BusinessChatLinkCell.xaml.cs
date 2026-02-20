//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells.Business
{
    public sealed partial class BusinessChatLinkCell : Grid
    {
        public BusinessChatLinkCell()
        {
            InitializeComponent();
        }

        public void UpdateContent(IClientService clientService, BusinessChatLink chatLink)
        {
            FromLabel.Text = string.IsNullOrEmpty(chatLink.Title)
                ? chatLink.Link
                : chatLink.Title;

            if (string.IsNullOrEmpty(chatLink.Text.Text))
            {
                BriefText.SetText(clientService, Strings.NoText.AsFormattedText());
            }
            else
            {
                BriefText.SetText(clientService, chatLink.Text);
            }

            BriefText.SetQuery(string.Empty);

            ViewCountLabel.Text = chatLink.ViewCount > 0
                ? Locale.Declension(Strings.R.Clicks, chatLink.ViewCount)
                : Strings.NoClicks;
        }
    }
}
