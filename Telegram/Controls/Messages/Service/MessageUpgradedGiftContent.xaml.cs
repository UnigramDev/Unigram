//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class MessageUpgradedGiftContent : MessageService
    {
        public MessageUpgradedGiftContent()
        {
            InitializeComponent();
        }

        protected override void UpdateContent(MessageViewModel message)
        {
            if (message.Content is not MessageUpgradedGift upgradedGift)
            {
                return;
            }

            var user = message.ClientService.GetUser(message.Chat);
            var self = message.ClientService.GetUser(message.ClientService.Options.MyId);

            if (user == null || self == null)
            {
                return;
            }

            var centerColor = upgradedGift.Gift.Backdrop.Colors.CenterColor.ToColor();
            var edgeColor = upgradedGift.Gift.Backdrop.Colors.EdgeColor.ToColor();

            RibbonTop.Color = centerColor.Darken();
            RibbonBottom.Color = edgeColor.Darken();

            Pattern.Update(message.ClientService, upgradedGift.Gift);
            Animation.Source = DelayedFileSource.FromSticker(message.ClientService, upgradedGift.Gift.Model.Sticker);

            if (upgradedGift.ReceiverId.IsUser(message.ClientService.Options.MyId) && upgradedGift.ReceiverId.AreTheSame(upgradedGift.SenderId))
            {
                Title.Text = Strings.Gift2ActionSelfTitle;
            }
            else
            {
                Title.Text = string.Format(Strings.Gift2UniqueTitle, message.IsOutgoing ? self.FirstName : user.FullName(true));
            }

            Subtitle.Text = upgradedGift.Gift.ToName();

            AttributeInfo.Text = Strings.Gift2AttributeModel + "\n" + Strings.Gift2AttributeBackdrop + "\n" + Strings.Gift2AttributeSymbol;
            AttributeText.Text = upgradedGift.Gift.Model.Name + "\n" + upgradedGift.Gift.Backdrop.Name + "\n" + upgradedGift.Gift.Symbol.Name;
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
