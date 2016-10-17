using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
   public class SettingsViewModel : UnigramViewModelBase
    {
        public SettingsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) : base(protoService, cacheService, aggregator)
        {

        }


        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var user = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
            if (user != null)
                Item = user;
        }

        private TLUser _item;
        public TLUser Item
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
            }
        }
    }
}
