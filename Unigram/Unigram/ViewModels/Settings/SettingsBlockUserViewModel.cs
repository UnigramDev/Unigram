using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsBlockUserViewModel : UsersSelectionViewModel
    {
        public SettingsBlockUserViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator) 
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        public override int Maximum => 1;

        protected override void SendExecute(User user)
        {
            if (user != null)
            {
                ProtoService.Send(new BlockUser(user.Id));
            }

            NavigationService.GoBack();
        }
    }
}
