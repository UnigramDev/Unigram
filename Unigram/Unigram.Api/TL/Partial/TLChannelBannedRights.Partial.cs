using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLChannelBannedRights
    {
        public bool IsForever()
        {
            return Math.Abs(UntilDate - Utils.CurrentTimestamp / 1000) > 5 * 365 * 24 * 60 * 60;
        }
    }
}
