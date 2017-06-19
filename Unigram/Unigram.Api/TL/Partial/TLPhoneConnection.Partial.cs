using libtgvoip;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLPhoneConnection
    {
        public Endpoint ToEndpoint()
        {
            return new Endpoint
            {
                id = Id,
                ipv4 = Ip,
                ipv6 = Ipv6,
                port = (ushort)Port,
                peerTag = PeerTag
            };
        }
    }
}
