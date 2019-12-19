using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.ViewModels.Delegates
{
    public interface ISignInDelegate : IViewModelDelegate
    {
        void UpdateQrCodeMode(QrCodeMode mode);
        void UpdateQrCode(string link);
    }

    public enum QrCodeMode
    {
        Loading,
        Primary,
        Secondary,
        Disabled
    }
}
