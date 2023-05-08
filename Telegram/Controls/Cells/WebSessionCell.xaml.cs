//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
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

            Photo.SetUser(clientService, bot, 36);

            Domain.Text = bot.FullName();
            Title.Text = string.Format("{0}, {1}, {2}", session.DomainName, session.Browser, session.Platform);
            Subtitle.Text = string.Format("{0} \u2022 {1}", session.Location, Formatter.DateExtended(session.LastActiveDate));
        }
    }
}
