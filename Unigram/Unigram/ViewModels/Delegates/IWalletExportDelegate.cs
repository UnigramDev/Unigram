using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.ViewModels.Wallet;

namespace Unigram.ViewModels.Delegates
{
    public interface IWalletExportDelegate : IViewModelDelegate
    {
        void UpdateWordList(IList<WalletWordViewModel> wordList);
    }
}
