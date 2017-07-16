using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public enum TLMessageState
    {
        Sending = 1,
        Confirmed = 0,
        Failed = 2,
        Read = 3,
        Broadcast = 4,
        Compressing = 5,
    }
}
