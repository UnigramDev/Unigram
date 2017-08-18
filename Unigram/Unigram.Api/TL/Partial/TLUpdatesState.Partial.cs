using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL.Updates
{
    public partial class TLUpdatesState
    {
        public override string ToString()
        {
            return string.Format("p={0} q={1} s={2} u_c={3} d={4} [{5}]", Pts, Qts, Seq, UnreadCount, Date, TLUtils.ToDateTime(Date));
        }
    }
}
