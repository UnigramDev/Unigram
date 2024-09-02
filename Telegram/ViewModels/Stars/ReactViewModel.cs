//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Payments;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Stars
{
    public class ReactViewModel : ViewModelBase, IHandle
    {
        public ReactViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            OwnedStarCount = clientService.OwnedStarCount;

            TopReactors = new ObservableCollection<PaidReactor>();
        }

        private long _ownedStarCount;
        public long OwnedStarCount
        {
            get => _ownedStarCount;
            set => Set(ref _ownedStarCount, value);
        }

        public ObservableCollection<PaidReactor> TopReactors { get; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is Message message)
            {
                TopReactors.AddRange(message.InteractionInfo.Reactions.PaidReactors);
            }

            _ = ClientService.GetStarTransactionsAsync(ClientService.MyId, string.Empty, null, string.Empty, 1);
            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateOwnedStarCount>(this, Handle);
        }

        private void Handle(UpdateOwnedStarCount update)
        {
            BeginOnUIThread(() => RaisePropertyChanged(nameof(OwnedStarCount)));
        }

        public async Task<PayResult> SubmitAsync()
        {

            return PayResult.Failed;
        }
    }
}
