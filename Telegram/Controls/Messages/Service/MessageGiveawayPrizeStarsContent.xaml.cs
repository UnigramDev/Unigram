//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class MessageGiveawayPrizeStarsContent : MessageService
    {
        public MessageGiveawayPrizeStarsContent()
        {
            InitializeComponent();
        }

        protected override void UpdateContent(MessageViewModel message)
        {
            if (message.Content is not MessageGiveawayPrizeStars giveawayPrizeStars)
            {
                return;
            }

            Title.Text = Strings.ActionStarGiveawayPrizeTitle;
            Animation.Source = DelayedFileSource.FromSticker(message.ClientService, giveawayPrizeStars.Sticker);
        }

        public override void Recycle()
        {
            base.Recycle();

            Animation.Source = null;
        }

        private void Service_Click(object sender, RoutedEventArgs e)
        {
            if (Message?.Delegate != null)
            {
                Message.Delegate.ExecuteServiceMessage(Message);
            }
        }
    }
}
