using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLStickerSet : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }
}
