using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Utils;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class CallsViewModel : UnigramViewModelBase
    {
        public CallsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Items = new ObservableCollection<TLMessageService>();
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await ProtoService.SearchAsync(new TLInputPeerEmpty(), null, new TLInputMessagesFilterPhoneCalls(), 0, int.MaxValue, 0, 0, 20000);
            if (response.IsSucceeded)
            {
                Items.AddRange(response.Result.Messages.OfType<TLMessageService>(), true);
            }
        }

        public ObservableCollection<TLMessageService> Items { get; private set; }
    }
}
