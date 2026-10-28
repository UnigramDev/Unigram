//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// Someone's wallet card, handed over by the profile's balance: it comes out of the button that
    /// asked for it, and a note on why it can be seen above it and what can be done with it below
    /// come in with its flight. Closing plays it backwards, the card going back into the button.
    /// </summary>
    public sealed partial class WalletBalancePopup : ModalPopup
    {
        // How far the note and the buttons rise into place as they come in, and how small the
        // buttons start.
        private const float TipRise = 8;
        private const float ActionsRise = 16;
        private const float ActionsStartScale = 0.8f;

        private Deferral _closing;

        /// <param name="slot">What was clicked to show it, which the card comes out of.</param>
        public WalletBalancePopup(IClientService clientService, User user, TonWalletCard card, FrameworkElement slot)
        {
            InitializeComponent();

            Card.SetCard(clientService, card, user.FullName());
            Card.Stickers.IsReadOnly = true;
            Card.Stickers.Show(clientService, card);

            var background = Card.Stickers.Find(card.BackgroundNftAddress);
            Card.SetStyle(clientService, background?.Collectible.Gift, default, false);

            CloseCardButton.Content = Strings.Close;

            // There is no sending money to yourself.
            if (user.Id == clientService.Options.MyId)
            {
                Note.Text = Strings.WalletPublicBalanceNote;
                SendMoneyButton.Visibility = Visibility.Collapsed;

                Grid.SetColumn(CloseCardButton, 0);
                Grid.SetColumnSpan(CloseCardButton, 2);

                // Enter presses primary even in a popup with no primary button, which would close
                // this one as if Send Money had been clicked.
                IsPrimaryButtonEnabled = false;
            }
            else
            {
                Note.Text = card.GramAmount > 0
                    ? string.Format(Strings.WalletProfileOwnsGrams, user.FirstName, Formatter.Grams(Formatter.GramSummary(card.GramAmount), card.GramAmount, capital: true))
                    : string.Format(Strings.WalletProfileOwnsNoGrams, user.FirstName);

                SendMoneyButton.Content = Strings.WalletSendMoney;
            }

            // ToastPopup's, and unguarded like it: ThemeShadow arrived in the minimum version.
            TipRoot.Shadow = new ThemeShadow();
            TipRoot.Translation = new Vector3(0, 0, 32);

            Card.EjectFlightChanged += Card_EjectFlightChanged;
            Card.Eject(slot);

            Closing += OnClosing;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            HideChrome();
        }

        private void OnClosing(ModalPopup sender, ModalPopupClosingEventArgs args)
        {
            _closing = args.GetDeferral();

            // The smoke goes with the card's progress, which runs linearly.
            var remaining = Card.Insert();
            if (remaining > TimeSpan.Zero)
            {
                HideSmoke(remaining);
            }
        }

        private void Card_EjectFlightChanged(object sender, float flight)
        {
            Tip.Opacity = flight;
            Tip.Translation = new Vector3(0, TipRise * (1 - flight), 0);

            var scale = ActionsStartScale + (1 - ActionsStartScale) * flight;

            Actions.Opacity = flight;
            Actions.Translation = new Vector3(0, ActionsRise * (1 - flight), 0);
            Actions.CenterPoint = new Vector3(Actions.ActualSize / 2, 0);
            Actions.Scale = new Vector3(scale, scale, 1);
        }

        private void Card_Inserted(object sender, EventArgs e)
        {
            _closing?.Complete();
            _closing = null;
        }

        private void SendMoneyButton_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }

        private void CloseCardButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
    }
}
