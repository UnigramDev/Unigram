using Microsoft.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Navigation.Services;
using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPhoneIntroViewModel : TLViewModelBase
    {
        public SettingsPhoneIntroViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ProtoService.SendAsync(new GetMe());
            if (response is User user)
            {
                Self = user;
            }
        }

        private User _self;
        public User Self
        {
            get => _self;
            set => Set(ref _self, value);
        }
    }
}
