using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Updates;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsAdvancedViewModel : TLViewModelBase
    {
        public SettingsAdvancedViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        public bool IsAdaptiveWideEnabled
        {
            get
            {
                return Settings.IsAdaptiveWideEnabled;
            }
            set
            {
                Settings.IsAdaptiveWideEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool PreferIpv6
        {
            get
            {
                return CacheService.Options.PreferIpv6;
            }
            set
            {
                CacheService.Options.PreferIpv6 = value;
                RaisePropertyChanged();
            }
        }

        public bool IsTrayVisible
        {
            get { return Settings.IsTrayVisible; }
            set
            {
                if (Settings.IsTrayVisible != value)
                {
                    Settings.IsTrayVisible = value;
                    RaisePropertyChanged();

                    if (App.Connection != null)
                    {
                        App.Connection.SendMessageAsync(new Windows.Foundation.Collections.ValueSet { { "IsTrayVisible", value } });
                    }
                }
            }
        }
    }
}
