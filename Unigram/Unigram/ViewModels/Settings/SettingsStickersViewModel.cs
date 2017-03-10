using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Core;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStickersViewModel : UnigramViewModelBase
    {
        public SettingsStickersViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<TLStickerSetCovered>();
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var cached = DatabaseContext.Current.SelectStickerSetsAsCovered();
                Execute.BeginOnUIThread(() =>
                {
                    Items.AddRange(cached);
                });
            });

            return Task.CompletedTask;
        }

        public ObservableCollection<TLStickerSetCovered> Items { get; private set; }
    }
}
