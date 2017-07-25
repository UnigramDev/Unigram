using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public interface ITLMessageMediaDestruct : INotifyPropertyChanged
    {
        Int32? TTLSeconds { get; set; }
        Boolean HasTTLSeconds { get; set; }

        DateTime? DestructDate { get; set; }
        Int32? DestructIn { get; set; }
    }
}
