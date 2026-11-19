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
            else if (upgradedGift.ReceiverId is MessageSenderChat)
            {
                Title.Text = Strings.Gift2UniqueTitle2;
            }
            else
            {
                // Without a sender: a transfer comes from whoever posted the message, while an upgrade is
                // posted by the receiver, so that gift came from the other side.
                var upgrade = upgradedGift.Origin is UpgradedGiftOriginUpgrade;
                var senderId = upgradedGift.SenderId ?? (upgrade != message.IsOutgoing
                    ? new MessageSenderUser(message.ClientService.Options.MyId)
                    : message.Chat.ToMessageSender());

                Title.Text = string.Format(Strings.Gift2UniqueTitle, message.ClientService.GetTitle(senderId, true));
            }

            Subtitle.Text = upgradedGift.Gift.ToName();

            // A message takes the place of the attributes, and its sender stays hidden with the name.
            if (upgradedGift.Text?.Text.Length > 0)
            {
                if (GiftMessage == null)
                {
                    FindName(nameof(GiftMessage));
                }

                GiftMessage.Visibility = Visibility.Visible;
                AttributeInfo.Visibility = Visibility.Collapsed;
                AttributeText.Visibility = Visibility.Collapsed;

                GiftMessageText.SetText(message.ClientService, upgradedGift.Text);

                if (upgradedGift.IsPrivate || upgradedGift.SenderId == null)
                {
                    GiftMessagePhoto.Visibility = Visibility.Collapsed;
                }
                else
                {
                    GiftMessagePhoto.Visibility = Visibility.Visible;
                    GiftMessagePhoto.Source = ProfilePictureSource.MessageSender(message.ClientService, upgradedGift.SenderId);
                }
            }
            else
            {
                GiftMessage?.Visibility = Visibility.Collapsed;
                AttributeInfo.Visibility = Visibility.Visible;
                AttributeText.Visibility = Visibility.Visible;

                AttributeInfo.Text = Strings.Gift2AttributeModel + "\n" + Strings.Gift2AttributeBackdrop + "\n" + Strings.Gift2AttributeSymbol;
                AttributeText.Text = upgradedGift.Gift.Model.Name + "\n" + upgradedGift.Gift.Backdrop.Name + "\n" + upgradedGift.Gift.Symbol.Name;
            }
        }

        /// <summary>
        /// The label of the card's button: View in chat, Send Now in the preview before buying.
        /// </summary>
        public string ButtonText
        {
            get => ViewLabel.Text;
            set => ViewLabel.Text = value;
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
