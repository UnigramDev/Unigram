using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLDialogWith : INotifyPropertyChanged
    {
        object PhotoSelf { get; }
        string DisplayName { get; }

        TLInputPeerBase ToInputPeer();
    }
}
