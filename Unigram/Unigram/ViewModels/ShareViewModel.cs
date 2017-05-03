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

namespace Unigram.ViewModels
{
    public class ShareViewModel : DialogsViewModel
    {
        public ShareViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            GroupedItems = new ObservableCollection<ShareViewModel> { this };
            LoadFirstSlice();
        }

        public ObservableCollection<ShareViewModel> GroupedItems { get; private set; }
    }
}
