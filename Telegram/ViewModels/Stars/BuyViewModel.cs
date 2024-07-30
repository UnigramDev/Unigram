//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Stars.Popups;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Stars
{
    public class BuyViewModel : ViewModelBase, IHandle
    {
        private IList<StarPaymentOption> _options;

        public BuyViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Options = new MvxObservableCollection<StarPaymentOption>();
            OwnedStarCount = clientService.OwnedStarCount;
        }

        private long _ownedStarCount;
        public long OwnedStarCount
        {
            get => _ownedStarCount;
            set => Set(ref _ownedStarCount, value);
        }

        public BuyStarsArgs Arguments { get; private set; }

        public MvxObservableCollection<StarPaymentOption> Options { get; private set; }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState _)
        {
            if (parameter is BuyStarsArgs args)
            {
                Arguments = args;
            }

            var response = await ClientService.SendAsync(new GetStarPaymentOptions());
            if (response is StarPaymentOptions options)
            {
                _options = options.Options.ToList();
                Options.ReplaceWith(options.Options.Where(x => !x.IsAdditional));

                RaisePropertyChanged(nameof(CanExpand));
            }

            await ClientService.GetStarTransactionsAsync(ClientService.MyId, null, string.Empty, 1);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateOwnedStarCount>(this, Handle);
        }

        private void Handle(UpdateOwnedStarCount update)
        {
            BeginOnUIThread(() => RaisePropertyChanged(nameof(OwnedStarCount)));
        }

        public int IndexOf(StarPaymentOption option)
        {
            return _options.IndexOf(option);
        }

        public bool CanExpand => _options?.Count > Options.Count;

        public void Expand()
        {
            for (int i = 0; i < _options.Count; i++)
            {
                if (Options.Count < i || Options[i] != _options[i])
                {
                    Options.Insert(i, _options[i]);
                }
            }

            RaisePropertyChanged(nameof(CanExpand));
        }
    }
}
