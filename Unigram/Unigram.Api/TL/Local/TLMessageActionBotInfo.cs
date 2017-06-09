using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL.Local
{
    public class TLMessageActionBotInfo : TLMessageActionBase
    {
        public TLBotInfo BotInfo { get; set; }
    }
}
