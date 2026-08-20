//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Wallet
{
    public class WalletViewModel : ViewModelBase
    {
        private readonly IWalletService _wallet;

        public WalletViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IWalletService wallet)
            : base(clientService, settingsService, aggregator)
        {
            _wallet = wallet;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateWalletState>(this, Handle);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            // No refresh here. Attaching already starts one, and a second refresh supersedes the
            // first and cancels its requests - so refreshing on navigation would throw away the
            // load that is already in flight. A deliberate refresh gesture can call RefreshAsync.
            //
            // Apply what is already known before waiting on anything: a wallet that was restored
            // earlier in the session has its balance in hand, and the page should not blank out.
            Apply(_wallet.State);
            Apply(await _wallet.RestoreAsync());
        }

        // Published from the engine's pump thread, so the hop happens here, where there is a
        // dispatcher to hop to.
        public void Handle(UpdateWalletState update)
        {
            BeginOnUIThread(() => Apply(update.State));
        }

        private void Apply(WalletState state)
        {
            HasWallet = state.HasWallet;
            Address = state.Address;
            Balance = state.BalanceNanograms;
            IsSynchronized = state.IsSynchronized;
        }

        private bool _hasWallet;
        public bool HasWallet
        {
            get => _hasWallet;
            set => Set(ref _hasWallet, value);
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        /// <summary>
        /// The balance in nanograms. See <see cref="WalletState.BalanceNanograms"/> for why this is
        /// not a <see cref="long"/>.
        /// </summary>
        private BigInteger _balance;
        public BigInteger Balance
        {
            get => _balance;
            set => Set(ref _balance, value);
        }

        /// <summary>
        /// False means the balance is not known yet, which the view must not render as zero.
        /// </summary>
        private bool _isSynchronized;
        public bool IsSynchronized
        {
            get => _isSynchronized;
            set => Set(ref _isSynchronized, value);
        }
    }
}
