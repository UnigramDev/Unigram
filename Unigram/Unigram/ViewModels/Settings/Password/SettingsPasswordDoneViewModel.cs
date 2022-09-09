using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Settings.Password;

namespace Unigram.ViewModels.Settings.Password
{
    public class SettingsPasswordDoneViewModel : TLViewModelBase
    {
        public SettingsPasswordDoneViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            NavigationService.Navigate(typeof(SettingsPasswordCreatePage));
        }
    }
}
