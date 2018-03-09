using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.ViewModels.Delegates
{
    public interface IDelegable<TDelegate> where TDelegate : IViewModelDelegate
    {
        TDelegate Delegate { get; set; }
    }

    public interface IViewModelDelegate
    {

    }
}
