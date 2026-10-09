//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System.Numerics;
using System.Text;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// The wallet, as a card: the balance on its face, the address engraved down its side.
    /// </summary>
    /// <remarks>
    /// Its own control because it is shown in two places that share nothing else - the wallet
    /// window, and the popup a dApp's connection request opens, where it is what tells the user
    /// which wallet they are about to hand over.
    ///
    /// Everything it draws comes from <see cref="SetState"/>. It owns the parts that are the card
    /// rather than the screen: the tilt, the gradient that repaints with it, and the skeleton that
    /// stands in before the numbers are known.
    /// </remarks>
    public sealed partial class WalletCard : UserControl
    {
        private readonly WalletCardSheen _sheen = new();

        private bool _skeleton;

        public WalletCard()
        {
            InitializeComponent();

            // The proportions of a payment card, which is what the design is drawn against.
            LayoutRoot.Constraint = new Size(360, 220);
            LayoutRoot.SizeChanged += OnSizeChanged;

            CardBalanceGram.Foreground = new SolidColorBrush(_sheen.Accent);
            CardBalanceUsd.Foreground = new SolidColorBrush(_sheen.Accent);

            CardAddress.SizeChanged += OnAddressSizeChanged;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            VisualUtilities.AttachTilt(LayoutRoot, Sheen, 10, 20, 0, OnTilt);

            // box-shadow: 0px 1px 0px 0px rgba(255, 255, 255, 0.06)
            //
            // No blur and a single pixel down: this is the highlight that makes the address read
            // as engraved into the card rather than printed on it.
            VisualUtilities.DropShadow(CardAddress, radius: 0, opacity: 1.0f,
                target: CardAddressShadow, color: Colors.White, offset: new Vector3(1, 0, 0));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            VisualUtilities.DetachTilt(LayoutRoot);

            // Detaching stops the per-frame callback wherever it happens to be, and the card can be
            // shown again with its surface intact, so the bands are reset explicitly.
            OnTilt(Vector2.Zero, 0);
        }

        /// <summary>
        /// Draws the wallet this card stands for.
        /// </summary>
        /// <remarks>
        /// Everything at once rather than a property each: the balance and what it converts to are
        /// one answer, and showing half of it is the thing this exists to avoid.
        /// </remarks>
        public void SetState(IClientService clientService, WalletState state)
        {
            UpdateAddress(clientService, state.Address);
            UpdateBalance(clientService, state);
        }

        /// <summary>
        /// The same card, showing one transfer instead of the balance: what leaves, and the
        /// address it leaves for.
        /// </summary>
        /// <remarks>
        /// The card is the wallet, so a request to spend from it belongs on the card rather than
        /// beside it - and the address engraved is the *recipient*, which is the one thing the
        /// user has to recognise before agreeing.
        ///
        /// No skeleton and no owner name here. Both belong to the card standing for the wallet at
        /// rest; this one stands for a single act.
        /// </remarks>
        public void SetTransfer(IClientService clientService, WalletState state, string recipient, BigInteger nanograms)
        {
            UpdateAddress(clientService, recipient);

            CardName.Visibility = Visibility.Collapsed;
            CardBalanceSkeleton.Visibility = Visibility.Collapsed;

            CardBalanceIcon.Visibility = Visibility.Visible;
            CardBalanceText.Visibility = Visibility.Visible;

            var amount = Formatter.GramExact(nanograms);

            // Signed, because the card is the user's own wallet and this is money leaving it. The
            // minus is part of the number rather than a decoration on it.
            CardBalance.Text = "−" + amount.Integer;
            CardBalanceFraction.Text = amount.Fraction;

            if (WalletHelper.TryToCurrency(clientService, state, nanograms, out var converted))
            {
                CardBalanceUsd.Text = Formatter.FormatAmountExact(converted, WalletHelper.CurrencyDecimals, state?.Currency ?? "USD");
                CardBalanceUsd.Visibility = Visibility.Visible;
            }
            else
            {
                // No rate, so no second line rather than a number in a currency it was never
                // converted into.
                CardBalanceUsd.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateAddress(IClientService clientService, string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                CardAddress.Text = string.Empty;
            }
            else
            {
                var builder = new StringBuilder();

                for (int i = 0; i < address.Length; i += 4)
                {
                    if (i > 0)
                    {
                        builder.Append(i == 24 ? "\n" : " ");
                    }

                    builder.Append(address.Substring(i, 4).ToUpperInvariant());
                }

                CardAddress.Text = builder.ToString();
            }

            if (clientService.TryGetUser(clientService.Options.MyId, out Td.Api.User user))
            {
                CardName.Text = user.FullName().ToUpperInvariant();
            }

            // Shown again for the caller that hid it, so one card can be reused for both.
            CardName.Visibility = Visibility.Visible;
        }

        private void UpdateBalance(IClientService clientService, WalletState state)
        {
            // Half an answer is not shown: grams with no rate to convert them at would be a number
            // beside a currency it has not been converted into.
            var known = state.IsSynchronized && state.CurrencyRate > 0;

            CardBalanceIcon.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
            CardBalanceText.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
            CardBalanceUsd.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
            CardBalanceSkeleton.Visibility = known ? Visibility.Collapsed : Visibility.Visible;

            if (!known)
            {
                ShowSkeleton();
                return;
            }

            var amount = Formatter.GramSummary(state.BalanceNanograms);
            CardBalance.Text = amount.Integer;
            CardBalanceFraction.Text = amount.Fraction;

            CardBalanceUsd.Text = WalletHelper.TryToCurrency(clientService, state, state.BalanceNanograms, out var converted)
                ? Formatter.FormatAmountExact(converted, WalletHelper.CurrencyDecimals, state.Currency ?? "USD")
                : string.Empty;
        }

        /// <summary>
        /// The two bars the balance and its converted line will fill, shimmering.
        /// </summary>
        /// <remarks>
        /// Once: every call builds a visual and hands it to the element, so calling it on each
        /// update would stack them. The card is drawn at a fixed design size inside a Viewbox, so
        /// these numbers are that design's, not the screen's.
        ///
        /// White rather than the theme's hover colour, which the skeleton would otherwise take: the
        /// card is the same blue in either theme, and a light theme's hover is dark.
        /// </remarks>
        private void ShowSkeleton()
        {
            if (_skeleton)
            {
                return;
            }

            _skeleton = true;

            VisualUtilities.SetSkeleton(CardBalanceSkeleton, new Vector2(180, 62),
                Color.FromArgb(0x3A, 0xFF, 0xFF, 0xFF),
                CanvasGeometry.CreateRoundedRectangle(null, 0, 0, 170, 32, 8, 8),
                CanvasGeometry.CreateRoundedRectangle(null, 0, 40, 120, 22, 8, 8));
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            SheenRoot.CornerRadius = new CornerRadius(e.NewSize.Width * (3.18 / 85.60));

            // A new surface only when the size actually moved, and the card settles at one size
            // and is measured again at that size on every reflow.
            if (_sheen.Update(e.NewSize))
            {
                if (Sheen.Fill is ImageBrush brush)
                {
                    brush.ImageSource = _sheen.Source;
                }
                else
                {
                    Sheen.Fill = new ImageBrush
                    {
                        ImageSource = _sheen.Source
                    };
                }
            }
        }

        /// <summary>
        /// Repaints the card's gradient for one frame of the tilt. What changes is the shape of
        /// the ramp, which is why it is a redraw rather than something the compositor can do.
        /// </summary>
        private void OnTilt(Vector2 pointer, float amount)
        {
            _sheen.Render(pointer, amount);
        }

        private void OnAddressSizeChanged(object sender, SizeChangedEventArgs e)
        {
            CardAddressShadow.RenderTransform = new CompositeTransform
            {
                Rotation = 90,
                TranslateX = -(e.NewSize.Height - e.NewSize.Width) / 2
            };
            CardAddress.RenderTransform = new CompositeTransform
            {
                Rotation = 90,
                TranslateX = -(e.NewSize.Height - e.NewSize.Width) / 2
            };
        }
    }
}
