//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Windows.UI.Xaml;

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

        // The operation's, and disposed by it.
        private readonly WalletVault.WalletVaultLease _lease;

        // Priced only once the box is ticked: pricing signs an emulated message, so it costs a
        // confirmation that disabling on its own would not ask for here.
        private BigInteger? _fee;
        private bool _pricing;

        public WalletDisableBackupPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, WalletVault.WalletVaultLease lease)
            : base(wallet, navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _lease = lease;

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
            var isChecked = UpdatePhrase.IsChecked == true;

            PhraseInfo.Visibility = isChecked
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (isChecked && _fee == null && !_pricing)
            {
                RequestFee();
            }

            IsPrimaryButtonPending = isChecked && _pricing;
            UpdatePhraseInfo();
        }

        private async void RequestFee()
        {
            _pricing = true;
            IsPrimaryButtonPending = true;

            var declined = false;

            try
            {
                _fee = await _wallet.EstimateKeyRotationFeeAsync(_lease);
            }
            catch (WalletAccessDeniedException)
            {
                declined = true;
            }
            catch (Exception ex)
            {
                Logger.Error("wallet key rotation fee could not be estimated: " + ex.Message);
            }

            _pricing = false;
            IsPrimaryButtonPending = false;

            if (_fee == null)
            {
                // Declined: the box clears, and ticking it again asks again. Not priced: an update
                // that cannot be priced cannot be offered - the alternative is a tick box that says
                // nothing about what it costs and may not be affordable at all.
                UpdatePhrase.IsChecked = false;

                if (!declined)
                {
                    UpdatePhrase.IsEnabled = false;
                }

                return;
            }

            UpdatePhraseInfo();
        }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            // An update not yet priced may turn out not to be affordable, and Disable would then
            // either fail afterwards or quietly drop the half they asked for.
            if (UpdatePhrase.IsChecked == true && _pricing)
            {
                args.Cancel = true;
            }
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
                // Still being priced, or nothing to say about a cost. A key change is an external
                // message and the chain charges for one, so a zero fee should not happen - but
                // "Network fee: 0" would be a stranger thing to show than saying nothing at all.
                PhraseInfoLabel.Text = Strings.WalletUpdateSecretPhraseInfo;
                IsPrimaryButtonEnabled = true;
                return;
            }

            var affordable = State.BalanceNanograms >= fee;

            PhraseInfoLabel.Text = string.Format(
                affordable ? Strings.WalletUpdateSecretPhraseInfoWithFee : Strings.WalletUpdateSecretPhraseInsufficientFunds,
                Formatter.GramExact(fee).Join(),
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
