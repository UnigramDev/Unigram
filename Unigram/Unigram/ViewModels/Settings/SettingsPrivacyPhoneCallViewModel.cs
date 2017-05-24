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
    public class SettingsPrivacyPhoneCallViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyPhoneCallViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator, new TLInputPrivacyKeyPhoneCall(), TLType.PrivacyKeyPhoneCall)
        {
        }
    }
}
