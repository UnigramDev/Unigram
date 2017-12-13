using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLIpPort
    {
        public string GetIpString()
        {
            var ip = BitConverter.GetBytes(Ipv4);
            return string.Format("{0}.{1}.{2}.{3}", ip[3], ip[2], ip[1], ip[0]);
        }
    }
}
