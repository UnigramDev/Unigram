using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLMessageActionAdminLogEvent : TLMessageActionBase
    {
        public TLChannelAdminLogEvent Event { get; set; }
    }
}
