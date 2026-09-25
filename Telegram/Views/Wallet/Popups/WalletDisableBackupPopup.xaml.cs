//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// Turning the cloud backup off, and the offer that goes with it.
    /// </summary>
    /// <remarks>
    /// Updating the secret phrase belongs here rather than anywhere else: disabling the backup
    /// stops the server keeping the phrase from now on, but it is the *same* phrase it has been
    /// keeping. A new one, generated on this device, is the only way for the user to hold a phrase
    /// the server has never seen - which is what most people think disabling a backup means.
    ///
    /// It is a transaction on the chain, so it costs a fee and the wallet has to be able to pay it.
    /// </remarks>
    public sealed partial class WalletDisableBackupPopup : WalletPopup
    {
        private readonly IClientService _clientService;

        // Worked out before this opened, because working it out asks the user for things. Null
        // when it could not be, and then there is nothing to offer.
        private readonly BigInteger? _fee;

        /// <param name="fee">
        /// What a phrase update would cost. The caller obtains it first: pricing one means binding
        /// this device and signing an emulated message, and asking for either from inside a popup
        /// the user opened to make one decision reads as the app changing the subject.
        /// </param>
        public WalletDisableBackupPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, BigInteger? fee)
            : base(wallet, navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _fee = fee;

            // An update that cannot be priced cannot be offered: the alternative is a tick box
            // that says nothing about what it costs and may not be affordable at all.
            UpdatePhrase.Visibility = fee.HasValue
                ? Visibility.Visible
                : Visibility.Collapsed;

            Title = Strings.WalletDisableBackupTitle;
            PrimaryButtonText = Strings.WalletDisable;
            SecondaryButtonText = Strings.Cancel;
            PrimaryButtonStyle = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
        }

        /// <summary>
        /// Whether the user also asked for a phrase the server has never seen.
        /// </summary>
        public bool IsPhraseUpdateRequested => UpdatePhrase.IsChecked == true;

        protected override void UpdateWalletState(WalletState state)
        {
            // The balance decides whether the fee can be paid at all, and it can move while this
            // is open - a transfer landing is exactly what would make it affordable.
            UpdatePhraseInfo();
        }

        private void UpdatePhrase_Toggled(object sender, RoutedEventArgs e)
        {
            PhraseInfo.Visibility = UpdatePhrase.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdatePhraseInfo();
        }

        /// <summary>
        /// What the border says: what an update is, what it costs, or that it cannot be paid for.
        /// </summary>
        private void UpdatePhraseInfo()
        {
            if (UpdatePhrase.IsChecked != true)
            {
                // Disabling the backup on its own costs nothing, so whatever the fee said stops
                // applying the moment the box is cleared.
                IsPrimaryButtonEnabled = true;
                return;
            }

            if (_fee is not BigInteger fee || fee <= BigInteger.Zero)
            {
                // Nothing to say about a cost. A key change is an external message and the chain
                // charges for one, so this should not happen - but "Network fee: 0" would be a
                // stranger thing to show than saying nothing about the fee at all.
                //
                // The unpriced case is a guard rather than a state: an update that could not be
                // priced is not offered, so the box that leads here is not there to tick.
                PhraseInfoLabel.Text = Strings.WalletUpdateSecretPhraseInfo;
                IsPrimaryButtonEnabled = true;
                return;
            }

            var affordable = State.BalanceNanograms >= fee;

            PhraseInfoLabel.Text = string.Format(
                affordable ? Strings.WalletUpdateSecretPhraseInfoWithFee : Strings.WalletUpdateSecretPhraseInsufficientFunds,
                Formatter.TonBalance(fee).Join(),
                Fiat(fee));

            // Nothing can be signed that cannot be paid for, and letting them press Disable would
            // mean either failing afterwards or quietly dropping the half they asked for.
            IsPrimaryButtonEnabled = affordable;
        }

        private string Fiat(BigInteger nanograms)
        {
            return WalletHelper.TryToCurrency(_clientService, State, nanograms, out var amount)
                ? Formatter.FormatAmountExact(amount, WalletHelper.CurrencyDecimals, State?.Currency ?? "USD")
                : string.Empty;
        }
    }
}
