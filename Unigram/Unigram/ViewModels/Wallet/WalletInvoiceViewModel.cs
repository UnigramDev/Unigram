using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Services;

namespace Unigram.ViewModels.Wallet
{
    public class WalletInvoiceViewModel : WalletReceiveViewModel
    {
        public WalletInvoiceViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
        }

        private long _amount;
        public long Amount
        {
            get => _amount;
            set => Set(ref _amount, value);
        }

        private string _comment;
        public string Comment
        {
            get => _comment;
            set => Set(ref _comment, value);
        }
    }
}
