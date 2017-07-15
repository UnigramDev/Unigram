using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.TL
{
    public interface ITLInputPeer
    {
        TLInputPeerBase ToInputPeer();
    }
}
