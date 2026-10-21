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
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// Someone's wallet card, handed over by the profile's balance: it comes out of the button that
    /// asked for it, and the popup explaining why it can be seen comes in around it as it settles.
    /// Closing plays it backwards: the card goes back into the button as the popup fades around it.
    /// </summary>
    public sealed partial class WalletBalancePopup : ModalPopup
    {
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

            // There is no sending money to yourself.
            if (user.Id == clientService.Options.MyId)
            {
                Note.Text = Strings.WalletPublicBalanceNote;
                SecondaryButtonText = Strings.Close;
            }
            else
            {
                Note.Text = card.GramAmount > 0
                    ? string.Format(Strings.WalletProfileOwnsGrams, user.FirstName, Formatter.Grams(Formatter.GramSummary(card.GramAmount), card.GramAmount, capital: true))
                    : string.Format(Strings.WalletProfileOwnsNoGrams, user.FirstName);

                PrimaryButtonText = Strings.WalletSendMoney;
            }

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

            var remaining = Card.Insert();
            if (remaining > TimeSpan.Zero)
            {
                // Both on the card's progress, which runs linearly: the smoke is the progress
                // itself, and the popup goes on the curve the slot's edge comes down on, over
                // the flight back, its first 70%. Backwards, that edge's quint ease out is an ease in.
                var compositor = ElementComposition.GetElementVisual(this).Compositor;
                var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.68f, 0), new Vector2(0.77f, 0));

                HideChrome(TimeSpan.FromMilliseconds(remaining.TotalMilliseconds * 0.7), easing);
                HideSmoke(remaining);
            }
        }

        private void Card_Inserted(object sender, EventArgs e)
        {
            _closing?.Complete();
            _closing = null;
        }

        private void Card_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CardSpace.Height = e.NewSize.Height;
        }

        private void Card_EjectSettling(object sender, EventArgs e)
        {
            ShowChrome();
        }
    }
}
