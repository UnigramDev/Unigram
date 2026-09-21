//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// A popup that shows something the wallet knows, and keeps showing the current answer.
    /// </summary>
    /// <remarks>
    /// Two jobs, and neither is optional for anything reading <see cref="State"/>:
    ///
    /// **Restoring.** The state is only loaded when something asks for it, and opening the wallet
    /// window is what usually does. Reached from anywhere else - a chat, a deep link - the service
    /// still holds <see cref="WalletState.None"/>, which is not an empty state but a wrong one: its
    /// currency is USD at a rate of one, so amounts are priced in dollars wearing another currency's
    /// name, and its balance is zero, so everything reads as insufficient funds.
    ///
    /// **Subscribing.** A balance moves while a popup is open - an incoming transfer over the chain
    /// stream, the rates arriving, a transfer settling - and a popup that read the state once shows
    /// what was true when it opened.
    /// </remarks>
    public abstract partial class WalletPopup : ModalPopup
    {
        private readonly IEventAggregator _aggregator;

        protected readonly IWalletService _wallet;

        protected WalletPopup(IWalletService wallet, INavigationService navigationService)
        {
            _wallet = wallet;
            _aggregator = navigationService.Session.Resolve<IEventAggregator>();
        }

        /// <summary>
        /// What the wallet currently knows. Never null, and never stale for longer than it takes an
        /// update to arrive.
        /// </summary>
        protected WalletState State => _wallet.State;

        protected override void OnLoaded()
        {
            base.OnLoaded();

            _aggregator.Subscribe<UpdateWalletState>(this, Handle);

            // Already restored in the usual case, where it answers from the field it holds without
            // reaching for anything. The await is for the first time, and what it changes is
            // announced through the same subscription as everything else - so there is nothing to
            // do with its result here.
            RestoreAsync();
        }

        protected override void OnUnloaded()
        {
            _aggregator.Unsubscribe(this);

            base.OnUnloaded();
        }

        private async void RestoreAsync()
        {
            await _wallet.RestoreAsync();

            // Raised unconditionally rather than only on a change: a popup that opened against
            // WalletState.None has drawn itself with the stand-in values, and a restore that found
            // nothing new still has to correct that.
            UpdateWalletState(_wallet.State);
        }

        private void Handle(UpdateWalletState update)
        {
            this.BeginOnUIThread(() => UpdateWalletState(update.State));
        }

        /// <summary>
        /// The state changed, or arrived for the first time. Called on the UI thread.
        /// </summary>
        protected abstract void UpdateWalletState(WalletState state);
    }
}
