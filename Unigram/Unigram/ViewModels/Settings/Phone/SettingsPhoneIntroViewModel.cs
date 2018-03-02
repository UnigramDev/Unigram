using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPhoneIntroViewModel : UnigramViewModelBase
    {
        public SettingsPhoneIntroViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
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
            get
            {
                return _self;
            }
            set
            {
                Set(ref _self, value);
            }
        }
    }
}
