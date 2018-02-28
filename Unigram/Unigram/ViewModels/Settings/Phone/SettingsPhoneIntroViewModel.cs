using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
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
            //var cached = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
            //if (cached != null)
            //{
            //    Self = cached;
            //}
            //else
            //{
            //    var response = await LegacyService.GetUsersAsync(new TLVector<TLInputUserBase> { new TLInputUserSelf() });
            //    if (response.IsSucceeded)
            //    {
            //        var result = response.Result.FirstOrDefault() as TLUser;
            //        if (result != null)
            //        {
            //            Self = result;
            //            SettingsHelper.UserId = result.Id;
            //        }
            //    }
            //}
        }

        private TLUser _self;
        public TLUser Self
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
