using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;

namespace Unigram.ViewModels.Settings
{
    public class SettingsBlockUserViewModel : UsersSelectionViewModel
    {
        public SettingsBlockUserViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
        }

        public override int Maximum { get  { return int.MaxValue; } }

        protected override async void SendExecute()
        {
            foreach (var item in SelectedItems)
            {
                if (item.HasAccessHash)
                {
                    var result = await ProtoService.BlockAsync(item.ToInputUser());
                    if (result.IsSucceeded)
                    {
                        //Aggregator.Publish(new TLUpdateUserBlocked { UserId = item.Id, Blocked = true });
                    }
                }
            }

            NavigationService.GoBack();
        }
    }
}
