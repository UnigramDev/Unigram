using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public class BusinessBotsViewModel : BusinessRecipientsViewModelBase
    {
        public BusinessBotsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        private int _periodDays;
        public int PeriodDays
        {
            get => _periodDays;
            set => Set(ref _periodDays, value);
        }

        protected override Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            return Task.CompletedTask;
        }

        public override void Continue()
        {
            throw new System.NotImplementedException();
        }
    }
}
