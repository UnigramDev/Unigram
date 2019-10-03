using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ton.Tonlib.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface IWalletTransactionDelegate : IViewModelDelegate
    {
        void UpdateTransaction(RawTransaction item);
    }
}
