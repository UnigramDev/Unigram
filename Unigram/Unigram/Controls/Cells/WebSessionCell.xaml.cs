//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;
using Unigram.Converters;
using Unigram.Services;

namespace Unigram.Controls.Cells
{
    public sealed partial class WebSessionCell : Grid
    {
        public WebSessionCell()
        {
            InitializeComponent();
        }

        public void UpdateConnectedWebsite(IClientService clientService, ConnectedWebsite session)
        {
            var bot = clientService.GetUser(session.BotUserId);
            if (bot == null)
            {
                return;
            }

            Photo.SetUser(clientService, bot, 18);

            Domain.Text = session.DomainName;
            Title.Text = string.Format("{0}, {1}, {2}", bot.FirstName, session.Browser, session.Platform);
            Subtitle.Text = string.Format("{0} — {1}", session.Ip, session.Location);

            LastActiveDate.Text = Converter.DateExtended(session.LastActiveDate);
        }
    }
}
