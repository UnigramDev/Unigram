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

            if (transfer == null || (transfer.Comment.Length == 0 && !transfer.IsCommentEncrypted))
            {
                CommentRoot?.Visibility = Visibility.Collapsed;
                return;
            }

            if (CommentRoot == null)
            {
                FindName(nameof(CommentRoot));
                Comment.TextEntityClick += OnTextEntityClick;
            }

            CommentRoot.Visibility = Visibility.Visible;

            if (transfer.IsCommentEncrypted)
            {
                var entities = new TextEntity[]
                {
                    new TextEntity(0, CommentPlaceholder.Length, new TextEntityTypeSpoiler())
                };

                Comment.SetText(message.ClientService, new FormattedText(CommentPlaceholder, entities));
            }
            else
            {
                Comment.SetText(message.ClientService, transfer.Comment.AsFormattedText());
            }
        }

        private void OnTextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            e.Handled = true;
        }

        private const string CommentPlaceholder = "encrypted comment";

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
