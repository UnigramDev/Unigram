using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;

namespace Unigram.ViewModels.Settings
{
    public class SettingsGenericViewModel : UnigramViewModelBase
    {
        public SettingsGenericViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public bool IsSendByEnterEnabled
        {
            get
            {
                return ApplicationSettings.Current.IsSendByEnterEnabled;
            }
            set
            {
                ApplicationSettings.Current.IsSendByEnterEnabled = value;
            }
        }

        public bool IsReplaceEmojiEnabled
        {
            get
            {
                return ApplicationSettings.Current.IsReplaceEmojiEnabled;
            }
            set
            {
                ApplicationSettings.Current.IsReplaceEmojiEnabled = value;
            }
        }
    }
}
