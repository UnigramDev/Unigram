//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Text;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class MessageTonWalletTransferContent : MessageService
    {
        public MessageTonWalletTransferContent()
        {
            InitializeComponent();

            var sheen = new WalletCardSheen();
            if (sheen.Update(new Windows.Foundation.Size(50, 50)))
            {
                Pattern.Background = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    ImageSource = sheen.Source
                };
            }

            VisualUtilities.DropShadow(Address, radius: 0, opacity: 1.0f,
                target: AddressShadow, color: Colors.White, offset: new Vector3(1, 0, 0));
        }

        protected override void UpdateContent(MessageViewModel message)
        {
            if (message.Content is not MessageTonWalletTransfer transfer)
            {
                return;
            }

            var user = message.ClientService.GetUser(message.Chat);
            var self = message.ClientService.GetUser(message.ClientService.Options.MyId);

            var sent = message.IsOutgoing;
            var amount = Formatter.TonBalance(Math.Abs(transfer.Amount));

            AmountInteger.Text = (sent ? "-" : "+") + amount.Integer;
            AmountFraction.Text = amount.Fraction;

            if (user == null || self == null)
            {
                return;
            }

            var builder = new StringBuilder();

            for (int i = 0; i < transfer.PeerAddress.Length; i += 4)
            {
                if (i > 0)
                {
                    builder.Append(i == 24 ? "\n" : " ");
                }

                builder.Append(transfer.PeerAddress.Substring(i, 4).ToUpperInvariant());
            }

            Address.Text = builder.ToString();
            Domain.Text = user.FullName().ToUpper();

            Ribbon.Text = message.SendingState is not null
                ? "sending"
                : sent
                ? "sent"
                : "received";

            //var centerColor = upgradedGift.Gift.Backdrop.Colors.CenterColor.ToColor();
            //var edgeColor = upgradedGift.Gift.Backdrop.Colors.EdgeColor.ToColor();

            //RibbonTop.Color = centerColor.Darken();
            //RibbonBottom.Color = edgeColor.Darken();

            //Pattern.Update(message.ClientService, upgradedGift.Gift);
            //Animation.Source = DelayedFileSource.FromSticker(message.ClientService, upgradedGift.Gift.Model.Sticker);

            //if (upgradedGift.ReceiverId.IsUser(message.ClientService.Options.MyId) && upgradedGift.ReceiverId.AreTheSame(upgradedGift.SenderId))
            //{
            //    Title.Text = Strings.Gift2ActionSelfTitle;
            //}
            //else
            //{
            //    Title.Text = string.Format(Strings.Gift2UniqueTitle, message.IsOutgoing ? self.FirstName : user.FullName(true));
            //}

            //Subtitle.Text = upgradedGift.Gift.ToName();

            //AttributeInfo.Text = Strings.Gift2AttributeModel + "\n" + Strings.Gift2AttributeBackdrop + "\n" + Strings.Gift2AttributeSymbol;
            //AttributeText.Text = upgradedGift.Gift.Model.Name + "\n" + upgradedGift.Gift.Backdrop.Name + "\n" + upgradedGift.Gift.Symbol.Name;
        }

        public override void Recycle()
        {
            base.Recycle();
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
