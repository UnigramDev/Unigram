using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;

namespace Unigram.Views
{
    public class ScheduledChatPage : ChatPage
    {
        protected override DialogViewModel GetViewModel()
        {
            return TLContainer.Current.Resolve<DialogScheduledViewModel, IDialogDelegate>(this);
        }
    }
}
