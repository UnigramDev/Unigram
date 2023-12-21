using Telegram.Navigation;
using Telegram.Services;

namespace Telegram.ViewModels.Settings
{
    public class SettingsProfileColorViewModel : ViewModelBase
    {
        public SettingsProfileColorViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }
    }
}
