using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Entities
{
    public enum PhoneCallState
    {
        WaitInit = 1,
        WaitInitAck,
        Established,
        Failed,
        Requesting,
        Waiting,
        WaitingIncoming,
        ExchangingKeys,
        HangingUp,
        Busy,
        Ringing,
        Ended
    }
}
